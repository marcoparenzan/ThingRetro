using System.Globalization;

namespace Fake64.Asm;

public static partial class Assembler
{
    // A value: an integer or, after a float operation (ACME rules), a floating point number
    readonly record struct Num(long Int, double Float, bool IsFloat)
    {
        public static Num Of(long value) => new(value, value, false);
        public static Num Of(double value) => new((long)value, value, true);

        public long AsInt => IsFloat ? (long)Math.Truncate(Float) : Int;
        public double AsDouble => IsFloat ? Float : Int;
        public bool IsTrue => IsFloat ? Float != 0 : Int != 0;
    }

    // Expression parser with ACME's priorities. Returns null when a symbol is not known yet (in the first passes)
    sealed class ExpressionParser(Pass pass, string text)
    {
        int i;

        public bool AtEnd { get { SkipSpaces(); return i >= text.Length; } }

        public Num? Parse()
        {
            var value = Expression(0);
            SkipSpaces();
            if (i < text.Length) throw pass.Error($"unexpected '{text[i..]}' in '{text}'");
            return value;
        }

        void SkipSpaces()
        {
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
        }

        // Binary operators: text, priority, right associative
        static readonly (string Op, int Priority, bool Right)[] Binary =
        [
            (">>>", 8, false), ("<<", 8, false), (">>", 8, false), ("<=", 6, false), (">=", 6, false), ("<>", 5, false),
            ("><", 5, false), ("!=", 5, false), ("==", 4, false), ("|", 1, false), ("&", 3, false), ("=", 4, false),
            ("<", 6, false), (">", 6, false), ("+", 9, false), ("-", 9, false), ("*", 10, false), ("/", 10, false),
            ("%", 10, false), ("^", 12, true),
        ];

        static readonly (string Word, string Op, int Priority)[] BinaryWords =
        [
            ("OR", "|", 1), ("XOR", "XOR", 2), ("AND", "&", 3), ("DIV", "DIV", 10), ("MOD", "%", 10),
            ("ASL", "<<", 8), ("LSL", "<<", 8), ("ASR", ">>", 8), ("LSR", ">>>", 8),
        ];

        (string Op, int Priority, bool Right, int Length)? PeekBinary()
        {
            SkipSpaces();
            if (i >= text.Length) return null;
            foreach (var (word, op, priority) in BinaryWords)
                if (IsWordAt(word)) return (op, priority, false, word.Length);
            foreach (var (op, priority, right) in Binary)
                if (string.CompareOrdinal(text, i, op, 0, op.Length) == 0) return (op, priority, right, op.Length);
            return null;
        }

        bool IsWordAt(string word) =>
            i + word.Length <= text.Length &&
            string.Compare(text, i, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) == 0 &&
            (i + word.Length == text.Length || !IsSymbolChar(text[i + word.Length])) &&
            (i == 0 || !IsSymbolChar(text[i - 1]));

        Num? Expression(int minPriority)
        {
            var left = Prefix();
            while (PeekBinary() is var (op, priority, right, length) && priority >= minPriority)
            {
                i += length;
                var rightValue = Expression(right ? priority : priority + 1);
                left = Apply(op, left, rightValue);
            }
            return left;
        }

        Num? Prefix()
        {
            SkipSpaces();
            if (i >= text.Length) throw pass.Error($"incomplete expression: '{text}'");
            char c = text[i];

            if ((c == '-' || c == '+') && AnonymousReference() is string anonymous) return pass.Anonymous(anonymous);
            if (c == '<' && !Next("<<")) { i++; return Map(Expression(8), v => Num.Of(v.AsInt & 0xFF)); }
            if (c == '>' && !Next(">>")) { i++; return Map(Expression(8), v => Num.Of(v.AsInt >> 8 & 0xFF)); }
            if (c == '^') { i++; return Map(Expression(8), v => Num.Of(v.AsInt >> 16 & 0xFF)); }
            if (c == '!') { i++; return Map(Expression(13), v => Num.Of(~v.AsInt)); }
            if (IsWordAt("NOT")) { i += 3; return Map(Expression(13), v => Num.Of(~v.AsInt)); }
            if (c == '-') { i++; return Map(Expression(12), v => v.IsFloat ? Num.Of(-v.Float) : Num.Of(-v.Int)); }
            if (c == '+') { i++; return Expression(12); }
            return Primary();
        }

