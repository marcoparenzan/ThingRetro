using Fake64.Mos;
using System.Globalization;
using System.Text;

namespace Fake64.C64;

public partial class Board
{
    Dictionary<ushort, string> rom_debugging;
    int debugging;

    public void DebugPause()
    {
        if (debugging == 0) debugging = 1;
        else if (debugging == 1) debugging = 0;
    }

    public void StepPause()
    {
        debugging = -1;
    }

    public void DebugCPU(CancellationTokenSource cts, TextWriter writer)
    {
        if (rom_debugging is null)
        {
            rom_debugging = new();
            var lines = File.ReadAllLines("c64disasm_en.txt");

            string pcstring = null;
            var sb = new StringBuilder();

            var i = 0;
            ushort pc = 0;
            foreach (var line in lines)
            {
                if (line.StartsWith("."))
                {
                    if (pcstring is not null)
                    {
                        rom_debugging.Add(pc, sb.ToString());
                    }
                    sb.Clear();
                    pcstring = line.Substring(2, 4);
                    if (ushort.TryParse(pcstring, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsedPc))
                    {
                        pc = parsedPc;
                    }
                    else
                    {
                        continue;
                    }
                }
                sb.AppendLine(line);
                i++;
            }
            if (pcstring is not null)
            {
                rom_debugging.Add(pc, sb.ToString());
            }
        }

        (ushort pc, string info) lastInfo = default;
        (ushort pc, string info) info = default;

        if (writer == Console.Out)
        {
            Console.Clear();
        }
        while (!cts.IsCancellationRequested)
        {
            if (cpu is not MOS6510 mos6510) return;
            info = mos6510.DebugInfo;
            if (info == lastInfo) continue;
            lastInfo = info;

            writer.WriteLine(lastInfo.info);
            if (rom_debugging.TryGetValue(lastInfo.pc, out var value))
            {
                writer.WriteLine(value);
            }

            if (lastInfo.pc == 0xff58)
            {
                DebugPause();
            }

            Wait();
        }
    }

    private void Wait()
    {
        while (debugging>0)
        {
            Task.Delay(100).Wait();
            continue;
        }
        if (debugging == -1)
        {
            debugging = 1;
        }
    }

    public void DebugCIA1(CancellationTokenSource cts, TextWriter writer)
    {
        Console.Clear();
        while (!cts.IsCancellationRequested)
        {
            (cia1 as MOS6526)?.Debug(writer);
            Wait();
        }
    }

    public void DebugCIA2(CancellationTokenSource cts, TextWriter writer)
    {
        Console.Clear();
        while (!cts.IsCancellationRequested)
        {
            (cia2 as MOS6526)?.Debug(writer);
            Wait();
        }
    }
}
