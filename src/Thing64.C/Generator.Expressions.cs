namespace Thing64.C;

public sealed partial class Generator
{
    // ------------------------------------------------------------------------------------------------- types

    // The type of an expression, without generating it. A constant has the smallest type that holds its value
    CType TypeOf(Expression e) => e switch
    {
        CastExpression c => c.Type,
        StringExpression => CType.PointerTo(CType.Char),
        NameExpression n => Find(n.Name, n).Type,
        _ when Constants.Evaluate(e) is int value => LiteralType(value),
        _ => TypeOfOperation(e),
    };

    static CType LiteralType(int value) =>
        value is >= 0 and <= 255 ? CType.Char : value is >= -128 and < 0 ? CType.SignedChar : value is >= -32768 and <= 32767 ? CType.Int : CType.UnsignedInt;

    CType TypeOfOperation(Expression e) => e switch
    {
        NumberExpression n => LiteralType(n.Value),
        StringExpression => CType.PointerTo(CType.Char),
        NameExpression n => Find(n.Name, n).Type,
        UnaryExpression { Operator: "!" } => CType.Char,
        UnaryExpression { Operator: "*" } u => Pointee(TypeOf(u.Operand), u),
        UnaryExpression { Operator: "&" } u => CType.PointerTo(TypeOf(u.Operand)),
        UnaryExpression { Operator: "sizeof" } => CType.UnsignedInt,
        UnaryExpression u => Decay(TypeOf(u.Operand)),
        PostfixExpression p => TypeOf(p.Operand),
        BinaryExpression { Operator: "==" or "!=" or "<" or ">" or "<=" or ">=" or "&&" or "||" } => CType.Char,
        BinaryExpression { Operator: "<<" or ">>" } b => Decay(TypeOf(b.Left)),
        BinaryExpression b => BinaryType(b),
        AssignExpression a => TypeOf(a.Target),
        ConditionalExpression c => Common(c.Then, c.Else),
        CallExpression c => FunctionOf(c).Return,
        IndexExpression i => Pointee(TypeOf(i.Array), i),
        CastExpression c => c.Type,
        SizeofExpression => CType.UnsignedInt,
        _ => throw new CException("Unknown expression", e.Line, e.Column),
    };

    static CType Decay(CType type) => type.Kind == CTypeKind.Array ? CType.PointerTo(type.Target!) : type;

    static CType Pointee(CType type, Node at) =>
        type.IsPointerLike ? type.Target! : throw new CException($"A {type} is not a pointer", at.Line, at.Column);

    CType BinaryType(BinaryExpression b)
    {
        var left = Decay(TypeOf(b.Left)); var right = Decay(TypeOf(b.Right));
        if (left.Kind == CTypeKind.Pointer && right.Kind == CTypeKind.Pointer && b.Operator == "-") return CType.Int;
        if (left.Kind == CTypeKind.Pointer) return left;
        if (right.Kind == CTypeKind.Pointer && b.Operator == "+") return right;
        return Common(b.Left, b.Right);
    }

    // The type two operands are computed in. A constant takes the other operand's type when its value fits
    // (x + 1 with a char x stays on 8 bits, s < 3 with a signed char s is a signed comparison)
    CType Common(Expression left, Expression right)
    {
        var l = Decay(TypeOf(left)); var r = Decay(TypeOf(right));
        if (Constants.Evaluate(right) is int rv && left is not StringExpression && Fits(rv, l)) return l.Kind == CTypeKind.Pointer ? CType.UnsignedInt : l;
        if (Constants.Evaluate(left) is int lv && Fits(lv, r)) return r.Kind == CTypeKind.Pointer ? CType.UnsignedInt : r;
        return Arithmetic(l, r);
    }

