namespace Thing64.C;

// From tokens to the syntax tree, by recursive descent: one method per level of C's precedence
public sealed class Parser
{
    readonly List<Token> tokens;
    int position;

    Parser(List<Token> tokens) => this.tokens = tokens;

    public static TranslationUnit Parse(string source) => new Parser(Lexer.Tokenize(source)).Unit();

    Token Current => tokens[position];
    Token Next(int offset = 1) => tokens[Math.Min(position + offset, tokens.Count - 1)];
    Token Take() => tokens[position++ < tokens.Count - 1 ? position - 1 : tokens.Count - 1];
    bool Accept(string text) { if (!Current.Is(text)) return false; position++; return true; }

    Token Expect(string text)
    {
        if (!Current.Is(text)) throw Error($"Expected '{text}' instead of {Current}");
        return Take();
    }

    string ExpectIdentifier()
    {
        if (Current.Kind != TokenKind.Identifier) throw Error($"Expected a name instead of {Current}");
        return Take().Text;
    }

    CException Error(string message, Token? at = null) => new(message, (at ?? Current).Line, (at ?? Current).Column);

    static readonly HashSet<string> TypeWords = ["void", "char", "int", "short", "long", "signed", "unsigned"];
    static readonly HashSet<string> Qualifiers = ["const", "volatile", "static", "extern", "__zp"];

    bool StartsDeclaration(Token token) => token.Kind == TokenKind.Keyword && (TypeWords.Contains(token.Text) || Qualifiers.Contains(token.Text));

    TranslationUnit Unit()
    {
        var globals = new List<Variable>();
        var functions = new List<Function>();
        while (Current.Kind != TokenKind.End)
        {
            if (Accept(";")) continue;
            if (!StartsDeclaration(Current)) throw Error($"Expected a declaration instead of {Current}");
            var start = Current;
            var (baseType, isStatic, isExtern, zeroPage) = Specifiers();
            var (name, type, nameToken) = Declarator(baseType);
            if (Current.Is("("))
            {
                functions.Add(FunctionRest(name, type, isStatic, nameToken));
                continue;
            }
            while (true)
            {
                globals.Add(VariableRest(name, type, isStatic, isExtern, zeroPage, nameToken));
                if (!Accept(",")) break;
                (name, type, nameToken) = Declarator(baseType);
            }
            Expect(";");
        }
        return new TranslationUnit(globals, functions);
    }

    // static, extern, const, volatile and __zp, then the type words: "unsigned char", "int", "signed char", ...
    (CType Type, bool Static, bool Extern, bool ZeroPage) Specifiers()
    {
        bool isStatic = false, isExtern = false, zeroPage = false;
        var words = new List<string>();
        var start = Current;
        while (Current.Kind == TokenKind.Keyword && (TypeWords.Contains(Current.Text) || Qualifiers.Contains(Current.Text)))
        {
            string word = Take().Text;
            switch (word)
            {
                case "static": isStatic = true; break;
                case "extern": isExtern = true; break;
                case "__zp": zeroPage = true; break;
                case "const" or "volatile": break;
                default: words.Add(word); break;
            }
        }
        return (TypeOf(words, start), isStatic, isExtern, zeroPage);
    }

    CType TypeOf(List<string> words, Token at)
    {
        if (words.Contains("long")) throw Error("long is not supported: int is 16 bits, char 8", at);
        bool isUnsigned = words.Contains("unsigned"), isSigned = words.Contains("signed");
        if (words.Contains("void")) return CType.Void;
        if (words.Contains("char")) return isSigned ? CType.SignedChar : CType.Char;
        if (words.Contains("int") || words.Contains("short") || isUnsigned || isSigned) return isUnsigned ? CType.UnsignedInt : CType.Int;
        throw Error("Expected a type", at);
    }

