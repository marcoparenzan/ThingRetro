namespace Thing64.C;

// The types of the 6502's C: char (8 bits, unsigned unless "signed char"), int (16 bits, signed unless "unsigned"),
// pointers (16 bits), arrays, void. The arithmetic of two 8-bit values stays on 8 bits, as on the 6502
public sealed record CType(CTypeKind Kind, bool Signed = false, CType? Target = null, int Count = 0)
{
    public static readonly CType Void = new(CTypeKind.Void);
    public static readonly CType Char = new(CTypeKind.Char);
    public static readonly CType SignedChar = new(CTypeKind.Char, Signed: true);
    public static readonly CType Int = new(CTypeKind.Int, Signed: true);
    public static readonly CType UnsignedInt = new(CTypeKind.Int);

    public static CType PointerTo(CType target) => new(CTypeKind.Pointer, Target: target);
    public static CType ArrayOf(CType element, int count) => new(CTypeKind.Array, Target: element, Count: count);

    public int Size => Kind switch
    {
        CTypeKind.Char => 1,
        CTypeKind.Int or CTypeKind.Pointer => 2,
        CTypeKind.Array => Target!.Size * Count,
        _ => 0,
    };

    public bool IsInteger => Kind is CTypeKind.Char or CTypeKind.Int;
    public bool IsPointerLike => Kind is CTypeKind.Pointer or CTypeKind.Array;
    public bool IsScalar => IsInteger || Kind == CTypeKind.Pointer;
    public bool Wide => Size == 2;   // carried in A (low byte) and X (high byte)

    public override string ToString() => Kind switch
    {
        CTypeKind.Char => Signed ? "signed char" : "char",
        CTypeKind.Int => Signed ? "int" : "unsigned int",
        CTypeKind.Pointer => Target + "*",
        CTypeKind.Array => $"{Target}[{Count}]",
        _ => "void",
    };
}

public enum CTypeKind { Void, Char, Int, Pointer, Array }

// The syntax tree. Every node remembers where it starts in the source, for the error messages
public abstract record Node(int Line, int Column);

public abstract record Expression(int Line, int Column) : Node(Line, Column);
public sealed record NumberExpression(int Value, int Line, int Column) : Expression(Line, Column);
public sealed record StringExpression(string Text, int Line, int Column) : Expression(Line, Column);
public sealed record NameExpression(string Name, int Line, int Column) : Expression(Line, Column);
public sealed record UnaryExpression(string Operator, Expression Operand, int Line, int Column) : Expression(Line, Column);
public sealed record PostfixExpression(string Operator, Expression Operand, int Line, int Column) : Expression(Line, Column);
public sealed record BinaryExpression(string Operator, Expression Left, Expression Right, int Line, int Column) : Expression(Line, Column);
public sealed record AssignExpression(string Operator, Expression Target, Expression Value, int Line, int Column) : Expression(Line, Column);
public sealed record ConditionalExpression(Expression Condition, Expression Then, Expression Else, int Line, int Column) : Expression(Line, Column);
public sealed record CallExpression(string Function, IReadOnlyList<Expression> Arguments, int Line, int Column) : Expression(Line, Column);
public sealed record IndexExpression(Expression Array, Expression Index, int Line, int Column) : Expression(Line, Column);
public sealed record CastExpression(CType Type, Expression Operand, int Line, int Column) : Expression(Line, Column);
public sealed record SizeofExpression(CType Type, int Line, int Column) : Expression(Line, Column);

public abstract record Statement(int Line, int Column) : Node(Line, Column);
public sealed record BlockStatement(IReadOnlyList<Statement> Statements, int Line, int Column) : Statement(Line, Column);
public sealed record ExpressionStatement(Expression Expression, int Line, int Column) : Statement(Line, Column);
public sealed record DeclarationStatement(Variable Variable, int Line, int Column) : Statement(Line, Column);
public sealed record IfStatement(Expression Condition, Statement Then, Statement? Else, int Line, int Column) : Statement(Line, Column);
public sealed record WhileStatement(Expression Condition, Statement Body, int Line, int Column) : Statement(Line, Column);
public sealed record DoStatement(Statement Body, Expression Condition, int Line, int Column) : Statement(Line, Column);
public sealed record ForStatement(Statement? Init, Expression? Condition, Expression? Step, Statement Body, int Line, int Column) : Statement(Line, Column);
public sealed record ReturnStatement(Expression? Value, int Line, int Column) : Statement(Line, Column);
public sealed record BreakStatement(int Line, int Column) : Statement(Line, Column);
public sealed record ContinueStatement(int Line, int Column) : Statement(Line, Column);
public sealed record AsmStatement(string Text, int Line, int Column) : Statement(Line, Column);
public sealed record EmptyStatement(int Line, int Column) : Statement(Line, Column);

// A variable: global, local or parameter. Initializer: one expression, or a list for an array. Equate: not a variable
// but a constant of the assembly (__equ char PLAYER_COLOR;), used as an immediate value: LDA #PLAYER_COLOR
public sealed record Variable(string Name, CType Type, bool Static, bool Extern, bool ZeroPage, Expression? Initializer,
                              IReadOnlyList<Expression>? Initializers, int Line, int Column, bool Equate = false);

public sealed record Function(string Name, CType Return, IReadOnlyList<Variable> Parameters, BlockStatement? Body, bool Static,
                              int Line, int Column);

public sealed record TranslationUnit(IReadOnlyList<Variable> Globals, IReadOnlyList<Function> Functions);
