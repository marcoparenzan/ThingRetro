namespace Thing65.Mos;

// Last instruction executed, disassembled only when the debugger asks for it
public partial class MOS6502
{
    ushort instructionPC;
    byte lastOpcode;
    byte operandLow;
    byte operandHigh;

    public (ushort, string) DebugInfo =>
        (instructionPC, $"[{instructionPC:X4}] {lastOpcode:x2} {Disassemble(lastOpcode, operandLow, operandHigh, instructionPC)} | A={A:X2}, X={X:X2}, Y={Y:X2}, SP={SP:X2}, PC={PC:X4}, Status={Status:X2}");

    internal static string Disassemble(byte opcode, byte low, byte high, ushort pc)
    {
        var instruction = Instructions[opcode];
        string op = instruction.Op.ToString();
        ushort word = (ushort)(high << 8 | low);
        return instruction.Mode switch
        {
            Mode.Imp => op,
            Mode.Acc => $"{op} A",
            Mode.Imm => $"{op} #${low:X2}",
            Mode.Zp => $"{op} ${low:X2}",
            Mode.ZpX => $"{op} ${low:X2},X",
            Mode.ZpY => $"{op} ${low:X2},Y",
            Mode.Abs => $"{op} ${word:X4}",
            Mode.AbsX => $"{op} ${word:X4},X",
            Mode.AbsY => $"{op} ${word:X4},Y",
            Mode.IndX => $"{op} (${low:X2},X)",
            Mode.IndY => $"{op} (${low:X2}),Y",
            Mode.Ind => $"{op} (${word:X4})",
            Mode.Rel => $"{op} ${(ushort)(pc + 2 + (sbyte)low):X4}",
            _ => op,
        };
    }
}
