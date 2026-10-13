using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Fake64.Asm;

public static partial class Assembler
{
    // What a pass learns, handed to the next one: symbol values (also forward references) and anonymous labels
    sealed class PassState
    {
        public readonly Dictionary<string, Num> Symbols = new(StringComparer.Ordinal);
        public readonly List<(int Zone, string Name, int Sequence, int Value)> Anonymous = [];

        public bool SameAs(PassState other) =>
            Symbols.Count == other.Symbols.Count &&
            Symbols.All(s => other.Symbols.TryGetValue(s.Key, out var v) && v == s.Value) &&
            Anonymous.SequenceEqual(other.Anonymous);
    }

    sealed record Macro(string Name, IReadOnlyList<string> Parameters, List<Node> Body);

    enum Conversion { Raw, Petscii, Screen }

    sealed partial class Pass(SourceFiles files, PassState previous, bool final)
    {
        public readonly PassState State = new();
        readonly HashSet<string> redefinable = new(StringComparer.Ordinal);
        readonly Dictionary<string, Macro> macros = new(StringComparer.Ordinal);
        readonly byte[] memory = new byte[0x10000];
        readonly bool[] written = new bool[0x10000];
        int lowest = int.MaxValue, highest = -1;
        int? firstOrigin;
        int pc = -1;
        int initmem;
        Conversion conversion = Conversion.Raw;

        // Scopes: zone for .locals and anonymous labels (new for each macro call, !zone and source file),
        // cheap scope for @locals (new after each global label and for each macro call)
        int zone, zones, cheap, cheaps, sequence;
        int macroDepth;
        Node location = new("", "", null, 0, "");

        public void Run(List<Node> nodes) => Execute(nodes);

        public AssemblerException Error(string message) =>
            new(location.File, location.Line, location.LineText, message, location.Path);

        public Result Result(string name)
        {
            if (firstOrigin is null) throw new AssemblerException(name, 1, "", "missing origin (* = ...)");
            var symbols = State.Symbols.Where(s => !s.Key.Contains('\u0001')).ToDictionary(s => s.Key, s => (int)s.Value.AsInt);
            if (highest < 0) return new Result((ushort)firstOrigin.Value, [], symbols);
            var bytes = new byte[highest - lowest + 1];
            for (int a = lowest; a <= highest; a++) bytes[a - lowest] = written[a] ? memory[a] : (byte)initmem;
            return new Result((ushort)lowest, bytes, symbols);
        }

        void Execute(List<Node> nodes)
        {
            for (int index = 0; index < nodes.Count; index++)
            {
                location = nodes[index];
                sequence++;
                Statement(nodes[index].Text, nodes, ref index);
            }
        }

        #region Statements

        void Statement(string text, List<Node> nodes, ref int index)
        {
            if (text.Length == 0) return;
            var node = nodes[index];

            // Anonymous label: "-", "--", "+", "++"... followed by a space or nothing
            if (text[0] is '-' or '+')
            {
                int run = 1;
                while (run < text.Length && text[run] == text[0]) run++;
                if (run == text.Length || char.IsWhiteSpace(text[run]))
                {
                    DefineAnonymous(text[..run]);
                    Statement(text[run..].Trim(), nodes, ref index);
                    return;
                }
            }

            if (text[0] == '*') { Origin(text[1..].Trim()); return; }
            if (text[0] == '+' && text.Length > 1 && IsSymbolStart(text[1])) { CallMacro(text[1..]); return; }

            // Directive (".byte", "!byte"); a "." word that is not a directive is a local label
            if (text[0] is '!' or '.')
            {
                int end = 1;
                while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_')) end++;
                string word = text[..end].ToLowerInvariant();
                if (text[0] == '!' || IsDotDirective(word))
                {
                    Directive(word, text[end..].Trim(), node, nodes, ref index);
                    return;
                }
            }

            if (text.Equals("else", StringComparison.OrdinalIgnoreCase)) throw Error("else without !if");

            // Symbol definition: NAME = value
            var assignment = AssignmentPattern().Match(text);
            if (assignment.Success)
            {
                var value = Evaluate(assignment.Groups[2].Value);
                if (value is Num v) Define(assignment.Groups[1].Value, v);
                else if (final) throw Error("undefined value");
                return;
            }

            // Instruction, or label (the colon has already split the statement) possibly followed by more
            int space = 0;
            while (space < text.Length && !char.IsWhiteSpace(text[space])) space++;
            string first = text[..space];
            string rest = text[space..].Trim();
            if (IsMnemonic(first)) { Instruction(first.ToUpperInvariant(), rest); return; }

            if (!IsSymbolStart(first[0]) || first.Skip(1).Any(c => !IsSymbolChar(c)))
                throw Error($"unknown instruction: {first}");
            if (rest.Length > 0 && !StartsStatement(rest))
                throw Error($"unknown instruction: {first}");

            DefineLabel(first);
            Statement(rest, nodes, ref index);
        }

