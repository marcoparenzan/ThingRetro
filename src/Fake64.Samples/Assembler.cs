using System.Globalization;
using System.Text;

namespace Fake64.Samples;

// Assembler 6502 essenziale per gli esempi. Sintassi:
//
//   ; commento                       fino a fine riga
//   * = $0801                        indirizzo di assemblaggio (in avanti: lo spazio intermedio vale $FF)
//   BORDO = $D020                    costante
//   ciclo:  LDA #<testo              etichetta (con i due punti) e istruzione
//           .byte $01, 2, "AB"       byte ed eventuali stringhe ASCII
//           .word ciclo              parole a 16 bit (little-endian)
//           .text "CIAO"             testo in PETSCII (maiuscole)
//           .screen "CIAO"           testo in codici schermo (A = 1), da scrivere direttamente in $0400
//           .fill 8, $00             ripetizione di un byte
//           .basic                   riga BASIC "10 SYSnnnn" che salta subito dopo sé stessa (a $0801)
//
// Numeri: $esadecimale, %binario, decimale, 'c'. Espressioni: + e -, <valore (byte basso), >valore (byte alto),
// * (indirizzo corrente). Modi di indirizzamento con la sintassi standard; pagina zero scelta in automatico.
// I simboli distinguono maiuscole e minuscole (convenzione: COSTANTI in maiuscolo, etichette in minuscolo)
// Tutti gli opcode del 6510, compresi quelli non documentati
public static class Assembler
{
    enum Mode { Imp, Acc, Imm, Zp, ZpX, ZpY, Abs, AbsX, AbsY, IndX, IndY, Rel, Ind }

    static readonly string[] OpcodeTable =
    [
        "BRK imp","ORA izx","JAM imp","SLO izx","NOP zp", "ORA zp", "ASL zp", "SLO zp", "PHP imp","ORA imm","ASL acc","ANC imm","NOP abs","ORA abs","ASL abs","SLO abs",
        "BPL rel","ORA izy","JAM imp","SLO izy","NOP zpx","ORA zpx","ASL zpx","SLO zpx","CLC imp","ORA aby","NOP imp","SLO aby","NOP abx","ORA abx","ASL abx","SLO abx",
        "JSR abs","AND izx","JAM imp","RLA izx","BIT zp", "AND zp", "ROL zp", "RLA zp", "PLP imp","AND imm","ROL acc","ANC imm","BIT abs","AND abs","ROL abs","RLA abs",
        "BMI rel","AND izy","JAM imp","RLA izy","NOP zpx","AND zpx","ROL zpx","RLA zpx","SEC imp","AND aby","NOP imp","RLA aby","NOP abx","AND abx","ROL abx","RLA abx",
        "RTI imp","EOR izx","JAM imp","SRE izx","NOP zp", "EOR zp", "LSR zp", "SRE zp", "PHA imp","EOR imm","LSR acc","ALR imm","JMP abs","EOR abs","LSR abs","SRE abs",
        "BVC rel","EOR izy","JAM imp","SRE izy","NOP zpx","EOR zpx","LSR zpx","SRE zpx","CLI imp","EOR aby","NOP imp","SRE aby","NOP abx","EOR abx","LSR abx","SRE abx",
        "RTS imp","ADC izx","JAM imp","RRA izx","NOP zp", "ADC zp", "ROR zp", "RRA zp", "PLA imp","ADC imm","ROR acc","ARR imm","JMP ind","ADC abs","ROR abs","RRA abs",
        "BVS rel","ADC izy","JAM imp","RRA izy","NOP zpx","ADC zpx","ROR zpx","RRA zpx","SEI imp","ADC aby","NOP imp","RRA aby","NOP abx","ADC abx","ROR abx","RRA abx",
        "NOP imm","STA izx","NOP imm","SAX izx","STY zp", "STA zp", "STX zp", "SAX zp", "DEY imp","NOP imm","TXA imp","XAA imm","STY abs","STA abs","STX abs","SAX abs",
        "BCC rel","STA izy","JAM imp","SHA izy","STY zpx","STA zpx","STX zpy","SAX zpy","TYA imp","STA aby","TXS imp","TAS aby","SHY abx","STA abx","SHX aby","SHA aby",
        "LDY imm","LDA izx","LDX imm","LAX izx","LDY zp", "LDA zp", "LDX zp", "LAX zp", "TAY imp","LDA imm","TAX imp","LXA imm","LDY abs","LDA abs","LDX abs","LAX abs",
        "BCS rel","LDA izy","JAM imp","LAX izy","LDY zpx","LDA zpx","LDX zpy","LAX zpy","CLV imp","LDA aby","TSX imp","LAS aby","LDY abx","LDA abx","LDX aby","LAX aby",
        "CPY imm","CMP izx","NOP imm","DCP izx","CPY zp", "CMP zp", "DEC zp", "DCP zp", "INY imp","CMP imm","DEX imp","AXS imm","CPY abs","CMP abs","DEC abs","DCP abs",
        "BNE rel","CMP izy","JAM imp","DCP izy","NOP zpx","CMP zpx","DEC zpx","DCP zpx","CLD imp","CMP aby","NOP imp","DCP aby","NOP abx","CMP abx","DEC abx","DCP abx",
        "CPX imm","SBC izx","NOP imm","ISC izx","CPX zp", "SBC zp", "INC zp", "ISC zp", "INX imp","SBC imm","NOP imp","SBC imm","CPX abs","SBC abs","INC abs","ISC abs",
        "BEQ rel","SBC izy","JAM imp","ISC izy","NOP zpx","SBC zpx","INC zpx","ISC zpx","SED imp","SBC aby","NOP imp","ISC aby","NOP abx","SBC abx","INC abx","ISC abx",
    ];