    // *name, name[10], name[]
    (string Name, CType Type, Token Token) Declarator(CType type)
    {
        while (Accept("*"))
        {
            while (Current.Is("const") || Current.Is("volatile")) Take();
            type = CType.PointerTo(type);
        }
        var token = Current;
        string name = ExpectIdentifier();
        if (Accept("["))
        {
            int count = Current.Is("]") ? -1 : ConstantNumber();
            Expect("]");
            type = CType.ArrayOf(type, count);
        }
        return (name, type, token);
    }

    int ConstantNumber()
    {
        var expression = Expression();
        return Constants.Evaluate(expression) ?? throw Error("Expected a constant", Current);
    }

    Variable VariableRest(string name, CType type, bool isStatic, bool isExtern, bool zeroPage, Token at)
    {
        Expression? initializer = null;
        List<Expression>? initializers = null;
        if (Accept("="))
        {
            if (Accept("{"))
            {
                initializers = [];
                while (!Current.Is("}"))
                {
                    initializers.Add(Assignment());
                    if (!Accept(",")) break;
                }
                Expect("}");
            }
            else
            {
                initializer = Assignment();
            }
        }
        if (type.Kind == CTypeKind.Array && type.Count < 0)
        {
            int count = initializers?.Count ?? (initializer is StringExpression s ? s.Text.Length + 1 : -1);
            if (count < 0) throw Error($"The size of {name} is missing", at);
            type = type with { Count = count };
        }
        if (type.Kind == CTypeKind.Void) throw Error($"{name} cannot be void", at);
        return new Variable(name, type, isStatic, isExtern, zeroPage, initializer, initializers, at.Line, at.Column);
    }

    Function FunctionRest(string name, CType returnType, bool isStatic, Token at)
    {
        Expect("(");
        var parameters = new List<Variable>();
        if (Current.Is("void") && Next().Is(")")) Take();
        while (!Current.Is(")"))
        {
            var (type, _, _, zeroPage) = Specifiers();
            var (parameter, parameterType, token) = Declarator(type);
            if (parameterType.Kind == CTypeKind.Array) parameterType = CType.PointerTo(parameterType.Target!);
            parameters.Add(new Variable(parameter, parameterType, false, false, zeroPage, null, null, token.Line, token.Column));
            if (!Accept(",")) break;
        }
        Expect(")");
        if (Accept(";")) return new Function(name, returnType, parameters, null, isStatic, at.Line, at.Column);
        return new Function(name, returnType, parameters, Block(), isStatic, at.Line, at.Column);
    }

    BlockStatement Block()
    {
        var start = Expect("{");
        var statements = new List<Statement>();
        while (!Current.Is("}"))
        {
            if (Current.Kind == TokenKind.End) throw Error("Expected '}' before the end of the file", start);
            statements.AddRange(StatementOrDeclarations());
        }
        Expect("}");
        return new BlockStatement(statements, start.Line, start.Column);
    }

    IEnumerable<Statement> StatementOrDeclarations()
    {
        if (!StartsDeclaration(Current)) return [Statement()];
        var (baseType, isStatic, isExtern, zeroPage) = Specifiers();
        var declarations = new List<Statement>();
        while (true)
        {
            var (name, type, token) = Declarator(baseType);
            declarations.Add(new DeclarationStatement(VariableRest(name, type, isStatic, isExtern, zeroPage, token), token.Line, token.Column));
            if (!Accept(",")) break;
        }
        Expect(";");
        return declarations;
    }