        bool Next(string s) => string.CompareOrdinal(text, i, s, 0, s.Length) == 0;

        // "-", "--", "+", "++"... followed by the end of the operand
        string? AnonymousReference()
        {
            int j = i;
            while (j < text.Length && text[j] == text[i]) j++;
            int k = j;
            while (k < text.Length && char.IsWhiteSpace(text[k])) k++;
            if (k < text.Length && text[k] is not (',' or ')')) return null;
            string name = text[i..j];
            i = j;
            return name;
        }

        Num? Primary()
        {
            char c = text[i];
            int start = i;

            if (c == '(')
            {
                i++;
                var inner = Expression(0);
                SkipSpaces();
                if (i >= text.Length || text[i] != ')') throw pass.Error($"missing ')' in '{text}'");
                i++;
                return inner;
            }
            if (c == '*') { i++; return Num.Of(pass.RequirePc()); }
            if (c == '$') return Integer(1, 16, Uri.IsHexDigit);
            if (c == '0' && i + 1 < text.Length && text[i + 1] is 'x' or 'X') return Integer(2, 16, Uri.IsHexDigit);
            if (c == '0' && i + 2 < text.Length && text[i + 1] is 'b' or 'B' && text[i + 2] is '0' or '1') return BinaryNumber(2);
            if (c == '%') return BinaryNumber(1);
            if (c == '&') return Integer(1, 8, d => d is >= '0' and <= '7');
            if (char.IsDigit(c))
            {
                while (i < text.Length && char.IsDigit(text[i])) i++;
                if (i + 1 < text.Length && text[i] == '.' && char.IsDigit(text[i + 1]))
                {
                    i++;
                    while (i < text.Length && char.IsDigit(text[i])) i++;
                    return Num.Of(double.Parse(text[start..i], CultureInfo.InvariantCulture));
                }
                return Num.Of(long.Parse(text[start..i], CultureInfo.InvariantCulture));
            }
            if (c == '\'' && i + 2 < text.Length && text[i + 2] == '\'')
            {
                i += 3;
                return Num.Of(text[start + 1]);
            }
            if (c == '"' && i + 2 < text.Length && text[i + 2] == '"')
            {
                i += 3;
                return Num.Of(text[start + 1]);
            }
            if (IsSymbolStart(c))
            {
                i++;
                while (i < text.Length && IsSymbolChar(text[i])) i++;
                string name = text[start..i];
                SkipSpaces();
                if (i < text.Length && text[i] == '(' && Functions.ContainsKey(name.ToLowerInvariant()))
                {
                    i++;
                    var argument = Expression(0);
                    SkipSpaces();
                    if (i >= text.Length || text[i] != ')') throw pass.Error($"missing ')' after {name}( in '{text}'");
                    i++;
                    return Map(argument, Functions[name.ToLowerInvariant()]);
                }
                return pass.Symbol(name);
            }
            throw pass.Error($"unexpected character '{c}' in '{text}'");
        }

        Num Integer(int prefix, int radix, Func<char, bool> isDigit)
        {
            i += prefix;
            int start = i;
            while (i < text.Length && isDigit(text[i])) i++;
            if (i == start) throw pass.Error($"number without digits in '{text}'");
            return Num.Of(Convert.ToInt64(text[start..i], radix));
        }