    static readonly Dictionary<(string Mnemonic, Mode Mode), byte> Opcodes = BuildOpcodes();

    static Dictionary<(string, Mode), byte> BuildOpcodes()
    {
        var opcodes = new Dictionary<(string, Mode), byte>
        {
            [("NOP", Mode.Imp)] = 0xEA, // le forme documentate hanno la precedenza sui doppioni
            [("SBC", Mode.Imm)] = 0xE9,
        };
        for (int opcode = 0; opcode < 256; opcode++)
        {
            var parts = OpcodeTable[opcode].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var mode = parts[1] switch
            {
                "imp" => Mode.Imp, "acc" => Mode.Acc, "imm" => Mode.Imm, "zp" => Mode.Zp, "zpx" => Mode.ZpX, "zpy" => Mode.ZpY,
                "abs" => Mode.Abs, "abx" => Mode.AbsX, "aby" => Mode.AbsY, "izx" => Mode.IndX, "izy" => Mode.IndY,
                "rel" => Mode.Rel, _ => Mode.Ind,
            };
            opcodes.TryAdd((parts[0], mode), (byte)opcode);
        }
        return opcodes;
    }

    static int Size(Mode mode) => mode switch
    {
        Mode.Imp or Mode.Acc => 1,
        Mode.Abs or Mode.AbsX or Mode.AbsY or Mode.Ind => 3,
        _ => 2,
    };

    // Risultato: i byte dall'indirizzo di origine in poi e la tabella dei simboli
    public sealed record Result(ushort Origin, byte[] Bytes, IReadOnlyDictionary<string, int> Symbols)
    {
        // File .prg: indirizzo di caricamento seguito dai byte
        public byte[] Prg => [(byte)Origin, (byte)(Origin >> 8), .. Bytes];
    }

    public static Result Assemble(string source, string name = "sorgente")
    {
        var lines = source.Replace("\r\n", "\n").Split('\n');
        var symbols = new Dictionary<string, int>(StringComparer.Ordinal);

        // Più passaggi: la scelta fra pagina zero e indirizzo assoluto cambia le dimensioni, quindi gli
        // indirizzi delle etichette, finché non si stabilizzano
        for (int pass = 0; pass < 10; pass++)
        {
            var state = new Pass(symbols, final: false, name);
            state.Run(lines);
            bool stable = state.Defined.Count == symbols.Count && state.Defined.All(s => symbols.TryGetValue(s.Key, out var v) && v == s.Value);
            symbols = state.Defined;
            if (stable) break;
        }

        var last = new Pass(symbols, final: true, name);
        last.Run(lines);
        if (last.Origin is null) throw new FormatException($"{name}: manca l'indirizzo di origine (* = ...)");
        return new Result((ushort)last.Origin.Value, last.Output.ToArray(), last.Defined);
    }

    sealed class Pass(Dictionary<string, int> previous, bool final, string name)
    {
        public readonly Dictionary<string, int> Defined = new(StringComparer.Ordinal);
        public readonly List<byte> Output = [];
        public int? Origin;
        int pc = -1;
        int lineNumber;
        string lineText = "";

        public void Run(string[] lines)
        {
            for (lineNumber = 1; lineNumber <= lines.Length; lineNumber++)
            {
                lineText = lines[lineNumber - 1];
                Statement(StripComment(lineText).Trim());
            }
        }

