using System.Globalization;
using System.Text;

namespace Thing64.C;

// From the syntax tree to assembly for Thing64.Asm (ACME syntax).
//
// The conventions, the same in every module, so that C and assembly can call each other:
//   - a C function or global variable has the same name in assembly: JSR player_update, LDA score
//     (static ones get the module's name in front: player__speed)
//   - parameters and local variables have fixed addresses, function__name (no stack: no recursion)
//   - the caller writes the arguments into the parameters, then JSR; the result comes back in A (8 bits) or in
//     A (low byte) and X (high byte), 16 bits
//   - an extern function written in assembly finds its arguments at function__parameter, which it defines
//   - pointers are followed through module__ptr, a pair of zero page bytes ($FB/$FC)
// The output is meant to be read: each C statement appears as a comment above its instructions
public sealed partial class Generator
{
    sealed record Symbol(string Name, CType Type, string Label, bool IsFunction = false);

    sealed class FunctionContext(Function function, string label)
    {
        public Function Function { get; } = function;
        public string Label { get; } = label;
        public string ReturnLabel { get; } = "@return";
        public int Labels;
        public int Temps, MaxTemps;
        public readonly Stack<string> Breaks = new(), Continues = new();
        public readonly List<(string Label, CType Type, Variable? Variable)> Storage = [];
        public readonly HashSet<string> LocalNames = [];
        public readonly HashSet<string> Calls = [];
    }

    readonly TranslationUnit unit;
    readonly string[] sourceLines;
    readonly string module;
    readonly StringBuilder code = new();
    readonly StringBuilder data = new();
    readonly Dictionary<string, Symbol> globals = [];
    readonly Dictionary<string, Function> functions = [];
    readonly List<Dictionary<string, Symbol>> scopes = [];
    readonly HashSet<string> runtime = [];
    readonly Dictionary<string, HashSet<string>> callGraph = [];
    FunctionContext? context;
    int strings;
    int lastCommentLine;

    Generator(TranslationUnit unit, string source, string module)
    {
        this.unit = unit;
        sourceLines = source.Replace("\r\n", "\n").Split('\n');
        this.module = module;
    }

