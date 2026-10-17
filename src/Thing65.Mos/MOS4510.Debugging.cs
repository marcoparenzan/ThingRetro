namespace Thing65.Mos;

// Last instruction executed, disassembled only when the debugger asks for it
public partial class MOS4510
{
    ushort instructionPC;

    public (ushort, string) DebugInfo =>
        (instructionPC, $"[{instructionPC:X4}] {opcode:x2} {Disassemble(opcode, operandLow, operandHigh, instructionPC)} | A={A:X2}, X={X:X2}, Y={Y:X2}, Z={Z:X2}, B={B:X2}, SP={SP:X4}, PC={PC:X4}, Status={Status:X2}");

    internal static string Disassemble(byte opcode, byte low, byte high, ushort pc)
    {
        var instruction = Instructions[opcode];
        string op = instruction.Op.ToString();
        if (instruction.Op is Op.RMB or Op.SMB or Op.BBR or Op.BBS) op += (opcode >> 4) & 7;
        if (instruction.Op == Op.NOP) op = "EOM";
        if (instruction.Mode == Mode.Rel16 && instruction.Op != Op.BSR) op = "L" + op; // LBPL... as the assembler writes them
        ushort word = (ushort)(high << 8 | low);
        return instruction.Mode switch
        {
            Mode.Imp => op,
            Mode.Acc => $"{op} A",
            Mode.Imm => $"{op} #${low:X2}",
            Mode.Imm16 => $"{op} #${word:X4}",
            Mode.Bp => $"{op} ${low:X2}",
            Mode.BpX => $"{op} ${low:X2},X",
            Mode.BpY => $"{op} ${low:X2},Y",
            Mode.Abs => $"{op} ${word:X4}",
            Mode.AbsX => $"{op} ${word:X4},X",
            Mode.AbsY => $"{op} ${word:X4},Y",
            Mode.IndX => $"{op} (${low:X2},X)",
            Mode.IndY => $"{op} (${low:X2}),Y",
            Mode.IndZ => $"{op} (${low:X2}),Z",
            Mode.StackY => $"{op} (${low:X2},SP),Y",
            Mode.Rel => $"{op} ${(ushort)(pc + 2 + (sbyte)low):X4}",
            Mode.Rel16 => $"{op} ${(ushort)(pc + 2 + (short)word):X4}",
            Mode.BpRel => $"{op} ${low:X2},${(ushort)(pc + 3 + (sbyte)high):X4}",
            Mode.AbsInd => $"{op} (${word:X4})",
            Mode.AbsXInd => $"{op} (${word:X4},X)",
            _ => op,
        };
    }
}
