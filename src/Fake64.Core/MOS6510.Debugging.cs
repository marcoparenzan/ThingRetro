using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;

namespace Fake64;

public partial class MOS6510
{
    // I valori vengono solo memorizzati: la stringa viene costruita quando il debugger la legge
    ushort instruction_PC;
    byte debugOpcode;
    string debugText = string.Empty;
    byte debugCycles, debugA, debugX, debugY, debugSP, debugStatus;
    ushort debugPC;

    public (ushort, string) DebugInfo => (instruction_PC,
        $"[{instruction_PC:X4}] {debugOpcode:x2} {debugText} | A={debugA:X2}, X={debugX:X2}, Y={debugY:X2}, SP={debugSP:X2}, PC={debugPC:X4}, Status={debugStatus:X2} | Cycles: {debugCycles}");

    public void LogInstructionText(ushort instruction_PC, byte opcode, string instruction_text, byte cyclesCount)
    {
        this.instruction_PC = instruction_PC;
        debugOpcode = opcode;
        debugText = instruction_text;
        debugCycles = cyclesCount;
        debugA = A; debugX = X; debugY = Y; debugSP = SP; debugPC = PC; debugStatus = Status;
    }
}