namespace Thing64.C;

public sealed partial class Generator
{
    // ------------------------------------------------------------------------------------------------- arithmetic

    CType Binary(BinaryExpression b)
    {
        if (b.Operator is "==" or "!=" or "<" or ">" or "<=" or ">=" or "&&" or "||") return GenerateBool(b);

        var left = Decay(TypeOf(b.Left));
        var right = Decay(TypeOf(b.Right));

        // Pointer arithmetic: the integer counts elements, so it is multiplied by their size
        if (b.Operator is "+" or "-" && (left.Kind == CTypeKind.Pointer || right.Kind == CTypeKind.Pointer))
            return PointerArithmetic(b, left, right);

        var type = Common(b.Left, b.Right);
        switch (b.Operator)
        {
            case "+" or "-" or "&" or "|" or "^":
                Combine(b.Operator, b.Left, b.Right, type);
                return type;
            case "*":
                return Multiply(b.Left, b.Right, type);
            case "/" or "%":
                return Divide(b.Operator, b.Left, b.Right, type);
            case "<<" or ">>":
                return Shift(b.Operator, b.Left, b.Right, left);
        }
        throw new CException($"The operator {b.Operator} is not supported", b.Line, b.Column);
    }

    // left op right, with right as an operand of the instruction (directly, or from a temporary)
    void Combine(string op, Expression leftExpression, Expression rightExpression, CType type)
    {
        var right = Simple(rightExpression, type);
        string? temp = null;
        if (right is null)
        {
            GenerateAs(rightExpression, type);
            temp = AllocateTemp();
            Emit($"STA {temp}");
            if (type.Wide) Emit($"STX {temp}+1");
            right = new Operand(temp, temp + "+1");
        }
        GenerateAs(leftExpression, type);
        string instruction = op switch { "+" => "ADC", "-" => "SBC", "&" => "AND", "|" => "ORA", _ => "EOR" };
        if (op == "+") Emit("CLC");
        if (op == "-") Emit("SEC");
        Emit($"{instruction} {right.Low}");
        if (type.Wide)
        {
            Emit("TAY");                                              // the low byte waits in Y while X is done
            Emit("TXA");
            Emit($"{instruction} {right.High}");
            Emit("TAX");
            Emit("TYA");
        }
        if (temp is not null) FreeTemp();
    }

    CType PointerArithmetic(BinaryExpression b, CType left, CType right)
    {
        if (left.Kind == CTypeKind.Pointer && right.Kind == CTypeKind.Pointer)
        {
            if (b.Operator != "-") throw new CException("Two pointers can only be subtracted", b.Line, b.Column);
            Combine("-", b.Left, b.Right, CType.UnsignedInt);
            int size = left.Target!.Size;
            if (size == 2) ShiftConstant(">>", 1, CType.Int);
            else if (size > 2) throw new CException("Subtracting pointers to elements larger than 2 bytes is not supported", b.Line, b.Column);
            return CType.Int;
        }
        var (pointer, pointerType, index) = left.Kind == CTypeKind.Pointer ? (b.Left, left, b.Right) : (b.Right, right, b.Left);
        if (b.Operator == "-" && pointer != b.Left) throw new CException("An integer minus a pointer has no meaning", b.Line, b.Column);
        int elementSize = pointerType.Target!.Size;
        if (elementSize == 0) throw new CException("A void* has no element size: cast it first", b.Line, b.Column);
        Expression offset = new CastExpression(CType.UnsignedInt, index, index.Line, index.Column);
        if (elementSize > 1) offset = new BinaryExpression("*", offset, new NumberExpression(elementSize, index.Line, index.Column), index.Line, index.Column);
        Combine(b.Operator, new CastExpression(CType.UnsignedInt, pointer, pointer.Line, pointer.Column), offset, CType.UnsignedInt);
        return pointerType;
    }

    // * by a power of two is a shift; anything else calls the multiplication routine
    CType Multiply(Expression left, Expression right, CType type)
    {
        if (Constants.Evaluate(left) is int l && Constants.Evaluate(right) is null) (left, right) = (right, left);
        if (Constants.Evaluate(right) is int r && r > 0 && (r & (r - 1)) == 0)
        {
            GenerateAs(left, type);
            ShiftConstant("<<", System.Numerics.BitOperations.Log2((uint)r), type);
            return type;
        }
        RuntimeArguments(left, right);
        Use("mul16");
        Emit($"JSR {module}__mul16");
        return type;
    }

