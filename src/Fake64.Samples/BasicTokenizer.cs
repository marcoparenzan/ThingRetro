namespace Fake64.Samples;

// Trasforma un programma BASIC V2 in testo nel formato .prg che il C64 avrebbe in memoria dopo averlo
// digitato: righe collegate, numeri di riga a 16 bit, parole chiave sostituite dai token ($80-$CB).
// Segue le regole del KERNAL (CRUNCH): le parole chiave si cercano nell'ordine della tabella, non dentro
// le virgolette, non dopo REM e non nelle righe di DATA fino ai due punti. Come sul C64, una variabile
// che contiene una parola chiave (es. TOTAL contiene TO) viene tokenizzata anch'essa
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

    // Programma completo, con l'indirizzo di caricamento $0801 nei primi due byte
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
            if (digits == 0) throw new FormatException($"Riga senza numero: \"{line}\"");
            int number = int.Parse(line[..digits]);
            if (number > 63999) throw new FormatException($"Numero di riga oltre 63999: {number}");

            byte[] body = Crunch(line[digits..].TrimStart());
            ushort next = (ushort)(address + 2 + 2 + body.Length + 1);
            program.AddRange([(byte)next, (byte)(next >> 8), (byte)number, (byte)(number >> 8)]);
            program.AddRange(body);
            program.Add(0);
            address = next;
        }

        program.AddRange([0, 0]); // fine del programma
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
            if (c > 0x7F) throw new FormatException($"Carattere non ASCII: '{text[i]}'");

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

    // Programma BASIC di una riga che esegue il codice macchina subito dopo: 10 SYS2061 ($080D)
    public static byte[] SysStub(byte[] machineCode)
    {
        var stub = Tokenize("10 SYS2061");
        if (stub.Length - 2 != 0x080D - BasicStart)
            throw new InvalidOperationException("Lo stub SYS deve terminare a $080D");
        return [.. stub, .. machineCode];
    }
}