    static bool Fits(int value, CType type) => type.Kind switch
    {
        CTypeKind.Char => type.Signed ? value is >= -128 and <= 127 : value is >= 0 and <= 255,
        CTypeKind.Int => type.Signed ? value is >= -32768 and <= 32767 : value is >= 0 and <= 65535,
        CTypeKind.Pointer => value is >= 0 and <= 65535,
        _ => false,
    };

    // The common type of two operand types: the wider; unsigned when an operand of that width is unsigned. Two
    // chars of different signedness meet in an int, as C promotes them
    static CType Arithmetic(CType a, CType b)
    {
        if (a.Kind == CTypeKind.Pointer || b.Kind == CTypeKind.Pointer) return CType.UnsignedInt;
        if (a.Size == 2 || b.Size == 2)
        {
            bool unsigned = (a.Size == 2 && !a.Signed) || (b.Size == 2 && !b.Signed);
            return unsigned ? CType.UnsignedInt : CType.Int;
        }
        if (a.Signed != b.Signed) return CType.Int;
        return a.Signed ? CType.SignedChar : CType.Char;
    }

    Function FunctionOf(CallExpression c) =>
        functions.TryGetValue(c.Function, out var function) ? function
        : throw new CException($"{c.Function} is not a declared function", c.Line, c.Column);

    // ------------------------------------------------------------------------------------------------- operands

    // The assembly operands of an expression's low and high bytes, when it can be used as it is by an instruction:
    // a constant (#$05 / #$00), a variable (score / score+1), a fixed address (*(char*)0xD020: $D020)
    sealed record Operand(string Low, string High);

    Operand? Simple(Expression e, CType type)
    {
        if (Constants.Evaluate(e) is int value && e is not StringExpression)
            return new Operand("#" + Hex(value & 0xFF), "#" + Hex((value >> 8) & 0xFF));
        switch (e)
        {
            case NameExpression n:
            {
                var symbol = Find(n.Name, n);
                if (symbol.IsFunction) return null;
                if (symbol.IsEquate)
                {
                    if (symbol.Type.Size == 2) return new Operand($"#<{symbol.Label}", $"#>{symbol.Label}");
                    if (type.Size == 2 && symbol.Type.Signed) return null;
                    return new Operand($"#{symbol.Label}", "#$00");
                }
                if (symbol.Type.Kind == CTypeKind.Array) return new Operand($"#<{symbol.Label}", $"#>{symbol.Label}");
                if (symbol.Type.Size == 2) return new Operand(symbol.Label, symbol.Label + "+1");
                if (type.Size == 2 && symbol.Type.Signed) return null;   // needs its sign extended
                return new Operand(symbol.Label, "#$00");
            }
            case UnaryExpression { Operator: "*" } u when FixedPointer(u.Operand) is { } address:
            {
                var pointee = Pointee(TypeOf(u.Operand), u);
                if (pointee.Size == 2) return address;
                if (type.Size == 2 && pointee.Signed) return null;
                return address with { High = "#$00" };
            }
            case CastExpression c when c.Type.IsScalar:
            {
                var inner = Simple(c.Operand, c.Type);
                return inner is null ? null : c.Type.Size == 1 ? inner with { High = "#$00" } : inner;
            }
        }
        return null;
    }

    // A pointer known before the program runs: (char*)0xD020
    int? Address(Expression pointer) => Constants.Evaluate(pointer) is int value ? value & 0xFFFF : null;

    // The same, as the operands of the two bytes it points to: (char*)0xD020 is $D020 and $D021, an __equ pointer
    // SCREEN is SCREEN and SCREEN+1
    Operand? FixedPointer(Expression pointer)
    {
        if (Address(pointer) is int address) return new Operand(Hex(address), Hex(address + 1));
        if (pointer is NameExpression n && Lookup(n.Name) is { IsEquate: true, Type.Kind: CTypeKind.Pointer } symbol)
            return new Operand(symbol.Label, symbol.Label + "+1");
        return null;
    }