        // %0101 or %.#.# (ACME: '.' = 0, '#' = 1), 0b0101
        Num BinaryNumber(int prefix)
        {
            i += prefix;
            int start = i;
            long value = 0;
            while (i < text.Length && text[i] is '0' or '1' or '.' or '#')
            {
                value = value << 1 | (text[i] is '1' or '#' ? 1L : 0L);
                i++;
            }
            if (i == start) throw pass.Error($"binary number without digits in '{text}'");
            return Num.Of(value);
        }

        static readonly Dictionary<string, Func<Num, Num>> Functions = new()
        {
            ["sin"] = v => Num.Of(Math.Sin(v.AsDouble)),
            ["cos"] = v => Num.Of(Math.Cos(v.AsDouble)),
            ["tan"] = v => Num.Of(Math.Tan(v.AsDouble)),
            ["arcsin"] = v => Num.Of(Math.Asin(v.AsDouble)),
            ["arccos"] = v => Num.Of(Math.Acos(v.AsDouble)),
            ["arctan"] = v => Num.Of(Math.Atan(v.AsDouble)),
            ["int"] = v => Num.Of(v.AsInt),
            ["float"] = v => Num.Of(v.AsDouble),
            ["address"] = v => v,
            ["addr"] = v => v,
        };

        static Num? Map(Num? value, Func<Num, Num> function) => value is Num v ? function(v) : null;

        Num? Apply(string op, Num? left, Num? right)
        {
            if (left is not Num a || right is not Num b) return null;
            bool useFloat = a.IsFloat || b.IsFloat;
            switch (op)
            {
                case "+": return useFloat ? Num.Of(a.AsDouble + b.AsDouble) : Num.Of(a.Int + b.Int);
                case "-": return useFloat ? Num.Of(a.AsDouble - b.AsDouble) : Num.Of(a.Int - b.Int);
                case "*": return useFloat ? Num.Of(a.AsDouble * b.AsDouble) : Num.Of(a.Int * b.Int);
                case "/":
                    if (useFloat) return b.AsDouble == 0 ? throw pass.Error("division by zero") : Num.Of(a.AsDouble / b.AsDouble);
                    return b.Int == 0 ? throw pass.Error("division by zero") : Num.Of(a.Int / b.Int);
                case "DIV": return b.AsInt == 0 ? throw pass.Error("division by zero") : Num.Of(a.AsInt / b.AsInt);
                case "%": return b.AsInt == 0 ? throw pass.Error("division by zero") : Num.Of(a.AsInt % b.AsInt);
                case "^": return useFloat ? Num.Of(Math.Pow(a.AsDouble, b.AsDouble)) : Num.Of((long)Math.Pow(a.Int, b.Int));
                case "<<": return Num.Of(a.AsInt << (int)b.AsInt);
                case ">>": return Num.Of(a.AsInt >> (int)b.AsInt);
                case ">>>": return Num.Of((long)((uint)a.AsInt >> (int)b.AsInt));
                case "&": return Num.Of(a.AsInt & b.AsInt);
                case "|": return Num.Of(a.AsInt | b.AsInt);
                case "XOR": return Num.Of(a.AsInt ^ b.AsInt);
                case "=" or "==": return Bool(useFloat ? a.AsDouble == b.AsDouble : a.Int == b.Int);
                case "!=" or "<>" or "><": return Bool(useFloat ? a.AsDouble != b.AsDouble : a.Int != b.Int);
                case "<": return Bool(a.AsDouble < b.AsDouble);
                case ">": return Bool(a.AsDouble > b.AsDouble);
                case "<=": return Bool(a.AsDouble <= b.AsDouble);
                case ">=": return Bool(a.AsDouble >= b.AsDouble);
                default: throw pass.Error($"unknown operator {op}");
            }
        }

        static Num Bool(bool value) => Num.Of(value ? 1 : 0);
    }

    static bool IsSymbolStart(char c) => char.IsLetter(c) || c is '_' or '.' or '@' || c > 127;
    static bool IsSymbolChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c > 127;
}
