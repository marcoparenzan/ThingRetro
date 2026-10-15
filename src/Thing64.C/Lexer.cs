using System.Globalization;
using System.Text;

namespace Thing64.C;

public enum TokenKind { Identifier, Keyword, Number, Character, String, Punctuator, End }

public sealed record Token(TokenKind Kind, string Text, int Value, int Line, int Column)
{
    public bool Is(string text) => Kind is TokenKind.Keyword or TokenKind.Punctuator && Text == text;
    public override string ToString() => Kind == TokenKind.End ? "the end of the file" : $"'{Text}'";
}

// An error in the C source: line and column from 1
public sealed class CException(string message, int line, int column) : Exception(message)
{
    public int Line { get; } = line;
    public int Column { get; } = column;
}

// From text to tokens. A small preprocessor works on the way: #define NAME tokens (object-like macros, expanded
// where NAME appears), #undef, and the other directives (#include, #pragma) are ignored here
public static class Lexer
{
    static readonly HashSet<string> Keywords =
    [
        "void", "char", "int", "short", "long", "signed", "unsigned", "const", "volatile", "static", "extern",
        "if", "else", "while", "do", "for", "return", "break", "continue", "sizeof", "__asm", "__zp",
    ];

    // Longest first: ">>=" before ">>" before ">"
    static readonly string[] Punctuators =
    [
        "<<=", ">>=", "...", "->", "++", "--", "<<", ">>", "<=", ">=", "==", "!=", "&&", "||",
        "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=",
        "+", "-", "*", "/", "%", "&", "|", "^", "~", "!", "<", ">", "=", "?", ":", ";", ",", ".", "(", ")", "[", "]", "{", "}",
    ];