        static bool StartsStatement(string text)
        {
            if (text[0] is '!' or '.' or '+' or '-' or '*') return true;
            int space = 0;
            while (space < text.Length && !char.IsWhiteSpace(text[space])) space++;
            return IsMnemonic(text[..space]);
        }

        [GeneratedRegex(@"^([A-Za-z_.@][\w]*)\s*=(?!=)\s*(.+)$")]
        private static partial Regex AssignmentPattern();

        static readonly HashSet<string> DotDirectives =
            [".byte", ".word", ".text", ".screen", ".fill", ".basic", ".initmem", ".source", ".include", ".binary"];

        static bool IsDotDirective(string word) => DotDirectives.Contains(word);

        void Origin(string text)
        {
            if (!text.StartsWith('=')) throw Error("expected '* = address'");
            string expression = text[1..];
            int comma = TopLevelComma(expression);
            if (comma >= 0) expression = expression[..comma];   // ACME options (overlay, invisible) are not needed here
            int address = (int)(Evaluate(expression) ?? throw Error("the program counter must be known")).AsInt;
            if (address is < 0 or > 0xFFFF) throw Error($"address out of range: ${address:X}");
            pc = address;
            firstOrigin ??= address;
        }

        #endregion

        #region Symbols

        // Symbol names in the table: globals as they are, locals with their scope in front
        string Key(string name) => name[0] switch
        {
            '.' => $"{zone}\u0001{name}",
            '@' => $"{cheap}\u0001{name}",
            _ => name,
        };

        void Define(string name, Num value, bool canRedefine = false)
        {
            string key = Key(name);
            if (State.Symbols.ContainsKey(key) && !redefinable.Contains(key) && !canRedefine)
                throw Error($"symbol already defined: {name}");
            if (canRedefine) redefinable.Add(key);
            State.Symbols[key] = value;
        }

        void DefineLabel(string name)
        {
            Define(name, Num.Of(RequirePc()));
            if (name[0] is not ('.' or '@')) cheap = ++cheaps;   // a global label starts a new range for @locals
        }

        public Num? Symbol(string name)
        {
            string key = Key(name);
            if (State.Symbols.TryGetValue(key, out var value)) return value;
            if (previous.Symbols.TryGetValue(key, out var earlier)) return earlier;
            if (final) throw Error($"undefined symbol: {name}");
            return null;
        }

        bool IsDefined(string name) => State.Symbols.ContainsKey(Key(name)) || previous.Symbols.ContainsKey(Key(name));

        void DefineAnonymous(string name) => State.Anonymous.Add((zone, name, sequence, RequirePc()));

        // "-": the last definition before this statement; "+": the next one after it (known from the previous pass)
        public Num? Anonymous(string name)
        {
            if (name[0] == '-')
            {
                for (int k = State.Anonymous.Count - 1; k >= 0; k--)
                {
                    var a = State.Anonymous[k];
                    if (a.Zone == zone && a.Name == name && a.Sequence <= sequence) return Num.Of(a.Value);
                }
                if (final) throw Error($"no anonymous label '{name}' before this line");
                return null;
            }
            foreach (var a in previous.Anonymous)
                if (a.Zone == zone && a.Name == name && a.Sequence > sequence) return Num.Of(a.Value);
            if (final) throw Error($"no anonymous label '{name}' after this line");
            return null;
        }