    Statement Statement()
    {
        var start = Current;
        if (Current.Is("{")) return Block();
        if (Accept(";")) return new EmptyStatement(start.Line, start.Column);
        if (Accept("if"))
        {
            Expect("(");
            var condition = Expression();
            Expect(")");
            var then = Statement();
            Statement? otherwise = Accept("else") ? Statement() : null;
            return new IfStatement(condition, then, otherwise, start.Line, start.Column);
        }
        if (Accept("while"))
        {
            Expect("(");
            var condition = Expression();
            Expect(")");
            return new WhileStatement(condition, Statement(), start.Line, start.Column);
        }
        if (Accept("do"))
        {
            var body = Statement();
            Expect("while");
            Expect("(");
            var condition = Expression();
            Expect(")");
            Expect(";");
            return new DoStatement(body, condition, start.Line, start.Column);
        }
        if (Accept("for"))
        {
            Expect("(");
            Statement? init = null;
            if (StartsDeclaration(Current))
            {
                var declarations = StatementOrDeclarations().ToList();   // reads the ';' too
                init = declarations.Count == 1 ? declarations[0] : new BlockStatement(declarations, start.Line, start.Column);
            }
            else
            {
                if (!Current.Is(";")) init = new ExpressionStatement(Expression(), start.Line, start.Column);
                Expect(";");
            }
            Expression? condition = Current.Is(";") ? null : Expression();
            Expect(";");
            Expression? step = Current.Is(")") ? null : Expression();
            Expect(")");
            return new ForStatement(init, condition, step, Statement(), start.Line, start.Column);
        }
        if (Accept("return"))
        {
            Expression? value = Current.Is(";") ? null : Expression();
            Expect(";");
            return new ReturnStatement(value, start.Line, start.Column);
        }
        if (Accept("break")) { Expect(";"); return new BreakStatement(start.Line, start.Column); }
        if (Accept("continue")) { Expect(";"); return new ContinueStatement(start.Line, start.Column); }
        if (Accept("__asm"))
        {
            // __asm("LDA #1\n STA $D020"); one string, or several in a row
            Expect("(");
            var text = new System.Text.StringBuilder();
            while (Current.Kind == TokenKind.String) text.Append(Take().Text);
            Expect(")");
            Expect(";");
            return new AsmStatement(text.ToString(), start.Line, start.Column);
        }
        var expression = Expression();
        Expect(";");
        return new ExpressionStatement(expression, start.Line, start.Column);
    }

    // Expressions, from the lowest precedence: the comma is not an operator here (only in calls and for)
    Expression Expression() => Assignment();

    static readonly HashSet<string> AssignOperators = ["=", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "<<=", ">>="];

    Expression Assignment()
    {
        var left = Conditional();
        if (Current.Kind == TokenKind.Punctuator && AssignOperators.Contains(Current.Text))
        {
            var op = Take();
            return new AssignExpression(op.Text, left, Assignment(), op.Line, op.Column);
        }
        return left;
    }

    Expression Conditional()
    {
        var condition = Binary(0);
        if (!Current.Is("?")) return condition;
        var op = Take();
        var then = Expression();
        Expect(":");
        return new ConditionalExpression(condition, then, Conditional(), op.Line, op.Column);
    }

    static readonly string[][] Levels =
    [
        ["||"], ["&&"], ["|"], ["^"], ["&"], ["==", "!="], ["<", ">", "<=", ">="], ["<<", ">>"], ["+", "-"], ["*", "/", "%"],
    ];

    Expression Binary(int level)
    {
        if (level == Levels.Length) return Unary();
        var left = Binary(level + 1);
        while (Current.Kind == TokenKind.Punctuator && Levels[level].Contains(Current.Text))
        {
            var op = Take();
            left = new BinaryExpression(op.Text, left, Binary(level + 1), op.Line, op.Column);
        }
        return left;
    }

    Expression Unary()
    {
        var start = Current;
        if (Current.Kind == TokenKind.Punctuator && Current.Text is "-" or "+" or "!" or "~" or "*" or "&" or "++" or "--")
        {
            Take();
            var operand = Unary();
            return start.Text == "+" ? operand : new UnaryExpression(start.Text, operand, start.Line, start.Column);
        }
        if (Accept("sizeof"))
        {
            if (Current.Is("(") && StartsDeclaration(Next()))
            {
                Take();
                var type = TypeName();
                Expect(")");
                return new SizeofExpression(type, start.Line, start.Column);
            }
            return new UnaryExpression("sizeof", Unary(), start.Line, start.Column);
        }
        if (Current.Is("(") && StartsDeclaration(Next()))
        {
            Take();
            var type = TypeName();
            Expect(")");
            return new CastExpression(type, Unary(), start.Line, start.Column);
        }
        return Postfix(Primary());
    }