    public static List<Token> Tokenize(string source)
    {
        var tokens = new List<Token>();
        var macros = new Dictionary<string, List<Token>>();
        int line = 1, column = 1, i = 0;
        bool lineStart = true;

        void Advance(int count)
        {
            for (int k = 0; k < count && i < source.Length; k++, i++)
            {
                if (source[i] == '\n') { line++; column = 1; lineStart = true; }
                else column++;
            }
        }

        while (i < source.Length)
        {
            char c = source[i];
            if (c == '\n') { Advance(1); continue; }
            if (char.IsWhiteSpace(c)) { Advance(1); continue; }
            if (c == '/' && Peek(1) == '/') { while (i < source.Length && source[i] != '\n') Advance(1); continue; }
            if (c == '/' && Peek(1) == '*')
            {
                int startLine = line, startColumn = column;
                Advance(2);
                while (i < source.Length && !(source[i] == '*' && Peek(1) == '/')) Advance(1);
                if (i >= source.Length) throw new CException("Comment without its end (*/)", startLine, startColumn);
                Advance(2);
                continue;
            }

            if (c == '#' && lineStart)
            {
                int directiveLine = line;
                string text = ReadDirective();
                Directive(text, directiveLine, macros);
                continue;
            }
            lineStart = false;
            int tokenLine = line, tokenColumn = column;

            if (char.IsLetter(c) || c == '_')
            {
                int start = i;
                while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_')) Advance(1);
                string word = source[start..i];
                if (macros.TryGetValue(word, out var body))
                {
                    foreach (var t in body) tokens.Add(t with { Line = tokenLine, Column = tokenColumn });
                    continue;
                }
                tokens.Add(new Token(Keywords.Contains(word) ? TokenKind.Keyword : TokenKind.Identifier, word, 0, tokenLine, tokenColumn));
                continue;
            }
            if (char.IsDigit(c))
            {
                int start = i;
                while (i < source.Length && (char.IsLetterOrDigit(source[i]))) Advance(1);
                string text = source[start..i];
                tokens.Add(new Token(TokenKind.Number, text, Number(text, tokenLine, tokenColumn), tokenLine, tokenColumn));
                continue;
            }
            if (c == '\'')
            {
                Advance(1);
                int value = Escaped(tokenLine, tokenColumn);
                if (i >= source.Length || source[i] != '\'') throw new CException("A character constant is one character between ' and '", tokenLine, tokenColumn);
                Advance(1);
                tokens.Add(new Token(TokenKind.Character, ((char)value).ToString(), Petscii(value), tokenLine, tokenColumn));
                continue;
            }
            if (c == '"')
            {
                Advance(1);
                var text = new StringBuilder();
                while (i < source.Length && source[i] != '"')
                {
                    if (source[i] == '\n') throw new CException("A string without its closing \"", tokenLine, tokenColumn);
                    text.Append((char)Escaped(tokenLine, tokenColumn));
                }
                if (i >= source.Length) throw new CException("A string without its closing \"", tokenLine, tokenColumn);
                Advance(1);
                tokens.Add(new Token(TokenKind.String, text.ToString(), 0, tokenLine, tokenColumn));
                continue;
            }
            string? punctuator = Punctuators.FirstOrDefault(p => string.CompareOrdinal(source, i, p, 0, p.Length) == 0);
            if (punctuator is null) throw new CException($"Unexpected character '{c}'", tokenLine, tokenColumn);
            Advance(punctuator.Length);
            tokens.Add(new Token(TokenKind.Punctuator, punctuator, 0, tokenLine, tokenColumn));
        }
        tokens.Add(new Token(TokenKind.End, "", 0, line, column));
        return tokens;

        char Peek(int offset) => i + offset < source.Length ? source[i + offset] : '\0';

        // The rest of the line, with backslash-newline joining lines
        string ReadDirective()
        {
            var text = new StringBuilder();
            while (i < source.Length && source[i] != '\n')
            {
                if (source[i] == '\\' && Peek(1) == '\n') { Advance(2); continue; }
                text.Append(source[i]);
                Advance(1);
            }
            return text.ToString();
        }

        int Escaped(int tokenLine, int tokenColumn)
        {
            if (i >= source.Length) throw new CException("Unexpected end of the file", tokenLine, tokenColumn);
            char ch = source[i];
            Advance(1);
            if (ch != '\\') return ch;
            char e = i < source.Length ? source[i] : '\0';
            Advance(1);
            return e switch
            {
                'n' => 13,   // a new line on the C64 is RETURN
                'r' => 13,
                't' => 9,
                '0' => 0,
                '\\' => '\\',
                '\'' => '\'',
                '"' => '"',
                'x' => ReadHex(),
                _ => throw new CException($"Unknown escape \\{e}", tokenLine, tokenColumn),
            };

            int ReadHex()
            {
                int start = i;
                while (i < source.Length && Uri.IsHexDigit(source[i]) && i - start < 2) Advance(1);
                return int.Parse(source[start..i], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
        }
    }

    // 123, 0x7F, 0b1010, 017 (octal), with u/U/l/L suffixes ignored
    static int Number(string text, int line, int column)
    {
        string digits = text.TrimEnd('u', 'U', 'l', 'L');
        try
        {
            if (digits.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return Convert.ToInt32(digits[2..], 16);
            if (digits.StartsWith("0b", StringComparison.OrdinalIgnoreCase)) return Convert.ToInt32(digits[2..], 2);
            if (digits.Length > 1 && digits[0] == '0') return Convert.ToInt32(digits[1..], 8);
            return int.Parse(digits, CultureInfo.InvariantCulture);
        }
        catch (Exception e) when (e is FormatException or OverflowException or ArgumentException)
        {
            throw new CException($"'{text}' is not a number", line, column);
        }
    }

    // Characters as the C64 prints them: letters become the PETSCII capitals ('a' and 'A' alike, as the C64 shows
    // them in its standard upper case mode); digits and punctuation are the same as ASCII
    public static int Petscii(int c) => c switch
    {
        >= 'a' and <= 'z' => c - 32,
        _ => c,
    };

    static void Directive(string text, int line, Dictionary<string, List<Token>> macros)
    {
        string body = text.TrimStart('#').Trim();
        string name = body.Split(' ', '\t')[0];
        string rest = body[name.Length..].Trim();
        switch (name)
        {
            case "define":
            {
                string macro = new string(rest.TakeWhile(ch => char.IsLetterOrDigit(ch) || ch == '_').ToArray());
                if (macro.Length == 0) throw new CException("#define needs a name", line, 1);
                if (rest.Length > macro.Length && rest[macro.Length] == '(') throw new CException("Macros with parameters are not supported: use a function", line, 1);
                var expansion = Tokenize(rest[macro.Length..]);
                expansion.RemoveAt(expansion.Count - 1);   // the End token
                // The macros already defined are expanded in the body now: #define B (A + 1) after #define A 5
                macros[macro] = expansion
                    .SelectMany(t => t.Kind == TokenKind.Identifier && macros.TryGetValue(t.Text, out var inner) ? inner : [t])
                    .Select(t => t with { Line = line }).ToList();
                break;
            }
            case "undef":
                macros.Remove(rest);
                break;
            case "include" or "pragma" or "":
                break;
            default:
                throw new CException($"Unknown directive #{name}", line, 1);
        }
    }
}