        Num? Evaluate(string expression)
        {
            if (expression.Trim().Length == 0) throw Error("empty expression");
            return new ExpressionParser(this, expression.Trim()).Parse();
        }

        int Known(string expression, string what) =>
            (int)(Evaluate(expression) ?? throw Error($"{what} must be known in the first pass")).AsInt;

        #endregion

        #region Output

        public int RequirePc() => pc >= 0 ? pc : throw Error("missing origin (* = ...) before the code");

        void Emit(int value)
        {
            RequirePc();
            if (pc > 0xFFFF) throw Error("the program counter went past $FFFF");
            if (final)
            {
                memory[pc] = (byte)value;
                written[pc] = true;
                lowest = Math.Min(lowest, pc);
                highest = Math.Max(highest, pc);
            }
            pc++;
        }

        int Byte(Num? value, string expression)
        {
            if (value is not Num v) return final ? throw Error($"undefined value: {expression}") : 0;
            long n = v.AsInt;
            if (final && (n < -128 || n > 0xFF)) throw Error($"value does not fit in a byte: {expression}");
            return (int)(n & 0xFF);
        }

        long Value(Num? value, string expression, long min, long max)
        {
            if (value is not Num v) return final ? throw Error($"undefined value: {expression}") : 0;
            long n = v.AsInt;
            if (final && (n < min || n > max)) throw Error($"value out of range: {expression}");
            return n;
        }

        #endregion

        #region Directives

        void Directive(string word, string arguments, Node node, List<Node> nodes, ref int index)
        {
            bool dot = word[0] == '.';
            switch (word[1..])
            {
                case "byte" or "by" or "8" or "08":
                    foreach (var item in SplitArguments(arguments))
                    {
                        if (item.StartsWith('"') && item.Length != 3)
                            foreach (char c in Unquote(item)) Emit(dot ? Petscii(c) : Convert(c, Conversion.Raw));
                        else Emit(Byte(Evaluate(item), item));
                    }
                    break;
                case "word" or "wo" or "16" or "le16": Multibyte(arguments, 2, bigEndian: false); break;
                case "be16": Multibyte(arguments, 2, bigEndian: true); break;
                case "24" or "le24": Multibyte(arguments, 3, bigEndian: false); break;
                case "be24": Multibyte(arguments, 3, bigEndian: true); break;
                case "32" or "le32": Multibyte(arguments, 4, bigEndian: false); break;
                case "be32": Multibyte(arguments, 4, bigEndian: true); break;
                case "hex" or "h":
                    {
                        string digits = arguments.Replace(" ", "").Replace("\t", "");
                        if (digits.Length % 2 != 0) throw Error("!hex needs pairs of hex digits");
                        for (int k = 0; k < digits.Length; k += 2) Emit(System.Convert.ToByte(digits.Substring(k, 2), 16));
                        break;
                    }
                case "text" or "tx":
                    if (dot) foreach (char c in Unquote(arguments)) Emit(Petscii(c));
                    else Strings(arguments, conversion);
                    break;
                case "raw": Strings(arguments, Conversion.Raw); break;
                case "pet": Strings(arguments, Conversion.Petscii); break;
                case "scr": Strings(arguments, Conversion.Screen); break;
                case "scrxor":
                    {
                        var parts = SplitArguments(arguments);
                        int mask = Byte(Evaluate(parts[0]), parts[0]);
                        Strings(string.Join(",", parts.Skip(1)), Conversion.Screen, mask);
                        break;
                    }
                case "screen":
                    foreach (char c in Unquote(arguments)) Emit(ScreenCode(c));
                    break;
                case "convtab" or "ct":
                    {
                        var table = arguments.Trim().ToLowerInvariant() switch
                        {
                            "raw" => Conversion.Raw,
                            "pet" => Conversion.Petscii,
                            "scr" => Conversion.Screen,
                            _ => throw Error($"conversion table not supported: {arguments} (raw, pet, scr)"),
                        };
                        if (node.Block is null) conversion = table;
                        else
                        {
                            var saved = conversion;
                            conversion = table;
                            Execute(node.Block);
                            conversion = saved;
                        }
                        break;
                    }
                case "fill" or "fi":
                    {
                        var parts = SplitArguments(arguments);
                        int count = Known(parts[0], "the byte count of !fill");
                        int value = parts.Count > 1 ? Byte(Evaluate(parts[1]), parts[1]) : 0;
                        for (int k = 0; k < count; k++) Emit(value);
                        break;
                    }
                case "skip":
                    pc = RequirePc() + Known(arguments, "the byte count of !skip");
                    break;
                case "align":
                    {
                        var parts = SplitArguments(arguments);
                        int and = Known(parts[0], "the AND value of !align");
                        int equal = Known(parts[1], "the value of !align");
                        int value = parts.Count > 2 ? Byte(Evaluate(parts[2]), parts[2]) : 0xEA;
                        while ((RequirePc() & and) != equal) Emit(value);
                        break;
                    }
                case "basic": Basic(); break;
                case "initmem": initmem = Known(arguments, "!initmem") & 0xFF; break;
                case "source" or "src" or "include": Include(arguments, node); break;
                case "binary" or "bin" or "bi": Binary(arguments, node); break;
                case "zone" or "zn":
                    {
                        int saved = zone;
                        zone = ++zones;
                        if (node.Block is not null)
                        {
                            Execute(node.Block);
                            zone = saved;
                        }
                        break;
                    }
                case "if": If(Evaluate(arguments)?.IsTrue ?? (final ? throw Error("undefined condition") : false), node, nodes, ref index); break;
                case "ifdef": If(IsDefined(arguments.Trim()), node, nodes, ref index); break;
                case "ifndef": If(!IsDefined(arguments.Trim()), node, nodes, ref index); break;
                case "for": For(arguments, node); break;
                case "set":
                    {
                        var assignment = AssignmentPattern().Match(arguments);
                        if (!assignment.Success) throw Error("expected !set NAME = value");
                        if (Evaluate(assignment.Groups[2].Value) is Num v) Define(assignment.Groups[1].Value, v, canRedefine: true);
                        break;
                    }
                case "macro": DefineMacro(arguments, node); break;
                case "error" or "serious": throw Error(Message(arguments));
                case "warn" or "info" or "to" or "cpu" or "sl" or "symbollist": break;
                default: throw Error($"unknown directive: {word}");
            }
        }