    // An __equ cannot change, nor has an address
    void NotEquate(Expression target)
    {
        if (target is NameExpression n && Lookup(n.Name) is { IsEquate: true })
            throw new CException($"{n.Name} is a constant of the assembly: it cannot change", n.Line, n.Column);
    }

    // ------------------------------------------------------------------------------------------------- values

    // The value of an expression converted to a type
    void GenerateAs(Expression e, CType type) => Convert(Generate(e), type);

    void Convert(CType from, CType to)
    {
        from = Decay(from);
        if (to.Size != 2 || from.Size != 1) return;
        if (!from.Signed)
        {
            Emit("LDX #$00");
            return;
        }
        string positive = NewLabel("positive");                       // sign extension: X = $FF when bit 7 is set
        Emit("LDX #$00");
        Emit("CMP #$80");
        Emit($"BCC {positive}");
        Emit("DEX");
        Label(positive);
    }

    void LoadConstant(int value, CType type)
    {
        Emit($"LDA #{Hex(value & 0xFF)}");
        if (type.Wide) Emit($"LDX #{Hex((value >> 8) & 0xFF)}");
    }

    // An expression's value in A (8 bits) or A and X (16 bits); returns its type
    CType Generate(Expression e)
    {
        if (e is not StringExpression && Constants.Evaluate(e) is int constant)
        {
            var constantType = TypeOf(e);
            LoadConstant(constant, constantType);
            return constantType;
        }
        switch (e)
        {
            case NameExpression n:
            {
                var symbol = Find(n.Name, n);
                if (symbol.IsFunction) throw new CException($"{n.Name} is a function: call it with ()", n.Line, n.Column);
                if (symbol.IsEquate)
                {
                    Emit(symbol.Type.Wide ? $"LDA #<{symbol.Label}" : $"LDA #{symbol.Label}");
                    if (symbol.Type.Wide) Emit($"LDX #>{symbol.Label}");
                    return symbol.Type;
                }
                if (symbol.Type.Kind == CTypeKind.Array)
                {
                    Emit($"LDA #<{symbol.Label}");
                    Emit($"LDX #>{symbol.Label}");
                    return CType.PointerTo(symbol.Type.Target!);
                }
                Emit($"LDA {symbol.Label}");
                if (symbol.Type.Wide) Emit($"LDX {symbol.Label}+1");
                return symbol.Type;
            }
            case StringExpression s:
            {
                string label = StringLabel(s.Text);
                Emit($"LDA #<{label}");
                Emit($"LDX #>{label}");
                return CType.PointerTo(CType.Char);
            }
            case UnaryExpression u: return Unary(u);
            case PostfixExpression p: return Increment(p.Operand, p.Operator == "++" ? 1 : -1, prefix: false, keep: true);
            case BinaryExpression b: return Binary(b);
            case AssignExpression a: return Assign(a, keep: true);
            case ConditionalExpression c:
            {
                var type = Common(c.Then, c.Else);
                string otherwise = NewLabel("else"), end = NewLabel("endif");
                Branch(c.Condition, false, otherwise);
                GenerateAs(c.Then, type);
                Emit($"JMP {end}");
                Label(otherwise);
                GenerateAs(c.Else, type);
                Label(end);
                return type;
            }
            case CallExpression c: return Call(c);
            case IndexExpression i: return Dereference(i);
            case CastExpression c:
            {
                Convert(Generate(c.Operand), c.Type);
                return c.Type;
            }
        }
        throw new CException("This expression is not supported yet", e.Line, e.Column);
    }

    // An expression whose value is not used: a statement
    void GenerateDiscarded(Expression e)
    {
        switch (e)
        {
            case AssignExpression a: Assign(a, keep: false); break;
            case PostfixExpression p: Increment(p.Operand, p.Operator == "++" ? 1 : -1, prefix: false, keep: false); break;
            case UnaryExpression { Operator: "++" or "--" } u: Increment(u.Operand, u.Operator == "++" ? 1 : -1, prefix: true, keep: false); break;
            default: Generate(e); break;
        }
    }

