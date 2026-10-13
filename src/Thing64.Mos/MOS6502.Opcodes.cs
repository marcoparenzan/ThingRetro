namespace Thing64.Mos;

// Table of the 256 opcodes of the NMOS 6510, undocumented ones included: mnemonic and addressing mode.
// The kind of access (read, write, read-modify-write...) follows from the operation
public partial class MOS6502
{
    internal enum Mode { Imp, Acc, Imm, Zp, ZpX, ZpY, Abs, AbsX, AbsY, IndX, IndY, Rel, Ind }

    internal enum Kind { Implied, Read, Write, ReadModifyWrite, Branch, Special }

    internal enum Op
    {
        ADC, AND, ASL, BCC, BCS, BEQ, BIT, BMI, BNE, BPL, BRK, BVC, BVS, CLC, CLD, CLI, CLV, CMP, CPX, CPY,
        DEC, DEX, DEY, EOR, INC, INX, INY, JMP, JSR, LDA, LDX, LDY, LSR, NOP, ORA, PHA, PHP, PLA, PLP,
        ROL, ROR, RTI, RTS, SBC, SEC, SED, SEI, STA, STX, STY, TAX, TAY, TSX, TXA, TXS, TYA,
        // undocumented
        SLO, RLA, SRE, RRA, SAX, LAX, DCP, ISC, ANC, ALR, ARR, XAA, AXS, LXA, LAS, SHA, SHX, SHY, TAS, JAM,
    }

    internal readonly record struct Instruction(Op Op, Mode Mode, Kind Kind);

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

    internal static readonly Instruction[] Instructions = BuildInstructions();

    static Instruction[] BuildInstructions()
    {
        var instructions = new Instruction[256];
        for (int opcode = 0; opcode < 256; opcode++)
        {
            var parts = OpcodeTable[opcode].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var op = Enum.Parse<Op>(parts[0]);
            var mode = parts[1] switch
            {
                "imp" => Mode.Imp, "acc" => Mode.Acc, "imm" => Mode.Imm, "zp" => Mode.Zp, "zpx" => Mode.ZpX,
                "zpy" => Mode.ZpY, "abs" => Mode.Abs, "abx" => Mode.AbsX, "aby" => Mode.AbsY, "izx" => Mode.IndX,
                "izy" => Mode.IndY, "rel" => Mode.Rel, "ind" => Mode.Ind,
                _ => throw new InvalidOperationException($"Unknown mode for opcode ${opcode:X2}"),
            };
            instructions[opcode] = new Instruction(op, mode, KindOf(op, mode));
        }
        return instructions;
    }

    static Kind KindOf(Op op, Mode mode)
    {
        if (mode == Mode.Rel) return Kind.Branch;
        if (op is Op.BRK or Op.JSR or Op.RTS or Op.RTI or Op.PHA or Op.PHP or Op.PLA or Op.PLP or Op.JMP or Op.JAM)
            return Kind.Special;
        if (mode is Mode.Imp or Mode.Acc) return Kind.Implied;
        if (op is Op.STA or Op.STX or Op.STY or Op.SAX or Op.SHA or Op.SHX or Op.SHY or Op.TAS) return Kind.Write;
        if (op is Op.ASL or Op.LSR or Op.ROL or Op.ROR or Op.INC or Op.DEC or Op.SLO or Op.RLA or Op.SRE or Op.RRA or Op.DCP or Op.ISC)
            return Kind.ReadModifyWrite;
        return Kind.Read;
    }
}