        void Multibyte(string arguments, int size, bool bigEndian)
        {
            long min = -(1L << (size * 8 - 1)), max = (1L << (size * 8)) - 1;
            foreach (var item in SplitArguments(arguments))
            {
                long value = Value(Evaluate(item), item, min, max);
                for (int k = 0; k < size; k++)
                {
                    int shift = bigEndian ? (size - 1 - k) * 8 : k * 8;
                    Emit((int)(value >> shift & 0xFF));
                }
            }
        }

        // Strings in a conversion table; other items are values (not converted, as in ACME)
        void Strings(string arguments, Conversion table, int xor = 0)
        {
            foreach (var item in SplitArguments(arguments))
            {
                if (item.StartsWith('"')) foreach (char c in Unquote(item)) Emit(Convert(c, table) ^ xor);
                else Emit(Byte(Evaluate(item), item) ^ xor);
            }
        }

        // 10 SYS<address>: link, line number, SYS token ($9E), digits, end of line, end of program
        void Basic()
        {
            int start = RequirePc();
            int target = start + 12;
            string digits = target.ToString(CultureInfo.InvariantCulture);
            if (digits.Length != 4) throw Error(".basic needs a 4-digit address (e.g. * = $0801)");
            int next = start + 10;
            Emit(next & 0xFF); Emit(next >> 8);
            Emit(10); Emit(0);
            Emit(0x9E);
            foreach (char c in digits) Emit(c);
            Emit(0); Emit(0); Emit(0);
        }