    CType Unary(UnaryExpression u)
    {
        switch (u.Operator)
        {
            case "-":
            {
                var type = Decay(Generate(u.Operand));
                if (!type.Wide)
                {
                    Emit("EOR #$FF");
                    Emit("CLC");
                    Emit("ADC #$01");
                    return type;
                }
                Emit("EOR #$FF");                                     // two's complement: invert, add 1
                Emit("CLC");
                Emit("ADC #$01");
                Emit("TAY");
                Emit("TXA");
                Emit("EOR #$FF");
                Emit("ADC #$00");
                Emit("TAX");
                Emit("TYA");
                return type;
            }
            case "~":
            {
                var type = Decay(Generate(u.Operand));
                Emit("EOR #$FF");
                if (type.Wide)
                {
                    Emit("TAY");
                    Emit("TXA");
                    Emit("EOR #$FF");
                    Emit("TAX");
                    Emit("TYA");
                }
                return type;
            }
            case "!": return GenerateBool(u);
            case "*": return Dereference(u);
            case "&": return GenerateAddress(u.Operand);
            case "++" or "--": return Increment(u.Operand, u.Operator == "++" ? 1 : -1, prefix: true, keep: true);
            case "sizeof":
            {
                int size = TypeOf(u.Operand).Size;
                LoadConstant(size, CType.UnsignedInt);
                return CType.UnsignedInt;
            }
        }
        throw new CException($"The operator {u.Operator} is not supported", u.Line, u.Column);
    }

    // 0 or 1 from a condition
    CType GenerateBool(Expression e)
    {
        string isFalse = NewLabel("false"), end = NewLabel("bool");
        Branch(e, false, isFalse);
        Emit("LDA #$01");
        Emit($"BNE {end}");
        Label(isFalse);
        Emit("LDA #$00");
        Label(end);
        return CType.Char;
    }

    // ------------------------------------------------------------------------------------------------- memory

    // a[i] is *(a + i); the fast forms: a fixed address, an array with a constant index or a char index in Y
    CType Dereference(Expression e)
    {
        var (pointer, at) = PointerOf(e);
        var type = Pointee(TypeOf(pointer), at);
        if (type.Kind == CTypeKind.Array) throw new CException("Arrays of arrays are not supported yet", at.Line, at.Column);
        if (FixedPointer(pointer) is { } address)
        {
            Emit($"LDA {address.Low}");
            if (type.Wide) Emit($"LDX {address.High}");
            return type;
        }
        if (Indexed(e) is { } indexed)
        {
            indexed.LoadY(this);
            if (type.Wide)
            {
                Emit($"LDA {indexed.HighOperand}");
                Emit("TAX");
            }
            Emit($"LDA {indexed.Operand}");
            return type;
        }
        Generate(pointer);
        Emit($"STA {Ptr}");
        Emit($"STX {Ptr}+1");
        if (type.Wide)
        {
            Emit("LDY #$01");
            Emit($"LDA ({Ptr}),Y");
            Emit("TAX");
            Emit("DEY");
        }
        else
        {
            Emit("LDY #$00");
        }
        Emit($"LDA ({Ptr}),Y");
        return type;
    }

    // *p and a[i] as (the pointer expression, where it is)
    (Expression Pointer, Node At) PointerOf(Expression e) => e switch
    {
        UnaryExpression { Operator: "*" } u => (u.Operand, u),
        IndexExpression i => (new BinaryExpression("+", i.Array, i.Index, i.Line, i.Column), i),
        _ => throw new CException("Expected a pointer or an array element", e.Line, e.Column),
    };

    // An element of an array with a fixed address (a global or local array) and a small index: label,Y
    // Fixed: the index is a constant, already in Base (label+offset): no Y at all
    sealed record IndexedForm(string Base, Expression Index, int Scale, bool Fixed = false)
    {
        public string Operand => Fixed ? Base : Base + ",Y";
        public string HighOperand => Fixed ? Base + "+1" : Base + "+1,Y";

