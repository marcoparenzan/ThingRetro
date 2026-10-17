namespace Thing65.Asm;

// Multi-pass 6502/6510 assembler, used by the samples and by the app's editor. It understands two dialects that can
// be mixed freely: the project's own, with "." directives, and ACME's, with "!" pseudo opcodes, so that ACME sources
// assemble to the same bytes.
//
//   ; comment                        to the end of the line
//   * = $0801                        program counter (segments can go anywhere; gaps are filled, see !initmem)
//   BORDER = $D020                   constant
//   loop:   LDA #<text               label (the colon is optional) and instruction; ':' also separates statements
//   .local, @cheap, -, +             local symbols (zone or macro), cheap locals (between global labels),
//                                    anonymous labels ("-" backward, "+" forward; "--", "++"... are other names)
//
// Project directives (C64 conventions):
//   .byte $01, 2, "AB"   bytes and PETSCII strings        .word loop          16-bit words
//   .text "HELLO"        PETSCII text (upper case)        .screen "HELLO"     screen codes (A = 1)
//   .fill 8, $00         a byte repeated                  .basic              BASIC line "10 SYSnnnn" (at $0801)
// ACME pseudo opcodes (same meaning as in ACME 0.97):
//   !byte !by !8 !08, !word !wo !16 !le16 !be16 !24 !32 ..., !hex, !fill !fi, !skip, !align, !text !tx, !raw, !pet,
//   !scr, !scrxor, !convtab !ct, !source !src, !binary !bin, !zone !zn, !if/else, !ifdef, !ifndef, !for, !set,
//   !macro and +calls, !initmem, !error, !warn, !info; !to, !cpu and !sl are accepted and ignored
//
// Numbers: $hex, 0x hex, %binary (also with . and #), 0b binary, &octal, decimal, floats (3.14), 'c'.
// Operators with ACME's priorities: functions (sin cos tan arcsin arccos arctan int float), !/NOT, ^ (power),
// unary -, * / DIV % MOD, + -, << >> >>> (ASL LSL ASR LSR), unary < > ^ (low, high, bank byte), comparisons,
// &/AND, XOR, |/OR. Floats follow ACME's rules (see AllPOs/Floats in the ACME docs).
// Zero page is chosen automatically. Symbols are case-sensitive, mnemonics are not.
// All 6510 opcodes, the undocumented ones included
public static partial class Assembler
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

    // Other names of the undocumented opcodes, as used by ACME and other assemblers
    static readonly Dictionary<string, string> Aliases = new()
    {
        ["ASR"] = "ALR", ["SBX"] = "AXS", ["ANE"] = "XAA", ["ISB"] = "ISC", ["AHX"] = "SHA", ["SHS"] = "TAS",
        ["LAE"] = "LAS", ["DCM"] = "DCP", ["INS"] = "ISC", ["LSE"] = "SRE", ["AXA"] = "SHA", ["KIL"] = "JAM",
    };

    static readonly Dictionary<(string Mnemonic, Mode Mode), byte> Opcodes = BuildOpcodes();
    static readonly HashSet<string> MnemonicSet = [.. OpcodeTable.Select(entry => entry[..3]), .. Aliases.Keys];

    static Dictionary<(string, Mode), byte> BuildOpcodes()
    {
        var opcodes = new Dictionary<(string, Mode), byte>
        {
            [("NOP", Mode.Imp)] = 0xEA, // the documented forms take precedence over the duplicates
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

    static bool IsMnemonic(string word) => word.Length == 3 && MnemonicSet.Contains(word.ToUpperInvariant());

    // Known instructions and directives (also used by the editor for syntax highlighting)
    public static IReadOnlyList<string> Mnemonics { get; } = OpcodeTable.Select(entry => entry[..3]).Distinct().Order().ToArray();

    public static IReadOnlyList<string> Directives { get; } =
    [
        ".basic", ".byte", ".fill", ".screen", ".text", ".word", ".initmem", ".source", ".include", ".binary",
        "!8", "!08", "!by", "!byte", "!16", "!wo", "!word", "!le16", "!be16", "!24", "!le24", "!be24", "!32", "!le32", "!be32",
        "!hex", "!h", "!fill", "!fi", "!skip", "!align", "!text", "!tx", "!raw", "!pet", "!scr", "!scrxor", "!convtab", "!ct",
        "!source", "!src", "!binary", "!bin", "!bi", "!zone", "!zn", "!if", "!ifdef", "!ifndef", "!for", "!set", "!macro",
        "!initmem", "!error", "!serious", "!warn", "!info", "!to", "!cpu", "!sl", "!symbollist",
    ];

    // Result: the bytes from the lowest to the highest address written, and the symbol table (global symbols)
    public sealed record Result(ushort Origin, byte[] Bytes, IReadOnlyDictionary<string, int> Symbols)
    {
        // .prg file: load address followed by the bytes
        public byte[] Prg => [(byte)Origin, (byte)(Origin >> 8), .. Bytes];
    }

    // Assembles a source. name is used in error messages; directory (usually the folder of the source file) is
    // where !source and !binary look for their files, and must be given for sources that include other files.
    // Errors are thrown as AssemblerException (a FormatException) with file and line
    public static Result Assemble(string source, string name = "source", string? directory = null)
    {
        var files = new SourceFiles(directory);
        var root = files.Parse(source, name, directory is null ? null : Path.Combine(directory, name));
        var previous = new PassState();

        // Several passes: forward references, and the choice between zero page and absolute addressing changes the
        // sizes, and so the label addresses, until they settle
        for (int pass = 0; pass < 20; pass++)
        {
            var state = new Pass(files, previous, final: false);
            state.Run(root);
            bool stable = state.State.SameAs(previous);
            previous = state.State;
            if (stable && pass > 0) break;
        }

        var last = new Pass(files, previous, final: true);
        last.Run(root);
        return last.Result(name);
    }
}