    CType Divide(string op, Expression left, Expression right, CType type)
    {
        if (op == "/" && !type.Signed && Constants.Evaluate(right) is int r && r > 0 && (r & (r - 1)) == 0)
        {
            GenerateAs(left, type);
            ShiftConstant(">>", System.Numerics.BitOperations.Log2((uint)r), type);
            return type;
        }
        if (op == "%" && !type.Signed && Constants.Evaluate(right) is int m && m > 0 && (m & (m - 1)) == 0 && m <= 256)
        {
            GenerateAs(left, type);
            Emit($"AND #{Hex((m - 1) & 0xFF)}");
            if (type.Wide) Emit("LDX #$00");
            return type;
        }
        RuntimeArguments(left, right, signed: type.Signed);
        string routine = type.Signed ? "divs16" : "divu16";
        Use(routine);
        Emit($"JSR {module}__{routine}");
        if (op == "%")
        {
            Emit($"LDA {module}__remainder");
            Emit($"LDX {module}__remainder+1");
        }
        return type;
    }

    // The two operands of a library routine, as 16-bit values in module__arg1 and module__arg2 (the right one
    // first, through a temporary: the left one may call the same routine)
    void RuntimeArguments(Expression left, Expression right, bool signed = false)
    {
        var wide = signed ? CType.Int : CType.UnsignedInt;
        GenerateAs(right, wide);
        string temp = AllocateTemp();
        Emit($"STA {temp}");
        Emit($"STX {temp}+1");
        GenerateAs(left, wide);
        Emit($"STA {module}__arg1");
        Emit($"STX {module}__arg1+1");
        Emit($"LDA {temp}");
        Emit($"STA {module}__arg2");
        Emit($"LDA {temp}+1");
        Emit($"STA {module}__arg2+1");
        FreeTemp();
    }

    void Use(string routine)
    {
        runtime.Add(routine);
        if (routine == "divs16") runtime.Add("divu16");
    }

    CType Shift(string op, Expression left, Expression right, CType type)
    {
        type = Decay(type);
        if (Constants.Evaluate(right) is int count)
        {
            GenerateAs(left, type);
            ShiftConstant(op, count, type);
            return type;
        }
        // A count known only when the program runs: a loop on Y
        GenerateAs(right, CType.Char);
        string counter = AllocateTemp();
        Emit($"STA {counter}");
        GenerateAs(left, type);
        string loop = NewLabel("shift"), done = NewLabel("shifted");
        Emit($"LDY {counter}");
        Emit($"BEQ {done}");
        string temp = AllocateTemp();
        if (type.Wide)
        {
            Emit($"STA {temp}");
            Emit($"STX {temp}+1");
        }
        Label(loop);
        ShiftOnce(op, type, temp);
        Emit("DEY");
        Emit($"BNE {loop}");
        if (type.Wide)
        {
            Emit($"LDA {temp}");
            Emit($"LDX {temp}+1");
        }
        Label(done);
        FreeTemp();
        FreeTemp();
        return type;
    }

    void ShiftConstant(string op, int count, CType type)
    {
        if (count <= 0) return;
        if (!type.Wide)
        {
            for (int i = 0; i < Math.Min(count, 8); i++) ShiftOnce(op, type, "");
            return;
        }
        if (op == "<<" && count >= 8)                                 // whole bytes move: the low byte becomes the high one
        {
            Emit("TAX");
            Emit("LDA #$00");
            count -= 8;
        }
        string temp = AllocateTemp();
        Emit($"STA {temp}");
        Emit($"STX {temp}+1");
        for (int i = 0; i < Math.Min(count, 16); i++) ShiftOnce(op, type, temp);
        Emit($"LDA {temp}");
        Emit($"LDX {temp}+1");
        FreeTemp();
    }

    // One bit: 8 bits in A; 16 bits in the temporary (low, high); >> keeps the sign of signed types
    void ShiftOnce(string op, CType type, string temp)
    {
        if (!type.Wide)
        {
            if (op == "<<") Emit("ASL");
            else if (type.Signed)
            {
                Emit("CMP #$80");                                      // carry = the sign bit, shifted back in
                Emit("ROR");
            }
            else Emit("LSR");
            return;
        }
        if (op == "<<")
        {
            Emit($"ASL {temp}");
            Emit($"ROL {temp}+1");
        }
        else
        {
            if (type.Signed)
            {
                Emit($"LDA {temp}+1");
                Emit("CMP #$80");
                Emit($"ROR {temp}+1");
            }
            else
            {
                Emit($"LSR {temp}+1");
            }
            Emit($"ROR {temp}");
        }
    }