        public void LoadY(Generator g)
        {
            if (Scale == 1 && g.Simple(Index, CType.Char) is { } operand && !operand.Low.StartsWith('#'))
            {
                g.Emit($"LDY {operand.Low}");
                return;
            }
            if (Fixed) return;
            g.GenerateAs(Index, CType.Char);
            if (Scale == 2) g.Emit("ASL");
            g.Emit("TAY");
        }
    }

    // Also a pointer known before the program runs, ((char*)0x0400)[i]: $0400,Y
    IndexedForm? Indexed(Expression e)
    {
        if (e is not IndexExpression index) return null;
        string label;
        CType element;
        int count;
        if (index.Array is NameExpression name && Lookup(name.Name) is { Type.Kind: CTypeKind.Array } symbol)
        {
            label = symbol.Label;
            element = symbol.Type.Target!;
            count = symbol.Type.Count;
        }
        else if (FixedPointer(index.Array) is { } address && TypeOf(index.Array) is { Kind: CTypeKind.Pointer } pointer)
        {
            label = address.Low;
            element = pointer.Target!;
            count = 128;
        }
        else
        {
            return null;
        }
        var indexType = TypeOf(index.Index);
        if (Constants.Evaluate(index.Index) is int constant)
            return new IndexedForm(constant == 0 ? label : $"{label}+{constant * element.Size}", index.Index, 1, Fixed: true);
        if (indexType.Size != 1 || indexType.Signed) return null;
        if (element.Size == 1) return new IndexedForm(label, index.Index, 1);
        if (element.Size == 2 && count <= 128) return new IndexedForm(label, index.Index, 2);
        return null;
    }

    // The address of something that can be assigned: &x, &a[i], &*p
    CType GenerateAddress(Expression e)
    {
        switch (e)
        {
            case NameExpression n:
            {
                NotEquate(n);
                var symbol = Find(n.Name, n);
                Emit($"LDA #<{symbol.Label}");
                Emit($"LDX #>{symbol.Label}");
                return CType.PointerTo(symbol.Type.Kind == CTypeKind.Array ? symbol.Type.Target! : symbol.Type);
            }
            case UnaryExpression { Operator: "*" } u:
                return Decay(Generate(u.Operand));
            case IndexExpression i:
                return Decay(Generate(new BinaryExpression("+", i.Array, i.Index, i.Line, i.Column)));
        }
        throw new CException("Only variables and elements have an address", e.Line, e.Column);
    }

    // ------------------------------------------------------------------------------------------------- assignment

    CType Assign(AssignExpression a, bool keep)
    {
        if (a.Operator == "=") return Store(a.Target, a.Value, keep);
        if (SideEffects(a.Target)) throw new CException($"{a.Operator} on a target with side effects is not supported", a.Line, a.Column);
        string op = a.Operator[..^1];
        return Store(a.Target, new BinaryExpression(op, a.Target, a.Value, a.Line, a.Column), keep);
    }

