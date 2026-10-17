namespace Thing65.Asm;

// The processor the source is written for: the 6510 of the C64 (all NMOS opcodes, undocumented ones included) or the
// CSG 4510 of the C65 (a 65CE02: the CMOS instructions, the Z register, base page, 16-bit stack and branches,
// plus MAP and EOM). Chosen with Assemble(cpu: ...), or in the source with "!cpu 4510" / ".cpu 6510"
public enum Cpu
{
    Mos6510,
    Csg4510,
}

public static partial class Assembler
{
    // The instructions of the 4510, in opcode order: mnemonic and addressing mode. The modes are those of the
    // 6502 table plus: bp (base page, what the 6502 calls zero page), izz = (bp),Z, stk = (d,SP),Y, rel16 (word branch),
    // bpr (bit test and branch: bp,target), inx = (abs,X), ind = (abs), imm16 (PHW #word).
    // RMB, SMB, BBR and BBS carry the bit in the opcode: here they are listed for bit 0 and the others follow
    // The same table, in the same order, is in MOS4510.Opcodes.cs: a test compares them
    internal static readonly string[] OpcodeTable4510 =
    [
        "BRK imp","ORA izx","CLE imp","SEE imp","TSB zp", "ORA zp", "ASL zp", "RMB zp", "PHP imp","ORA imm","ASL acc","TSY imp","TSB abs","ORA abs","ASL abs","BBR bpr",
        "BPL rel","ORA izy","ORA izz","BPL rel16","TRB zp","ORA zpx","ASL zpx","RMB zp", "CLC imp","ORA aby","INC acc","INZ imp","TRB abs","ORA abx","ASL abx","BBR bpr",
        "JSR abs","AND izx","JSR ind","JSR inx","BIT zp", "AND zp", "ROL zp", "RMB zp", "PLP imp","AND imm","ROL acc","TYS imp","BIT abs","AND abs","ROL abs","BBR bpr",
        "BMI rel","AND izy","AND izz","BMI rel16","BIT zpx","AND zpx","ROL zpx","RMB zp", "SEC imp","AND aby","DEC acc","DEZ imp","BIT abx","AND abx","ROL abx","BBR bpr",
        "RTI imp","EOR izx","NEG acc","ASR acc","ASR zp", "EOR zp", "LSR zp", "RMB zp", "PHA imp","EOR imm","LSR acc","TAZ imp","JMP abs","EOR abs","LSR abs","BBR bpr",
        "BVC rel","EOR izy","EOR izz","BVC rel16","ASR zpx","EOR zpx","LSR zpx","RMB zp", "CLI imp","EOR aby","PHY imp","TAB imp","MAP imp","EOR abx","LSR abx","BBR bpr",
        "RTS imp","ADC izx","RTN imm","BSR rel16","STZ zp", "ADC zp", "ROR zp", "RMB zp", "PLA imp","ADC imm","ROR acc","TZA imp","JMP ind","ADC abs","ROR abs","BBR bpr",
        "BVS rel","ADC izy","ADC izz","BVS rel16","STZ zpx","ADC zpx","ROR zpx","RMB zp", "SEI imp","ADC aby","PLY imp","TBA imp","JMP inx","ADC abx","ROR abx","BBR bpr",
        "BRA rel","STA izx","STA stk","BRA rel16","STY zp", "STA zp", "STX zp", "SMB zp", "DEY imp","BIT imm","TXA imp","STY abx","STY abs","STA abs","STX abs","BBS bpr",
        "BCC rel","STA izy","STA izz","BCC rel16","STY zpx","STA zpx","STX zpy","SMB zp", "TYA imp","STA aby","TXS imp","STX aby","STZ abs","STA abx","STZ abx","BBS bpr",
        "LDY imm","LDA izx","LDX imm","LDZ imm","LDY zp", "LDA zp", "LDX zp", "SMB zp", "TAY imp","LDA imm","TAX imp","LDZ abs","LDY abs","LDA abs","LDX abs","BBS bpr",
        "BCS rel","LDA izy","LDA izz","BCS rel16","LDY zpx","LDA zpx","LDX zpy","SMB zp", "CLV imp","LDA aby","TSX imp","LDZ abx","LDY abx","LDA abx","LDX aby","BBS bpr",
        "CPY imm","CMP izx","CPZ imm","DEW zp", "CPY zp", "CMP zp", "DEC zp", "SMB zp", "INY imp","CMP imm","DEX imp","ASW abs","CPY abs","CMP abs","DEC abs","BBS bpr",
        "BNE rel","CMP izy","CMP izz","BNE rel16","CPZ zp", "CMP zpx","DEC zpx","SMB zp", "CLD imp","CMP aby","PHX imp","PHZ imp","CPZ abs","CMP abx","DEC abx","BBS bpr",
        "CPX imm","SBC izx","LDA stk","INW zp", "CPX zp", "SBC zp", "INC zp", "SMB zp", "INX imp","SBC imm","NOP imp","ROW abs","CPX abs","SBC abs","INC abs","BBS bpr",
        "BEQ rel","SBC izy","SBC izz","BEQ rel16","PHW imm16","SBC zpx","INC zpx","SMB zp","SED imp","SBC aby","PLX imp","PLZ imp","PHW abs","SBC abx","INC abx","BBS bpr",
    ];

