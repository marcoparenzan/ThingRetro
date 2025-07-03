using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;

namespace Fake64;

public partial class MOS6510
{
    Dictionary<ushort, string> rom_debugging;


    public void LogInstructionText(ushort instruction_PC, byte opcode, string instruction_text)
    {
        if (rom_debugging is null)
        {
            rom_debugging = new();
            var lines = File.ReadAllLines("c64disasm_en.txt");

            ushort? pc = null;
            var sb = new StringBuilder();

            var i = 0;
            foreach (var line in lines)
            {
                if (line.StartsWith("."))
                {
                    if (pc is not null)
                    {
                        rom_debugging.Add(pc.Value, sb.ToString());
                    }
                    sb.Clear();
                    var pcstring = line.Substring(2, 4);
                    pc = ushort.Parse(pcstring, System.Globalization.NumberStyles.HexNumber);
                }
                sb.AppendLine(line);
                Console.WriteLine($"{i:X4} {line}");
                i++;
            }
            if (pc is not null)
            {
                rom_debugging.Add(pc.Value, sb.ToString());
            }
        }
        Console.WriteLine($"[{instruction_PC:X4}] {opcode:x2} {instruction_text} | A={A:X2}, X={X:X2}, Y={Y:X2}, SP={SP:X2}, PC={PC:X4}, Status={Status:X2} | Cycles: {cycleCount}");
        if (rom_debugging.TryGetValue(instruction_PC, out var value))
        {
            Console.WriteLine(value);
        }
    }
}