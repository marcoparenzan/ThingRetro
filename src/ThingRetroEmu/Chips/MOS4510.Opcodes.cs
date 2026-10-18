namespace ThingRetroEmu.Chips;

// Table of the 256 opcodes of the CSG 4510 (65CE02 core): mnemonic and addressing mode.
// Unlike the NMOS 6502 there are no undocumented opcodes: all 256 are defined.
// Reference: "C65 Specifications", chapter 2.3.7 (the 65CE02 instruction set)
public partial class MOS4510
{
    internal enum Mode
    {
        Imp, Acc, Imm, Imm16,
        Bp, BpX, BpY,                 // base page: the B register supplies the high byte of the address
        Abs, AbsX, AbsY,
        IndX, IndY, IndZ,             // (bp,X)  (bp),Y  (bp),Z
        StackY,                       // (d,SP),Y
        Rel, Rel16,                   // 8 and 16 bit relative (branches, BSR)
        BpRel,                        // bp,rel (BBR/BBS)
        AbsInd, AbsXInd,              // (abs)  (abs,X)  (JMP and JSR only)
    }

    internal enum Op
    {
        ADC, AND, ASL, ASR, ASW, BBR, BBS, BCC, BCS, BEQ, BIT, BMI, BNE, BPL, BRA, BRK, BSR, BVC, BVS,
        CLC, CLD, CLE, CLI, CLV, CMP, CPX, CPY, CPZ, DEC, DEW, DEX, DEY, DEZ, EOR, INC, INW, INX, INY, INZ,
        JMP, JSR, LDA, LDX, LDY, LDZ, LSR, MAP, NEG, NOP, ORA, PHA, PHP, PHW, PHX, PHY, PHZ,
        PLA, PLP, PLX, PLY, PLZ, RMB, ROL, ROR, ROW, RTI, RTN, RTS, SBC, SEC, SED, SEE, SEI, SMB,
        STA, STX, STY, STZ, TAB, TAX, TAY, TAZ, TBA, TRB, TSB, TSX, TSY, TXA, TXS, TYA, TYS, TZA,
    }

    internal readonly record struct Instruction(Op Op, Mode Mode);

