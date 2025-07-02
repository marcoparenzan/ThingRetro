using System;
using System.Collections.Generic;

namespace Chips;

public partial class MOS6510
{
    private HashSet<ushort> breakpoints = new(); // Breakpoint virtuali
    private bool loggingEnabled = true; // Abilita/disabilita il logging
    private bool breakpointHit = false; // Indica se un breakpoint è stato raggiunto

    private string logFilePath = "cpu_log.txt";


    private void LogState(byte opcode)
    {
        string logMessage = $"[LOG] PC={PC:X4}, Opcode={opcode:X2}, A={A:X2}, X={X:X2}, Y={Y:X2}, SP={SP:X2}, Status={Status:X2}";
        Console.WriteLine(logMessage);

        // Salva il log su file
        System.IO.File.AppendAllText(logFilePath, logMessage + Environment.NewLine);
    }

    public void AddBreakpoint(ushort address)
    {
        breakpoints.Add(address);
    }

    public void RemoveBreakpoint(ushort address)
    {
        breakpoints.Remove(address);
    }

    public void EnableLogging(bool enable)
    {
        loggingEnabled = enable;
    }
    public bool IsBreakpointHit() => breakpointHit;

}