        void Include(string arguments, Node node)
        {
            string path = FilePath(arguments, node);
            List<Node> included;
            try
            {
                included = files.Include(path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                throw Error($"cannot read {Path.GetFileName(path)}: {e.Message}");
            }
            int saved = zone;
            zone = ++zones;
            Execute(included);
            zone = saved;
            location = node;
        }

        void Binary(string arguments, Node node)
        {
            var parts = SplitArguments(arguments, allowEmpty: true);
            string path = FilePath(parts[0], node);
            byte[] data;
            try
            {
                data = files.ReadBinary(path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                throw Error($"cannot read {Path.GetFileName(path)}: {e.Message}");
            }
            int skip = parts.Count > 2 && parts[2].Length > 0 ? Known(parts[2], "the skip of !binary") : 0;
            int size = parts.Count > 1 && parts[1].Length > 0 ? Known(parts[1], "the size of !binary") : Math.Max(0, data.Length - skip);
            for (int k = 0; k < size; k++) Emit(skip + k < data.Length ? data[skip + k] : 0);
        }

        string FilePath(string argument, Node node)
        {
            string name = argument.Trim();
            if (name.Length >= 2 && (name[0] == '"' && name[^1] == '"' || name[0] == '<' && name[^1] == '>')) name = name[1..^1];
            else throw Error("expected a file name in double quotes");
            try
            {
                return files.Resolve(name, node);
            }
            catch (FileNotFoundException e)
            {
                throw Error(e.Message);
            }
        }

        // !if: the block runs if the condition holds, otherwise the "else" block that follows (if any)
        void If(bool condition, Node node, List<Node> nodes, ref int index)
        {
            if (node.Block is null) throw Error("missing { block } after !if");
            Node? otherwise = null;
            if (index + 1 < nodes.Count && nodes[index + 1].Text.Equals("else", StringComparison.OrdinalIgnoreCase))
            {
                otherwise = nodes[++index];
                if (otherwise.Block is null) throw Error("missing { block } after else");
            }
            if (condition) Execute(node.Block);
            else if (otherwise is not null) Execute(otherwise.Block!);
        }

        // !for NAME, START, END { block } (and the old form !for NAME, END, counting from 1)
        void For(string arguments, Node node)
        {
            if (node.Block is null) throw Error("missing { block } after !for");
            var parts = SplitArguments(arguments);
            if (parts.Count is not (2 or 3)) throw Error("expected !for NAME, START, END { ... }");
            string name = parts[0];
            int start = parts.Count == 3 ? Known(parts[1], "the start of !for") : 1;
            int end = Known(parts[^1], "the end of !for");
            int step = start <= end ? 1 : -1;
            for (int value = start; ; value += step)
            {
                Define(name, Num.Of(value), canRedefine: true);
                Execute(node.Block);
                location = node;
                if (value == end) break;
            }
        }

        void DefineMacro(string arguments, Node node)
        {
            if (node.Block is null) throw Error("missing { block } after !macro");
            int space = 0;
            while (space < arguments.Length && !char.IsWhiteSpace(arguments[space])) space++;
            string name = arguments[..space];
            if (name.Length == 0) throw Error("missing macro name");
            var parameters = arguments[space..].Trim().Length == 0 ? [] : SplitArguments(arguments[space..]);
            if (parameters.Any(p => p.StartsWith('~'))) throw Error("parameters by reference (~) are not supported");
            macros[name] = new Macro(name, parameters, node.Block);
        }

        // +NAME arguments: the arguments are evaluated where the call is, the body runs in a new zone
        void CallMacro(string text)
        {
            int end = 0;
            while (end < text.Length && IsSymbolChar(text[end]) || end < text.Length && text[end] == '.' && end == 0) end++;
            string name = text[..end];
            string arguments = text[end..].Trim();
            if (!macros.TryGetValue(name, out var macro)) throw Error($"unknown macro: {name}");
            var items = arguments.Length == 0 ? [] : SplitArguments(arguments);
            if (items.Count != macro.Parameters.Count)
                throw Error($"macro {name} wants {macro.Parameters.Count} argument(s), not {items.Count}");
            var values = items.Select(Evaluate).ToList();
            if (++macroDepth > 100) throw Error("macros nested too deeply");

            var call = location;
            int savedZone = zone, savedCheap = cheap;
            zone = ++zones;
            cheap = ++cheaps;
            for (int k = 0; k < values.Count; k++)
                if (values[k] is Num v) Define(macro.Parameters[k], v, canRedefine: true);
            Execute(macro.Body);
            zone = savedZone;
            cheap = savedCheap;
            location = call;
            macroDepth--;
        }

        string Message(string arguments) =>
            string.Concat(SplitArguments(arguments).Select(item => item.StartsWith('"') ? Unquote(item) : Evaluate(item)?.AsInt.ToString()));

        #endregion

        #region Instructions

        void Instruction(string mnemonic, string operand)
        {
            if (Aliases.TryGetValue(mnemonic, out var real)) mnemonic = real;
            var (mode, expression) = ParseOperand(mnemonic, operand);
            Num? value = expression is null ? null : Evaluate(expression);

            if (mode == Mode.Rel)
            {
                Emit(Opcodes[(mnemonic, Mode.Rel)]);
                long target = value?.AsInt ?? (final ? throw Error($"undefined branch target: {expression}") : pc + 1);
                long offset = target - (pc + 1);
                if (final && (offset < -128 || offset > 127)) throw Error($"branch out of range ({offset} bytes)");
                Emit((int)(offset & 0xFF));
                return;
            }

            // Zero page when the value is known, fits in a byte and the mode exists for the instruction
            if (value is Num known && known.AsInt is >= 0 and < 0x100)
            {
                var zeroPage = mode switch { Mode.Abs => Mode.Zp, Mode.AbsX => Mode.ZpX, Mode.AbsY => Mode.ZpY, _ => mode };
                if (zeroPage != mode && Opcodes.ContainsKey((mnemonic, zeroPage))) mode = zeroPage;
            }
            if (!Opcodes.TryGetValue((mnemonic, mode), out byte opcode))
                throw Error($"{mnemonic} has no {mode} addressing mode");

            Emit(opcode);
            if (Size(mode) == 1) return;
            long operandValue = value?.AsInt ?? (final ? throw Error($"undefined value: {expression}") : 0xFFFF);
            if (Size(mode) == 2)
            {
                if (final && (operandValue < -128 || operandValue > 0xFF)) throw Error($"value does not fit in a byte: {expression}");
                Emit((int)(operandValue & 0xFF));
            }
            else
            {
                if (final && (operandValue < 0 || operandValue > 0xFFFF)) throw Error($"address out of range: {expression}");
                Emit((int)(operandValue & 0xFF));
                Emit((int)(operandValue >> 8 & 0xFF));
            }
        }

        static readonly HashSet<string> Branches = ["BPL", "BMI", "BVC", "BVS", "BCC", "BCS", "BNE", "BEQ"];
        static readonly HashSet<string> Shifts = ["ASL", "LSR", "ROL", "ROR"];

        [GeneratedRegex(@"^\((.*),\s*[xX]\s*\)$")] private static partial Regex IndexedIndirect();
        [GeneratedRegex(@"^\((.*)\)\s*,\s*[yY]$")] private static partial Regex IndirectIndexed();
        [GeneratedRegex(@"^(.*),\s*[xX]$")] private static partial Regex IndexedX();
        [GeneratedRegex(@"^(.*),\s*[yY]$")] private static partial Regex IndexedY();

        (Mode Mode, string? Expression) ParseOperand(string mnemonic, string operand)
        {
            if (operand.Length == 0) return (Shifts.Contains(mnemonic) ? Mode.Acc : Mode.Imp, null);
            if (operand.StartsWith('#')) return (Mode.Imm, operand[1..]);
            if (Branches.Contains(mnemonic)) return (Mode.Rel, operand);
            if (operand is "A" or "a" && Shifts.Contains(mnemonic)) return (Mode.Acc, null);

            if (IndexedIndirect().Match(operand) is { Success: true } ix) return (Mode.IndX, ix.Groups[1].Value);
            if (IndirectIndexed().Match(operand) is { Success: true } iy) return (Mode.IndY, iy.Groups[1].Value);
            // "(expression)" is indirect only for JMP: for the other instructions the parentheses are just grouping
            if (mnemonic == "JMP" && operand.StartsWith('(') && operand.EndsWith(')') && Balanced(operand[1..^1]))
                return (Mode.Ind, operand[1..^1]);
            if (IndexedX().Match(operand) is { Success: true } x) return (Mode.AbsX, x.Groups[1].Value);
            if (IndexedY().Match(operand) is { Success: true } y) return (Mode.AbsY, y.Groups[1].Value);
            return (Mode.Abs, operand);
        }

        // True if the parentheses of the text are balanced on their own (so "(a)+(b)" is not one parenthesized term)
        static bool Balanced(string text)
        {
            int depth = 0;
            foreach (char c in text)
            {
                if (c == '(') depth++;
                else if (c == ')' && --depth < 0) return false;
            }
            return depth == 0;
        }

        #endregion

        #region Text

        // Splits at the commas that are outside strings and parentheses
        List<string> SplitArguments(string text, bool allowEmpty = false)
        {
            var items = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;
            int depth = 0;
            for (int k = 0; k < text.Length; k++)
            {
                char c = text[k];
                if (c == '"') inQuotes = !inQuotes;
                else if (!inQuotes && c == '\'' && k + 2 < text.Length && text[k + 2] == '\'')
                {
                    current.Append(text, k, 3);
                    k += 2;
                    continue;
                }
                else if (!inQuotes && c == '(') depth++;
                else if (!inQuotes && c == ')') depth--;
                if (c == ',' && !inQuotes && depth == 0) { items.Add(current.ToString().Trim()); current.Clear(); }
                else current.Append(c);
            }
            if (current.Length > 0 || items.Count > 0) items.Add(current.ToString().Trim());
            if (items.Count == 0 || (!allowEmpty && items.Any(item => item.Length == 0))) throw Error("missing argument");
            return items;
        }

        static int TopLevelComma(string text)
        {
            int depth = 0;
            bool inQuotes = false;
            for (int k = 0; k < text.Length; k++)
            {
                if (text[k] == '"') inQuotes = !inQuotes;
                else if (!inQuotes && text[k] == '(') depth++;
                else if (!inQuotes && text[k] == ')') depth--;
                else if (!inQuotes && depth == 0 && text[k] == ',') return k;
            }
            return -1;
        }

        string Unquote(string text)
        {
            text = text.Trim();
            if (text.Length < 2 || text[0] != '"' || text[^1] != '"') throw Error("expected a string in double quotes");
            return text[1..^1];
        }

        // Project dialect: PETSCII for text printed with CHROUT: upper-case letters, digits and punctuation as in ASCII
        int Petscii(char c)
        {
            char upper = char.ToUpperInvariant(c);
            if (upper > 0x7F) throw Error($"non-ASCII character: '{c}'");
            return upper;
        }

        // Project dialect: screen codes (video memory): @ = 0, A-Z = 1-26, space and symbols 32-63 as in PETSCII
        int ScreenCode(char c)
        {
            int petscii = Petscii(c);
            return petscii switch
            {
                >= 0x40 and <= 0x5F => petscii - 0x40,
                >= 0x20 and <= 0x3F => petscii,
                _ => throw Error($"character without a screen code: '{c}'"),
            };
        }

        // ACME conversion tables: raw, PETSCII (upper and lower case swapped), C64 screen codes
        int Convert(char c, Conversion table)
        {
            if (c > 0xFF) throw Error($"character not in 8 bits: '{c}'");
            int b = c;
            return table switch
            {
                Conversion.Petscii when b is >= 'A' and <= 'Z' => b | 0x80,
                Conversion.Petscii when b is >= 'a' and <= 'z' => b - 32,
                Conversion.Screen when b is >= 'a' and <= 'z' => b - 96,
                Conversion.Screen when b is >= '[' and <= '_' => b - 64,
                Conversion.Screen when b == '`' => 64,
                Conversion.Screen when b == '@' => 0,
                _ => b,
            };
        }

        #endregion
    }
}
