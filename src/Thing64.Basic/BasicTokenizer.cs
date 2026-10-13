namespace Thing64.Basic;

// Turns a BASIC V2 program in text into the .prg format the C64 would have in memory after it was typed in:
// linked lines, 16-bit line numbers, keywords replaced by tokens ($80-$CB).
// It follows the KERNAL rules (CRUNCH): keywords are searched in table order, not inside quotes, not after
// REM and not in DATA lines up to the colon. As on the C64, a variable that contains a keyword
// (e.g. TOTAL contains TO) is tokenized too
public static class BasicTokenizer
{
    public const ushort BasicStart = 0x0801;

    static readonly string[] Keywords =
    [
        "END", "FOR", "NEXT", "DATA", "INPUT#", "INPUT", "DIM", "READ", "LET", "GOTO", "RUN", "IF", "RESTORE",
        "GOSUB", "RETURN", "REM", "STOP", "ON", "WAIT", "LOAD", "SAVE", "VERIFY", "DEF", "POKE", "PRINT#",
        "PRINT", "CONT", "LIST", "CLR", "CMD", "SYS", "OPEN", "CLOSE", "GET", "NEW", "TAB(", "TO", "FN", "SPC(",
        "THEN", "NOT", "STEP", "+", "-", "*", "/", "^", "AND", "OR", ">", "=", "<", "SGN", "INT", "ABS", "USR",
        "FRE", "POS", "SQR", "RND", "LOG", "EXP", "COS", "SIN", "TAN", "ATN", "PEEK", "LEN", "STR$", "VAL",
        "ASC", "CHR$", "LEFT$", "RIGHT$", "MID$", "GO",
    ];

    const byte TokenData = 0x83;
    const byte TokenRem = 0x8F;
    const byte TokenPrint = 0x99;

    // Complete program, with the load address $0801 in the first two bytes
    public static byte[] Tokenize(string source)
    {
        var program = new List<byte> { BasicStart & 0xFF, BasicStart >> 8 };
        ushort address = BasicStart;

        foreach (var rawLine in source.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r').Trim();
            if (line.Length == 0) continue;

            int digits = 0;
            while (digits < line.Length && char.IsDigit(line[digits])) digits++;
            if (digits == 0) throw new FormatException($"Line without a number: \"{line}\"");
            int number = int.Parse(line[..digits]);
            if (number > 63999) throw new FormatException($"Line number above 63999: {number}");

            byte[] body = Crunch(line[digits..].TrimStart());
            ushort next = (ushort)(address + 2 + 2 + body.Length + 1);
            program.AddRange([(byte)next, (byte)(next >> 8), (byte)number, (byte)(number >> 8)]);
            program.AddRange(body);
            program.Add(0);
            address = next;
        }

        program.AddRange([0, 0]); // end of the program
        return program.ToArray();
    }

    static byte[] Crunch(string text)
    {
        var output = new List<byte>();
        bool inQuotes = false, inData = false, inRem = false;
        int i = 0;
        while (i < text.Length)
        {
            char c = char.ToUpperInvariant(text[i]);
            if (c > 0x7F) throw new FormatException($"Non-ASCII character: '{text[i]}'");

            if (inRem || (inQuotes && c != '"') || (inData && c != ':' && c != '"'))
            {
                output.Add((byte)c);
                i++;
                continue;
            }
            if (c == '"') { inQuotes = !inQuotes; output.Add((byte)c); i++; continue; }
            if (c == ':') { inData = false; output.Add((byte)c); i++; continue; }
            if (c == '?') { output.Add(TokenPrint); i++; continue; }
            if (c == ' ' || char.IsDigit(c) || c == ';') { output.Add((byte)c); i++; continue; }

            int keyword = FindKeyword(text, i);
            if (keyword < 0) { output.Add((byte)c); i++; continue; }

            byte token = (byte)(0x80 + keyword);
            output.Add(token);
            i += Keywords[keyword].Length;
            if (token == TokenRem) inRem = true;
            if (token == TokenData) inData = true;
        }
        return output.ToArray();
    }

    static int FindKeyword(string text, int start)
    {
        for (int k = 0; k < Keywords.Length; k++)
        {
            string keyword = Keywords[k];
            if (start + keyword.Length <= text.Length &&
                string.Compare(text, start, keyword, 0, keyword.Length, StringComparison.OrdinalIgnoreCase) == 0)
                return k;
        }
        return -1;
    }

    // One-line BASIC program that runs the machine code right after it: 10 SYS2061 ($080D)
    public static byte[] SysStub(byte[] machineCode)
    {
        var stub = Tokenize("10 SYS2061");
        if (stub.Length - 2 != 0x080D - BasicStart)
            throw new InvalidOperationException("The SYS stub must end at $080D");
        return [.. stub, .. machineCode];
    }
}