    // A type without a name: (unsigned char*), sizeof(int)
    CType TypeName()
    {
        var (type, _, _, _) = Specifiers();
        while (Accept("*"))
        {
            while (Current.Is("const") || Current.Is("volatile")) Take();
            type = CType.PointerTo(type);
        }
        return type;
    }

    Expression Postfix(Expression expression)
    {
        while (true)
        {
            var start = Current;
            if (Accept("["))
            {
                var index = Expression();
                Expect("]");
                expression = new IndexExpression(expression, index, start.Line, start.Column);
            }
            else if (Current.Is("("))
            {
                if (expression is not NameExpression function) throw Error("Only functions can be called, by their name");
                Take();
                var arguments = new List<Expression>();
                while (!Current.Is(")"))
                {
                    arguments.Add(Assignment());
                    if (!Accept(",")) break;
                }
                Expect(")");
                expression = new CallExpression(function.Name, arguments, function.Line, function.Column);
            }
            else if (Current.Is("++") || Current.Is("--"))
            {
                expression = new PostfixExpression(Take().Text, expression, start.Line, start.Column);
            }
            else
            {
                return expression;
            }
        }
    }

    Expression Primary()
    {
        var token = Current;
        switch (token.Kind)
        {
            case TokenKind.Number:
            case TokenKind.Character:
                Take();
                return new NumberExpression(token.Value, token.Line, token.Column);
            case TokenKind.String:
            {
                var text = new System.Text.StringBuilder();
                while (Current.Kind == TokenKind.String) text.Append(Take().Text);   // "a" "b" is "ab"
                return new StringExpression(text.ToString(), token.Line, token.Column);
            }
            case TokenKind.Identifier:
                Take();
                return new NameExpression(token.Text, token.Line, token.Column);
        }
        if (Accept("("))
        {
            var inner = Expression();
            Expect(")");
            return inner;
        }
        throw Error($"Expected an expression instead of {token}");
    }
}

// Expressions computed while translating: array sizes, initializers, constant operands
public static class Constants
{
    public static int? Evaluate(Expression expression) => expression switch
    {
        NumberExpression n => n.Value,
        UnaryExpression { Operator: "-" } u => Evaluate(u.Operand) is int v ? -v : null,
        UnaryExpression { Operator: "~" } u => Evaluate(u.Operand) is int v ? ~v : null,
        UnaryExpression { Operator: "!" } u => Evaluate(u.Operand) is int v ? (v == 0 ? 1 : 0) : null,
        CastExpression c => Evaluate(c.Operand) is int v ? Truncate(v, c.Type) : null,
        SizeofExpression s => s.Type.Size,
        BinaryExpression b when Evaluate(b.Left) is int l && Evaluate(b.Right) is int r => b.Operator switch
        {
            "+" => l + r, "-" => l - r, "*" => l * r,
            "/" when r != 0 => l / r, "%" when r != 0 => l % r,
            "&" => l & r, "|" => l | r, "^" => l ^ r, "<<" => l << r, ">>" => l >> r,
            "==" => l == r ? 1 : 0, "!=" => l != r ? 1 : 0, "<" => l < r ? 1 : 0, ">" => l > r ? 1 : 0,
            "<=" => l <= r ? 1 : 0, ">=" => l >= r ? 1 : 0, "&&" => l != 0 && r != 0 ? 1 : 0, "||" => l != 0 || r != 0 ? 1 : 0,
            _ => null,
        },
        ConditionalExpression c when Evaluate(c.Condition) is int v => Evaluate(v != 0 ? c.Then : c.Else),
        _ => null,
    };

    static int Truncate(int value, CType type) => type.Kind switch
    {
        CTypeKind.Char => type.Signed ? (sbyte)value : (byte)value,
        CTypeKind.Int => type.Signed ? (short)value : (ushort)value,
        CTypeKind.Pointer => (ushort)value,
        _ => value,
    };
}