    // target = value; keep: the value stays in A (and X) afterwards
    CType Store(Expression target, Expression value, bool keep = false)
    {
        NotEquate(target);
        var type = TypeOf(target);
        if (type.Kind == CTypeKind.Array) throw new CException("An array cannot be assigned: assign its elements", target.Line, target.Column);
        bool reload = false;
        switch (target)
        {
            case NameExpression n:
            {
                var symbol = Find(n.Name, n);
                if (symbol.IsFunction) throw new CException($"{n.Name} is a function", n.Line, n.Column);
                GenerateAs(value, type);
                Emit($"STA {symbol.Label}");
                if (type.Wide) Emit($"STX {symbol.Label}+1");
                break;
            }
            case UnaryExpression { Operator: "*" } or IndexExpression:
            {
                var (pointer, _) = PointerOf(target);
                if (FixedPointer(pointer) is { } address)
                {
                    GenerateAs(value, type);
                    Emit($"STA {address.Low}");
                    if (type.Wide) Emit($"STX {address.High}");
                    break;
                }
                if (Indexed(target) is { } indexed && (indexed.Fixed || Simple(indexed.Index, CType.Char) is not null))
                {
                    GenerateAs(value, type);
                    if (!indexed.Fixed && indexed.Scale == 1)
                    {
                        Emit($"LDY {Simple(indexed.Index, CType.Char)!.Low}");
                    }
                    else if (!indexed.Fixed)
                    {
                        Emit("PHA");                                  // the value waits while Y gets the index
                        indexed.LoadY(this);
                        Emit("PLA");
                    }
                    Emit($"STA {indexed.Operand}");
                    if (type.Wide)
                    {
                        if (indexed.Fixed)
                        {
                            Emit($"STX {indexed.HighOperand}");
                        }
                        else
                        {
                            Emit("TXA");
                            Emit($"STA {indexed.HighOperand}");
                            reload = true;
                        }
                    }
                    break;
                }
                // The address first (in a temporary: the value may follow pointers too), then the value
                Generate(pointer);
                string temp = AllocateTemp();
                Emit($"STA {temp}");
                Emit($"STX {temp}+1");
                GenerateAs(value, type);
                Emit($"LDY {temp}");
                Emit($"STY {Ptr}");
                Emit($"LDY {temp}+1");
                Emit($"STY {Ptr}+1");
                Emit("LDY #$00");
                Emit($"STA ({Ptr}),Y");
                if (type.Wide)
                {
                    Emit("INY");
                    Emit("TXA");
                    Emit($"STA ({Ptr}),Y");
                    reload = true;
                }
                FreeTemp();
                break;
            }
            default:
                throw new CException("This cannot be assigned", target.Line, target.Column);
        }
        if (keep && reload) Generate(target);
        return type;
    }

    // ++x, x++, --x, x--: INC and DEC on variables, x = x + 1 otherwise (a pointer moves by its element's size)
    CType Increment(Expression target, int delta, bool prefix, bool keep)
    {
        NotEquate(target);
        var type = TypeOf(target);
        if (!type.IsScalar) throw new CException("Only numbers and pointers are incremented", target.Line, target.Column);
        int step = type.Kind == CTypeKind.Pointer ? type.Target!.Size : 1;
        if (target is NameExpression n && step == 1 && Find(n.Name, n) is { IsFunction: false } symbol)
        {
            if (keep && !prefix)
            {
                Emit($"LDA {symbol.Label}");                          // the old value stays in A (and X)
                if (type.Wide) Emit($"LDX {symbol.Label}+1");
            }
            if (!type.Wide)
            {
                Emit($"{(delta > 0 ? "INC" : "DEC")} {symbol.Label}");
            }
            else if (delta > 0)
            {
                string done = NewLabel("inc");
                Emit($"INC {symbol.Label}");
                Emit($"BNE {done}");
                Emit($"INC {symbol.Label}+1");
                Label(done);
            }
            else
            {
                string done = NewLabel("dec");
                Emit($"LDY {symbol.Label}");
                Emit($"BNE {done}");
                Emit($"DEC {symbol.Label}+1");
                Label(done);
                Emit($"DEC {symbol.Label}");
            }
            if (keep && prefix)
            {
                Emit($"LDA {symbol.Label}");
                if (type.Wide) Emit($"LDX {symbol.Label}+1");
            }
            return type;
        }
        if (SideEffects(target)) throw new CException("++ and -- on a target with side effects are not supported", target.Line, target.Column);
        var changed = new BinaryExpression(delta > 0 ? "+" : "-", target, new NumberExpression(1, target.Line, target.Column), target.Line, target.Column);
        if (!keep || prefix) return Store(target, changed, keep);
        Generate(target);                                             // x++ with its value: the old one, kept aside
        string temp = AllocateTemp();
        Emit($"STA {temp}");
        if (type.Wide) Emit($"STX {temp}+1");
        Store(target, changed);
        Emit($"LDA {temp}");
        if (type.Wide) Emit($"LDX {temp}+1");
        FreeTemp();
        return type;
    }