        FormatException Error(string message) => new($"{name}, riga {lineNumber}: {message}\n    {lineText.Trim()}");

        static string StripComment(string line)
        {
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '"') inQuotes = !inQuotes;
                else if ((line[i] == '\'') && !inQuotes && i + 2 < line.Length && line[i + 2] == '\'') i += 2; // 'c'
                else if (line[i] == ';' && !inQuotes) return line[..i];
            }
            return line;
        }

        void Statement(string text)
        {
            if (text.Length == 0) return;

            // Etichetta
            int colon = text.IndexOf(':');
            if (colon > 0 && IsIdentifier(text[..colon]))
            {
                Define(text[..colon], RequirePc());
                text = text[(colon + 1)..].Trim();
                if (text.Length == 0) return;
            }

            // Origine e costanti
            if (text.StartsWith('*'))
            {
                string rest = text[1..].TrimStart();
                if (!rest.StartsWith('=')) throw Error("atteso '* = indirizzo'");
                int address = Evaluate(rest[1..]) ?? throw Error("l'origine deve essere nota");
                if (pc < 0) { Origin = pc = address; return; }
                if (address < pc) throw Error("l'origine può solo andare avanti");
                while (pc < address) Emit(0xFF);
                return;
            }
            int equals = text.IndexOf('=');
            if (equals > 0 && IsIdentifier(text[..equals].Trim()))
            {
                var value = Evaluate(text[(equals + 1)..]);
                if (value is not null) Define(text[..equals].Trim(), value.Value);
                else if (final) throw Error("valore non definito");
                return;
            }

            if (text.StartsWith('.')) { Directive(text); return; }
            Instruction(text);
        }

        void Define(string symbol, int value)
        {
            if (Defined.ContainsKey(symbol)) throw Error($"simbolo già definito: {symbol}");
            Defined[symbol] = value;
        }

        int RequirePc() => pc >= 0 ? pc : throw Error("manca l'indirizzo di origine (* = ...) prima del codice");

        void Emit(int value)
        {
            RequirePc();
            if (final) Output.Add((byte)value);
            pc++;
        }

        void Directive(string text)
        {
            int space = text.IndexOfAny([' ', '\t']);
            string directive = (space < 0 ? text : text[..space]).ToLowerInvariant();
            string arguments = space < 0 ? "" : text[space..].Trim();

            switch (directive)
            {
                case ".byte":
                    foreach (var item in SplitArguments(arguments))
                    {
                        if (item.StartsWith('"')) foreach (char c in Unquote(item)) Emit(Petscii(c));
                        else Emit(Byte(Evaluate(item), item));
                    }
                    break;
                case ".word":
                    foreach (var item in SplitArguments(arguments))
                    {
                        int value = Evaluate(item) ?? (final ? throw Error($"valore non definito: {item}") : 0);
                        Emit(value & 0xFF);
                        Emit(value >> 8 & 0xFF);
                    }
                    break;
                case ".text":
                    foreach (char c in Unquote(arguments)) Emit(Petscii(c));
                    break;
                case ".screen":
                    foreach (char c in Unquote(arguments)) Emit(ScreenCode(c));
                    break;
                case ".fill":
                    {
                        var parts = SplitArguments(arguments);
                        int count = Evaluate(parts[0]) ?? throw Error("il numero di byte di .fill deve essere noto");
                        int value = parts.Count > 1 ? Byte(Evaluate(parts[1]), parts[1]) : 0;
                        for (int i = 0; i < count; i++) Emit(value);
                        break;
                    }
                case ".basic":
                    {
                        // 10 SYS<indirizzo>: collegamento, numero di riga, token SYS ($9E), cifre, fine riga, fine programma
                        int start = RequirePc();
                        int target = start + 12;
                        string digits = target.ToString(CultureInfo.InvariantCulture);
                        if (digits.Length != 4) throw Error(".basic richiede un indirizzo di 4 cifre (es. * = $0801)");
                        int next = start + 10;
                        Emit(next & 0xFF); Emit(next >> 8);
                        Emit(10); Emit(0);
                        Emit(0x9E);
                        foreach (char c in digits) Emit(c);
                        Emit(0); Emit(0); Emit(0);
                        break;
                    }
                default:
                    throw Error($"direttiva sconosciuta: {directive}");
            }
        }

        void Instruction(string text)
        {
            int space = text.IndexOfAny([' ', '\t']);
            string mnemonic = (space < 0 ? text : text[..space]).ToUpperInvariant();
            string operand = space < 0 ? "" : text[space..].Trim();
            if (!Opcodes.Keys.Any(k => k.Mnemonic == mnemonic)) throw Error($"istruzione sconosciuta: {mnemonic}");

            var (mode, expression) = ParseOperand(mnemonic, operand);
            int? value = expression is null ? null : Evaluate(expression);

            if (mode == Mode.Rel)
            {
                Emit(Opcodes[(mnemonic, Mode.Rel)]);
                int target = value ?? (final ? throw Error($"destinazione non definita: {expression}") : pc + 1);
                int offset = target - (pc + 1);
                if (final && (offset < -128 || offset > 127)) throw Error($"salto troppo lontano ({offset} byte)");
                Emit(offset & 0xFF);
                return;
            }

            // Pagina zero quando il valore è noto, sta in un byte e il modo esiste per l'istruzione
            if (value is >= 0 and < 0x100)
            {
                var zeroPage = mode switch { Mode.Abs => Mode.Zp, Mode.AbsX => Mode.ZpX, Mode.AbsY => Mode.ZpY, _ => mode };
                if (zeroPage != mode && Opcodes.ContainsKey((mnemonic, zeroPage))) mode = zeroPage;
            }
            if (!Opcodes.TryGetValue((mnemonic, mode), out byte opcode))
                throw Error($"{mnemonic} non ha il modo di indirizzamento {mode}");

            Emit(opcode);
            if (Size(mode) == 1) return;
            int operandValue = value ?? (final ? throw Error($"valore non definito: {expression}") : 0xFFFF);
            if (Size(mode) == 2)
            {
                if (final && (operandValue < -128 || operandValue > 0xFF)) throw Error($"valore oltre un byte: {expression}");
                Emit(operandValue & 0xFF);
            }
            else
            {
                Emit(operandValue & 0xFF);
                Emit(operandValue >> 8 & 0xFF);
            }
        }

        static readonly HashSet<string> Branches = ["BPL", "BMI", "BVC", "BVS", "BCC", "BCS", "BNE", "BEQ"];
        static readonly HashSet<string> Shifts = ["ASL", "LSR", "ROL", "ROR"];

        (Mode Mode, string? Expression) ParseOperand(string mnemonic, string operand)
        {
            if (operand.Length == 0) return (Shifts.Contains(mnemonic) ? Mode.Acc : Mode.Imp, null);
            if (operand.StartsWith('#')) return (Mode.Imm, operand[1..]);
            if (Branches.Contains(mnemonic)) return (Mode.Rel, operand);

            // Le forme indicizzate e indirette si riconoscono senza spazi: "( $FB ),Y" = "($FB),Y"
            string compact = operand.Replace(" ", "").Replace("\t", "");
            string upper = compact.ToUpperInvariant();
            if (upper == "A" && Shifts.Contains(mnemonic)) return (Mode.Acc, null);
            if (upper.StartsWith('(') && upper.EndsWith(",X)")) return (Mode.IndX, compact[1..^3]);
            if (upper.StartsWith('(') && upper.EndsWith("),Y")) return (Mode.IndY, compact[1..^3]);
            if (upper.StartsWith('(') && upper.EndsWith(')')) return (Mode.Ind, compact[1..^1]);
            if (upper.EndsWith(",X")) return (Mode.AbsX, compact[..^2]);
            if (upper.EndsWith(",Y")) return (Mode.AbsY, compact[..^2]);
            return (Mode.Abs, operand);
        }

        int Byte(int? value, string expression)
        {
            if (value is null) return final ? throw Error($"valore non definito: {expression}") : 0;
            if (final && (value < -128 || value > 0xFF)) throw Error($"valore oltre un byte: {expression}");
            return value.Value & 0xFF;
        }

        // Valore di un'espressione, o null se usa un simbolo non ancora definito.
        // <espressione e >espressione: byte basso e alto del valore intero; poi somme e differenze di prodotti
        int? Evaluate(string expression)
        {
            string text = expression.Trim();
            if (text.Length == 0) throw Error("espressione vuota");
            if (text[0] == '<') return Evaluate(text[1..]) is int low ? low & 0xFF : null;
            if (text[0] == '>') return Evaluate(text[1..]) is int high ? high >> 8 & 0xFF : null;

            var parser = new ExpressionParser(this, text);
            int? value = parser.Sum();
            parser.SkipSpaces();
            if (!parser.AtEnd) throw Error($"carattere inatteso in '{text}'");
            return value;
        }

        int? Symbol(string symbol)
        {
            int? value = Defined.TryGetValue(symbol, out var defined) ? defined
                : previous.TryGetValue(symbol, out var earlier) ? earlier
                : null;
            if (value is null && final) throw Error($"simbolo non definito: {symbol}");
            return value;
        }

        sealed class ExpressionParser(Pass pass, string text)
        {
            int i;

            public bool AtEnd => i >= text.Length;

            public void SkipSpaces()
            {
                while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            }

            bool Accept(char c)
            {
                SkipSpaces();
                if (i < text.Length && text[i] == c) { i++; return true; }
                return false;
            }

            public int? Sum()
            {
                int? value = Product();
                while (true)
                {
                    if (Accept('+')) value = Combine(value, Product(), (a, b) => a + b);
                    else if (Accept('-')) value = Combine(value, Product(), (a, b) => a - b);
                    else return value;
                }
            }

            int? Product()
            {
                int? value = Factor();
                while (true)
                {
                    if (Accept('*')) value = Combine(value, Factor(), (a, b) => a * b);
                    else if (Accept('/')) value = Combine(value, Factor(), (a, b) => b == 0 ? throw pass.Error("divisione per zero") : a / b);
                    else return value;
                }
            }

            static int? Combine(int? a, int? b, Func<int, int, int> operation) => a is null || b is null ? null : operation(a.Value, b.Value);

            int? Factor()
            {
                SkipSpaces();
                if (AtEnd) throw pass.Error($"espressione incompleta: '{text}'");
                char c = text[i];
                int start = i;

                if (c == '-') { i++; return Factor() is int negated ? -negated : null; }
                if (c == '(')
                {
                    i++;
                    int? inner = Sum();
                    if (!Accept(')')) throw pass.Error($"manca ')' in '{text}'");
                    return inner;
                }
                if (c == '*') { i++; return pass.RequirePc(); }
                if (c == '$')
                {
                    i++;
                    while (i < text.Length && Uri.IsHexDigit(text[i])) i++;
                    return int.Parse(text[(start + 1)..i], NumberStyles.HexNumber);
                }
                if (c == '%')
                {
                    i++;
                    while (i < text.Length && text[i] is '0' or '1') i++;
                    return Convert.ToInt32(text[(start + 1)..i], 2);
                }
                if (char.IsDigit(c))
                {
                    while (i < text.Length && char.IsDigit(text[i])) i++;
                    return int.Parse(text[start..i], CultureInfo.InvariantCulture);
                }
                if (c == '\'' && i + 2 < text.Length && text[i + 2] == '\'')
                {
                    i += 3;
                    return pass.Petscii(text[start + 1]);
                }
                if (char.IsLetter(c) || c == '_')
                {
                    while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) i++;
                    return pass.Symbol(text[start..i]);
                }
                throw pass.Error($"carattere inatteso '{c}' in '{text}'");
            }
        }

        List<string> SplitArguments(string text)
        {
            var items = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;
            foreach (char c in text)
            {
                if (c == '"') inQuotes = !inQuotes;
                if (c == ',' && !inQuotes) { items.Add(current.ToString().Trim()); current.Clear(); }
                else current.Append(c);
            }
            if (current.Length > 0 || items.Count > 0) items.Add(current.ToString().Trim());
            if (items.Count == 0 || items.Any(item => item.Length == 0)) throw Error("argomento mancante");
            return items;
        }

        string Unquote(string text)
        {
            text = text.Trim();
            if (text.Length < 2 || text[0] != '"' || text[^1] != '"') throw Error("attesa una stringa fra virgolette");
            return text[1..^1];
        }

        // PETSCII per il testo stampato con CHROUT: lettere maiuscole, cifre e punteggiatura come in ASCII
        int Petscii(char c)
        {
            char upper = char.ToUpperInvariant(c);
            if (upper > 0x7F) throw Error($"carattere non ASCII: '{c}'");
            return upper;
        }

        // Codici schermo (memoria video): @ = 0, A-Z = 1-26, spazio e simboli 32-63 come in PETSCII
        int ScreenCode(char c)
        {
            int petscii = Petscii(c);
            return petscii switch
            {
                >= 0x40 and <= 0x5F => petscii - 0x40,
                >= 0x20 and <= 0x3F => petscii,
                _ => throw Error($"carattere senza codice schermo: '{c}'"),
            };
        }

        static bool IsIdentifier(string text) =>
            text.Length > 0 && (char.IsLetter(text[0]) || text[0] == '_') && text.All(c => char.IsLetterOrDigit(c) || c == '_');
    }
}