    // ------------------------------------------------------------------------------------------------- conditions

    // Jumps to target when the condition is true (when = true) or false (when = false): comparisons become CMP and
    // a branch, && and || jump as soon as the result is known
    void Branch(Expression e, bool when, string target)
    {
        if (Constants.Evaluate(e) is int constant)
        {
            if ((constant != 0) == when) Emit($"JMP {target}");
            return;
        }
        switch (e)
        {
            case UnaryExpression { Operator: "!" } u:
                Branch(u.Operand, !when, target);
                return;
            case BinaryExpression { Operator: "&&" } b:
                if (when)
                {
                    string skip = NewLabel("and");
                    Branch(b.Left, false, skip);
                    Branch(b.Right, true, target);
                    Label(skip);
                }
                else
                {
                    Branch(b.Left, false, target);
                    Branch(b.Right, false, target);
                }
                return;
            case BinaryExpression { Operator: "||" } b:
                if (when)
                {
                    Branch(b.Left, true, target);
                    Branch(b.Right, true, target);
                }
                else
                {
                    string skip = NewLabel("or");
                    Branch(b.Left, true, skip);
                    Branch(b.Right, false, target);
                    Label(skip);
                }
                return;
            case BinaryExpression { Operator: "==" or "!=" or "<" or ">" or "<=" or ">=" } b:
                Compare(b, when, target);
                return;
        }
        // Any other value: true when not zero
        var type = Decay(Generate(e));
        if (type.Wide)
        {
            string temp = AllocateTemp();
            Emit($"STX {temp}");
            Emit($"ORA {temp}");
            FreeTemp();
        }
        else
        {
            Emit("CMP #$00");
        }
        Jump(when ? "BNE" : "BEQ", target);
    }

    // left op right: CMP for == and !=; for < and >= the carry (unsigned) or the sign corrected by the overflow
    // (signed). > and <= swap the operands
    void Compare(BinaryExpression b, bool when, string target)
    {
        var left = b.Left; var right = b.Right; string op = b.Operator;
        if (op is ">" or "<=")
        {
            (left, right) = (right, left);
            op = op == ">" ? "<" : ">=";
        }
        var type = Common(left, right);
        var operand = Simple(right, type);
        string? temp = null;
        if (operand is null)
        {
            GenerateAs(right, type);
            temp = AllocateTemp();
            Emit($"STA {temp}");
            if (type.Wide) Emit($"STX {temp}+1");
            operand = new Operand(temp, temp + "+1");
        }
        GenerateAs(left, type);

        if (op is "==" or "!=")
        {
            Emit($"CMP {operand.Low}");
            if (type.Wide)
            {
                string different = NewLabel("ne");
                Emit($"BNE {different}");
                Emit($"CPX {operand.High}");
                Label(different);
            }
            Jump((op == "==") == when ? "BEQ" : "BNE", target);
        }
        else
        {
            if (type.Wide)
            {
                Emit($"CMP {operand.Low}");                           // the carry of the low bytes goes into the high ones
                Emit("TXA");
                Emit($"SBC {operand.High}");
            }
            else if (type.Signed)
            {
                Emit("SEC");
                Emit($"SBC {operand.Low}");
            }
            else
            {
                Emit($"CMP {operand.Low}");
            }
            bool less = (op == "<") == when;
            if (type.Signed)
            {
                string corrected = NewLabel("sign");
                Emit($"BVC {corrected}");                             // with an overflow, the sign bit is the wrong one
                Emit("EOR #$80");
                Label(corrected);
                Jump(less ? "BMI" : "BPL", target);
            }
            else
            {
                Jump(less ? "BCC" : "BCS", target);
            }
        }
        if (temp is not null) FreeTemp();
    }

    // A branch to a label that may be farther than 127 bytes: the opposite branch skips a JMP
    void Jump(string branch, string target)
    {
        string opposite = branch switch
        {
            "BEQ" => "BNE", "BNE" => "BEQ", "BCC" => "BCS", "BCS" => "BCC", "BMI" => "BPL", "BPL" => "BMI", "BVC" => "BVS", _ => "BVC",
        };
        string skip = NewLabel("skip");
        Emit($"{opposite} {skip}");
        Emit($"JMP {target}");
        Label(skip);
    }
}