    sealed class CpuTable
    {
        public required Dictionary<(string Mnemonic, Mode Mode), byte> Opcodes { get; init; }
        public required HashSet<string> MnemonicSet { get; init; }
        public required Dictionary<string, string> Aliases { get; init; }
        public required HashSet<string> Branches { get; init; }        // 8-bit relative
        public required HashSet<string> WordBranches { get; init; }    // names of the 16-bit ones: LBPL... and BSR
        public required HashSet<string> Accumulator { get; init; }     // instructions that work on A without an operand
    }

    static readonly Lazy<CpuTable> Table6510Lazy = new(() => new CpuTable
    {
        Opcodes = BuildOpcodes(),
        MnemonicSet = [.. OpcodeTable.Select(entry => entry[..3]), .. Aliases.Keys],
        Aliases = Aliases,
        Branches = ["BPL", "BMI", "BVC", "BVS", "BCC", "BCS", "BNE", "BEQ"],
        WordBranches = [],
        Accumulator = ["ASL", "LSR", "ROL", "ROR"],
    });

    static readonly Lazy<CpuTable> Table4510Lazy = new(BuildTable4510);

    static CpuTable Table6510 => Table6510Lazy.Value;
    static CpuTable Table4510 => Table4510Lazy.Value;

    static CpuTable BuildTable4510()
    {
        var opcodes = new Dictionary<(string, Mode), byte>();
        var mnemonics = new HashSet<string>();
        for (int opcode = 0; opcode < 256; opcode++)
        {
            var parts = OpcodeTable4510[opcode].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var mode = ModeOf(parts[1]);
            opcodes.TryAdd((parts[0], mode), (byte)opcode);
            mnemonics.Add(parts[0]);
        }
        // RMB, SMB, BBR and BBS exist for the 8 bits: RMB0..RMB7 and so on
        foreach (string name in new[] { "RMB", "SMB", "BBR", "BBS" })
        {
            mnemonics.Remove(name);
            for (int bit = 0; bit < 8; bit++) mnemonics.Add(name + bit);
        }
        var wordBranches = new HashSet<string> { "BSR" };
        foreach (string branch in new[] { "BPL", "BMI", "BVC", "BVS", "BCC", "BCS", "BNE", "BEQ", "BRA" })
        {
            wordBranches.Add("L" + branch);
            mnemonics.Add("L" + branch);
        }
        mnemonics.Add("EOM"); // the NOP that ends a mapping sequence
        return new CpuTable
        {
            Opcodes = opcodes,
            MnemonicSet = mnemonics,
            Aliases = new Dictionary<string, string> { ["EOM"] = "NOP" },
            Branches = ["BPL", "BMI", "BVC", "BVS", "BCC", "BCS", "BNE", "BEQ", "BRA"],
            WordBranches = wordBranches,
            Accumulator = ["ASL", "LSR", "ROL", "ROR", "ASR", "INC", "DEC", "NEG"],
        };
    }

    static Mode ModeOf(string text) => text switch
    {
        "imp" => Mode.Imp, "acc" => Mode.Acc, "imm" => Mode.Imm, "zp" => Mode.Zp, "zpx" => Mode.ZpX, "zpy" => Mode.ZpY,
        "abs" => Mode.Abs, "abx" => Mode.AbsX, "aby" => Mode.AbsY, "izx" => Mode.IndX, "izy" => Mode.IndY,
        "rel" => Mode.Rel, "ind" => Mode.Ind,
        "izz" => Mode.IndZ, "stk" => Mode.StackY, "rel16" => Mode.Rel16, "bpr" => Mode.BpRel, "imm16" => Mode.Imm16, "inx" => Mode.IndAbsX,
        _ => throw new InvalidOperationException($"unknown mode {text}"),
    };

    static CpuTable TableFor(Cpu cpu) => cpu == Cpu.Csg4510 ? Table4510 : Table6510;

    // Instructions the processor knows (for the editor's highlighting), with the 8 bit variants of RMB/SMB/BBR/BBS
    public static IReadOnlyList<string> MnemonicsFor(Cpu cpu) =>
        cpu == Cpu.Mos6510 ? Mnemonics : [.. Table4510.MnemonicSet.Order()];

    // The processor named in a "!cpu" directive
    public static Cpu ParseCpu(string name) => name.Trim().Trim('"').ToLowerInvariant() switch
    {
        "6502" or "6510" or "nmos6502" or "c64" => Cpu.Mos6510,
        "4510" or "csg4510" or "4502" or "65ce02" or "c65" => Cpu.Csg4510,
        _ => throw new FormatException($"processor not supported: {name} (6510 or 4510)"),
    };
}