    // RMB/SMB/BBR/BBS carry the bit number in the opcode (bits 4-6): "RMB bp" is $07 + 16 * bit
    static readonly string[] OpcodeTable =
    [
        "BRK imp","ORA izx","CLE imp","SEE imp","TSB bp", "ORA bp", "ASL bp", "RMB bp", "PHP imp","ORA imm","ASL acc","TSY imp","TSB abs","ORA abs","ASL abs","BBR bpr",
        "BPL rel","ORA izy","ORA izz","BPL rel16","TRB bp","ORA bpx","ASL bpx","RMB bp", "CLC imp","ORA aby","INC acc","INZ imp","TRB abs","ORA abx","ASL abx","BBR bpr",
        "JSR abs","AND izx","JSR ind","JSR inx","BIT bp", "AND bp", "ROL bp", "RMB bp", "PLP imp","AND imm","ROL acc","TYS imp","BIT abs","AND abs","ROL abs","BBR bpr",
        "BMI rel","AND izy","AND izz","BMI rel16","BIT bpx","AND bpx","ROL bpx","RMB bp", "SEC imp","AND aby","DEC acc","DEZ imp","BIT abx","AND abx","ROL abx","BBR bpr",
        "RTI imp","EOR izx","NEG acc","ASR acc","ASR bp", "EOR bp", "LSR bp", "RMB bp", "PHA imp","EOR imm","LSR acc","TAZ imp","JMP abs","EOR abs","LSR abs","BBR bpr",
        "BVC rel","EOR izy","EOR izz","BVC rel16","ASR bpx","EOR bpx","LSR bpx","RMB bp", "CLI imp","EOR aby","PHY imp","TAB imp","MAP imp","EOR abx","LSR abx","BBR bpr",
        "RTS imp","ADC izx","RTN imm","BSR rel16","STZ bp", "ADC bp", "ROR bp", "RMB bp", "PLA imp","ADC imm","ROR acc","TZA imp","JMP ind","ADC abs","ROR abs","BBR bpr",
        "BVS rel","ADC izy","ADC izz","BVS rel16","STZ bpx","ADC bpx","ROR bpx","RMB bp", "SEI imp","ADC aby","PLY imp","TBA imp","JMP inx","ADC abx","ROR abx","BBR bpr",
        "BRA rel","STA izx","STA stk","BRA rel16","STY bp", "STA bp", "STX bp", "SMB bp", "DEY imp","BIT imm","TXA imp","STY abx","STY abs","STA abs","STX abs","BBS bpr",
        "BCC rel","STA izy","STA izz","BCC rel16","STY bpx","STA bpx","STX bpy","SMB bp", "TYA imp","STA aby","TXS imp","STX aby","STZ abs","STA abx","STZ abx","BBS bpr",
        "LDY imm","LDA izx","LDX imm","LDZ imm","LDY bp", "LDA bp", "LDX bp", "SMB bp", "TAY imp","LDA imm","TAX imp","LDZ abs","LDY abs","LDA abs","LDX abs","BBS bpr",
        "BCS rel","LDA izy","LDA izz","BCS rel16","LDY bpx","LDA bpx","LDX bpy","SMB bp", "CLV imp","LDA aby","TSX imp","LDZ abx","LDY abx","LDA abx","LDX aby","BBS bpr",
        "CPY imm","CMP izx","CPZ imm","DEW bp", "CPY bp", "CMP bp", "DEC bp", "SMB bp", "INY imp","CMP imm","DEX imp","ASW abs","CPY abs","CMP abs","DEC abs","BBS bpr",
        "BNE rel","CMP izy","CMP izz","BNE rel16","CPZ bp", "CMP bpx","DEC bpx","SMB bp", "CLD imp","CMP aby","PHX imp","PHZ imp","CPZ abs","CMP abx","DEC abx","BBS bpr",
        "CPX imm","SBC izx","LDA stk","INW bp", "CPX bp", "SBC bp", "INC bp", "SMB bp", "INX imp","SBC imm","NOP imp","ROW abs","CPX abs","SBC abs","INC abs","BBS bpr",
        "BEQ rel","SBC izy","SBC izz","BEQ rel16","PHW imm16","SBC bpx","INC bpx","SMB bp","SED imp","SBC aby","PLX imp","PLZ imp","PHW abs","SBC abx","INC abx","BBS bpr",
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
                "imp" => Mode.Imp, "acc" => Mode.Acc, "imm" => Mode.Imm, "imm16" => Mode.Imm16,
                "bp" => Mode.Bp, "bpx" => Mode.BpX, "bpy" => Mode.BpY,
                "abs" => Mode.Abs, "abx" => Mode.AbsX, "aby" => Mode.AbsY,
                "izx" => Mode.IndX, "izy" => Mode.IndY, "izz" => Mode.IndZ, "stk" => Mode.StackY,
                "rel" => Mode.Rel, "rel16" => Mode.Rel16, "bpr" => Mode.BpRel,
                "ind" => Mode.AbsInd, "inx" => Mode.AbsXInd,
                _ => throw new InvalidOperationException($"Unknown mode for opcode ${opcode:X2}"),
            };
            instructions[opcode] = new Instruction(op, mode);
        }
        return instructions;
    }

    // Cycles of an implied/accumulator instruction (opcode fetch included): one by default
    // (the 65CE02 does not waste a dummy cycle), more for those listed in the manual
    static int ImpliedCycles(Op op, Mode mode) => op switch
    {
        Op.NEG or Op.CLE or Op.SEE or Op.CLI or Op.SEI => 2,
        Op.ASR => 2, // ASR A
        Op.MAP => 4,
        _ => 1,
    };
}