    // C source to assembly. moduleName (the file's name, without extension) prefixes the module's own labels
    public static string Translate(string source, string moduleName)
    {
        string module = new string(moduleName.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '_').ToArray());
        if (module.Length == 0 || char.IsAsciiDigit(module[0])) module = "c_" + module;
        return new Generator(Parser.Parse(source), source, module).Run();
    }

    string Ptr => module + "__ptr";

    string Run()
    {
        foreach (var function in unit.Functions)
        {
            if (functions.TryGetValue(function.Name, out var earlier) && earlier.Body is not null && function.Body is not null)
                throw new CException($"{function.Name} is defined twice", function.Line, function.Column);
            if (!functions.TryGetValue(function.Name, out earlier) || function.Body is not null) functions[function.Name] = function;
            globals[function.Name] = new Symbol(function.Name, function.Return, FunctionLabel(function), IsFunction: true);
        }
        foreach (var variable in unit.Globals)
        {
            if (globals.ContainsKey(variable.Name) && !variable.Extern) throw new CException($"{variable.Name} is defined twice", variable.Line, variable.Column);
            if (variable.ZeroPage) throw new CException("__zp variables are not supported yet", variable.Line, variable.Column);
            globals[variable.Name] = new Symbol(variable.Name, variable.Type, variable.Static ? $"{module}__{variable.Name}" : variable.Name);
        }

        foreach (var function in unit.Functions.Where(f => f.Body is not null)) GenerateFunction(function);
        CheckRecursion();

        var output = new StringBuilder();
        output.AppendLine("; ----------------------------------------------------------------------------------------------------------");
        output.AppendLine($"; {module}: translated from C by Thing64.C. Do not edit: it is written again at every build.");
        output.AppendLine("; Parameters and locals have fixed addresses (function__name); results come back in A, or A/X for 16 bits.");
        output.AppendLine("; ----------------------------------------------------------------------------------------------------------");
        output.AppendLine();
        output.AppendLine($"{Ptr} = $FB                          ; zero page pair to follow pointers");
        output.AppendLine();
        output.Append(code);
        foreach (string routine in runtime.Order()) output.Append(Runtime.Routine(routine, module));
        if (runtime.Count > 0) output.Append(Runtime.Data(module));
        GenerateGlobals();
        if (data.Length > 0)
        {
            output.AppendLine();
            output.AppendLine("; ---------------------------------------------------------------------------------------------------- data");
            output.Append(data);
        }
        return output.ToString();
    }

    string FunctionLabel(Function function) => function.Static ? $"{module}__{function.Name}" : function.Name;

    // ------------------------------------------------------------------------------------------------- output

    void Emit(string instruction) => code.AppendLine("            " + instruction);

    void Label(string label) => code.AppendLine(label + ":");

    string NewLabel(string hint) => $"@{hint}{++context!.Labels}";

    // The C line as a comment, once per line
    void Comment(int line)
    {
        if (line == lastCommentLine || line < 1 || line > sourceLines.Length) return;
        lastCommentLine = line;
        string text = sourceLines[line - 1].Trim();
        if (text.Length > 0) code.AppendLine($"            ; {line}: {text}");
    }

    static string Hex(int value) => "$" + (value & 0xFFFF).ToString(value is >= 0 and <= 0xFF ? "X2" : "X4", CultureInfo.InvariantCulture);

    // ---------------------------------------------------------------------------------------------- functions

    void GenerateFunction(Function function)
    {
        string label = FunctionLabel(function);
        context = new FunctionContext(function, label);
        callGraph[function.Name] = context.Calls;
        scopes.Clear();
        scopes.Add([]);

        code.AppendLine();
        code.AppendLine($"; {function.Return} {function.Name}({string.Join(", ", function.Parameters.Select(p => $"{p.Type} {p.Name}"))})");
        foreach (var parameter in function.Parameters) Declare(parameter, isParameter: true);
        Label(label);
        lastCommentLine = function.Line;

        foreach (var statement in function.Body!.Statements) GenerateStatement(statement);
        Label(context.ReturnLabel);
        Emit("RTS");

        // The function's storage: parameters, locals, temporaries
        foreach (var (storage, type, variable) in context.Storage)
            data.AppendLine($"{storage}:".PadRight(28) + Storage(type, variable));
        for (int t = 0; t < context.MaxTemps; t++)
            data.AppendLine($"{label}__t{t}:".PadRight(28) + "!word 0");
        context = null;
    }

    // A parameter or local: its label in the function's storage
    Symbol Declare(Variable variable, bool isParameter = false)
    {
        if (variable.ZeroPage) throw new CException("__zp variables are not supported yet", variable.Line, variable.Column);
        if (scopes[^1].ContainsKey(variable.Name)) throw new CException($"{variable.Name} is declared twice", variable.Line, variable.Column);
        string name = variable.Name;
        for (int n = 2; !context!.LocalNames.Add(name); n++) name = $"{variable.Name}_{n}";   // the same name in two blocks
        string storageLabel = $"{context.Label}__{name}";
        var symbol = new Symbol(variable.Name, variable.Type, storageLabel);
        scopes[^1][variable.Name] = symbol;
        // Static locals keep their initial value in the data; the others are set where they are declared
        context.Storage.Add((storageLabel, variable.Type, variable.Static ? variable : null));
        return symbol;
    }

    Symbol? Lookup(string name)
    {
        for (int i = scopes.Count - 1; i >= 0; i--)
            if (scopes[i].TryGetValue(name, out var symbol)) return symbol;
        return globals.GetValueOrDefault(name);
    }

    Symbol Find(string name, Node at) =>
        Lookup(name) ?? throw new CException($"{name} is not declared", at.Line, at.Column);

    // ---------------------------------------------------------------------------------------------- statements

    void GenerateStatement(Statement statement)
    {
        switch (statement)
        {
            case BlockStatement block:
                scopes.Add([]);
                foreach (var inner in block.Statements) GenerateStatement(inner);
                scopes.RemoveAt(scopes.Count - 1);
                break;

            case ExpressionStatement e:
                Comment(e.Line);
                GenerateDiscarded(e.Expression);
                break;

            case DeclarationStatement d:
            {
                var variable = d.Variable;
                if (variable.Extern) throw new CException("extern belongs outside the functions", d.Line, d.Column);
                var symbol = Declare(variable);
                if (variable.Static) break;
                if (variable.Initializer is { } value)
                {
                    Comment(d.Line);
                    if (variable.Type.Kind == CTypeKind.Array)
                        throw new CException("A local array is initialized with { ... } or made static", d.Line, d.Column);
                    Store(new NameExpression(variable.Name, d.Line, d.Column), value);
                }
                else if (variable.Initializers is { } values)
                {
                    Comment(d.Line);
                    if (variable.Type.Kind != CTypeKind.Array) throw new CException("{ ... } initializes an array", d.Line, d.Column);
                    for (int i = 0; i < values.Count; i++)
                        Store(new IndexExpression(new NameExpression(variable.Name, d.Line, d.Column), new NumberExpression(i, d.Line, d.Column), d.Line, d.Column), values[i]);
                }
                break;
            }

            case IfStatement s:
            {
                Comment(s.Line);
                string otherwise = NewLabel("else"), end = NewLabel("endif");
                Branch(s.Condition, false, s.Else is null ? end : otherwise);
                GenerateStatement(s.Then);
                if (s.Else is not null)
                {
                    Emit($"JMP {end}");
                    Label(otherwise);
                    GenerateStatement(s.Else);
                }
                Label(end);
                break;
            }

            case WhileStatement s:
            {
                Comment(s.Line);
                string loop = NewLabel("while"), end = NewLabel("wend");
                Label(loop);
                Branch(s.Condition, false, end);
                Loop(s.Body, end, loop);
                Emit($"JMP {loop}");
                Label(end);
                break;
            }

            case DoStatement s:
            {
                Comment(s.Line);
                string loop = NewLabel("do"), test = NewLabel("dotest"), end = NewLabel("dend");
                Label(loop);
                Loop(s.Body, end, test);
                Label(test);
                Comment(s.Condition.Line);
                Branch(s.Condition, true, loop);
                Label(end);
                break;
            }

            case ForStatement s:
            {
                Comment(s.Line);
                scopes.Add([]);
                if (s.Init is not null) GenerateStatement(s.Init);
                string loop = NewLabel("for"), step = NewLabel("next"), end = NewLabel("fend");
                Label(loop);
                if (s.Condition is not null) Branch(s.Condition, false, end);
                Loop(s.Body, end, step);
                Label(step);
                if (s.Step is not null) GenerateDiscarded(s.Step);
                Emit($"JMP {loop}");
                Label(end);
                scopes.RemoveAt(scopes.Count - 1);
                break;
            }

            case ReturnStatement s:
            {
                Comment(s.Line);
                var returns = context!.Function.Return;
                if (s.Value is not null)
                {
                    if (returns.Kind == CTypeKind.Void) throw new CException($"{context.Function.Name} is void: it returns no value", s.Line, s.Column);
                    GenerateAs(s.Value, returns);
                }
                else if (returns.Kind != CTypeKind.Void)
                {
                    throw new CException($"{context.Function.Name} returns a {returns}", s.Line, s.Column);
                }
                Emit($"JMP {context.ReturnLabel}");
                break;
            }

            case BreakStatement s:
                if (context!.Breaks.Count == 0) throw new CException("break outside a loop", s.Line, s.Column);
                Comment(s.Line);
                Emit($"JMP {context.Breaks.Peek()}");
                break;

            case ContinueStatement s:
                if (context!.Continues.Count == 0) throw new CException("continue outside a loop", s.Line, s.Column);
                Comment(s.Line);
                Emit($"JMP {context.Continues.Peek()}");
                break;

            case AsmStatement s:
                Comment(s.Line);
                foreach (string line in s.Text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0))
                    code.AppendLine(line.EndsWith(':') ? line : "            " + line);
                break;

            case EmptyStatement:
                break;
        }
    }

    void Loop(Statement body, string breakLabel, string continueLabel)
    {
        context!.Breaks.Push(breakLabel);
        context.Continues.Push(continueLabel);
        GenerateStatement(body);
        context.Breaks.Pop();
        context.Continues.Pop();
    }

    // ---------------------------------------------------------------------------------------------- data

    void GenerateGlobals()
    {
        foreach (var variable in unit.Globals.Where(v => !v.Extern))
        {
            string label = globals[variable.Name].Label;
            data.AppendLine($"{label}:".PadRight(28) + Storage(variable.Type, variable));
        }
    }

    // The directive that reserves a variable, with its initial value
    string Storage(CType type, Variable? variable)
    {
        if (type.Kind == CTypeKind.Array)
        {
            var element = type.Target!;
            if (element.Kind == CTypeKind.Array) throw new CException("Arrays of arrays are not supported yet", variable?.Line ?? 0, variable?.Column ?? 0);
            var values = new List<string>();
            if (variable?.Initializer is StringExpression text && element.Size == 1)
                values.AddRange(text.Text.Select(c => Hex(Lexer.Petscii(c))).Append("$00"));
            else if (variable?.Initializers is { } list)
                values.AddRange(list.Select(v => InitialValue(v, element)));
            if (values.Count > type.Count) throw new CException($"{variable!.Name} has {type.Count} elements, not {values.Count}", variable.Line, variable.Column);
            string directive = element.Size == 1 ? "!byte" : "!word";
            if (values.Count == 0) return $"!fill {type.Size}, 0";
            string written = $"{directive} {string.Join(", ", values)}";
            int rest = (type.Count - values.Count) * element.Size;
            return rest > 0 ? written + $"\n                            !fill {rest}, 0" : written;
        }
        string value = variable?.Initializer is { } initial ? InitialValue(initial, type) : "0";
        return (type.Size == 1 ? "!byte " : "!word ") + value;
    }

    // A value known before the program runs: a constant, a string, the address of a global
    string InitialValue(Expression expression, CType type)
    {
        if (Constants.Evaluate(expression) is int value) return Hex(type.Size == 1 ? value & 0xFF : value & 0xFFFF);
        if (expression is StringExpression text) return StringLabel(text.Text);
        if (expression is UnaryExpression { Operator: "&", Operand: NameExpression name } && globals.TryGetValue(name.Name, out var symbol)) return symbol.Label;
        if (expression is NameExpression array && globals.TryGetValue(array.Name, out var arraySymbol) && arraySymbol.Type.Kind == CTypeKind.Array) return arraySymbol.Label;
        throw new CException("The initial value of a global must be known before the program runs", expression.Line, expression.Column);
    }

    // A string in the data, PETSCII, ending with 0
    string StringLabel(string text)
    {
        string label = $"{module}__string{++strings}";
        data.AppendLine($"{label}:".PadRight(28) + "!byte " + string.Join(", ", text.Select(c => Hex(Lexer.Petscii(c))).Append("$00")));
        return label;
    }

    // ---------------------------------------------------------------------------------------------- recursion

    void CheckRecursion()
    {
        foreach (string start in callGraph.Keys)
        {
            var path = new Stack<string>();
            if (Cycle(start, start, path, []))
            {
                var function = functions[start];
                throw new CException($"{start} calls itself ({string.Join(" -> ", path.Reverse().Prepend(start))}): recursion is not supported, " +
                                     "parameters and locals have fixed addresses", function.Line, function.Column);
            }
        }
    }

    bool Cycle(string start, string current, Stack<string> path, HashSet<string> seen)
    {
        if (!callGraph.TryGetValue(current, out var calls)) return false;
        foreach (string callee in calls)
        {
            path.Push(callee);
            if (callee == start || (seen.Add(callee) && Cycle(start, callee, path, seen))) return true;
            path.Pop();
        }
        return false;
    }
}