    static bool SideEffects(Expression e) => e switch
    {
        AssignExpression or CallExpression or PostfixExpression => true,
        UnaryExpression { Operator: "++" or "--" } => true,
        UnaryExpression u => SideEffects(u.Operand),
        BinaryExpression b => SideEffects(b.Left) || SideEffects(b.Right),
        IndexExpression i => SideEffects(i.Array) || SideEffects(i.Index),
        CastExpression c => SideEffects(c.Operand),
        ConditionalExpression c => SideEffects(c.Condition) || SideEffects(c.Then) || SideEffects(c.Else),
        _ => false,
    };

    static bool ContainsCall(Expression e) => e switch
    {
        CallExpression => true,
        UnaryExpression u => ContainsCall(u.Operand),
        PostfixExpression p => ContainsCall(p.Operand),
        BinaryExpression b => ContainsCall(b.Left) || ContainsCall(b.Right),
        AssignExpression a => ContainsCall(a.Target) || ContainsCall(a.Value),
        IndexExpression i => ContainsCall(i.Array) || ContainsCall(i.Index),
        CastExpression c => ContainsCall(c.Operand),
        ConditionalExpression c => ContainsCall(c.Condition) || ContainsCall(c.Then) || ContainsCall(c.Else),
        _ => false,
    };

    // ------------------------------------------------------------------------------------------------- temporaries

    // Temporaries of the function, used like a stack while an expression is generated
    string AllocateTemp()
    {
        string temp = $"{context!.Label}__t{context.Temps++}";
        context.MaxTemps = Math.Max(context.MaxTemps, context.Temps);
        return temp;
    }

    void FreeTemp() => context!.Temps--;

    // ------------------------------------------------------------------------------------------------- calls

    // The arguments into the parameters, then JSR. When an argument after the first calls a function, all go through
    // temporaries first: that call could be to the same function, and overwrite the parameters already written
    CType Call(CallExpression c)
    {
        var function = FunctionOf(c);
        if (c.Arguments.Count != function.Parameters.Count)
            throw new CException($"{c.Function} takes {function.Parameters.Count} arguments, not {c.Arguments.Count}", c.Line, c.Column);
        context!.Calls.Add(c.Function);
        string label = FunctionLabel(function);
        string Parameter(int i) => $"{label}__{function.Parameters[i].Name}";

        if (c.Arguments.Skip(1).Any(ContainsCall))
        {
            var temps = new List<string>();
            for (int i = 0; i < c.Arguments.Count; i++)
            {
                GenerateAs(c.Arguments[i], function.Parameters[i].Type);
                string temp = AllocateTemp();
                temps.Add(temp);
                Emit($"STA {temp}");
                if (function.Parameters[i].Type.Wide) Emit($"STX {temp}+1");
            }
            for (int i = 0; i < temps.Count; i++)
            {
                Emit($"LDA {temps[i]}");
                Emit($"STA {Parameter(i)}");
                if (function.Parameters[i].Type.Wide)
                {
                    Emit($"LDA {temps[i]}+1");
                    Emit($"STA {Parameter(i)}+1");
                }
            }
            foreach (var _ in temps) FreeTemp();
        }
        else
        {
            for (int i = 0; i < c.Arguments.Count; i++)
            {
                GenerateAs(c.Arguments[i], function.Parameters[i].Type);
                Emit($"STA {Parameter(i)}");
                if (function.Parameters[i].Type.Wide) Emit($"STX {Parameter(i)}+1");
            }
        }
        Emit($"JSR {label}");
        return function.Return;
    }
}
