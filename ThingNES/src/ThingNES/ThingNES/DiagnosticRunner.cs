using System.Text;

namespace ThingNES;

public enum DiagnosticOutcome { Passed, Failed, TimedOut, ResetRequested, Cancelled }
public sealed record DiagnosticResult(DiagnosticOutcome Outcome, byte? Code, string Message, long Cycles, bool SignatureSeen)
{
    public int ExitCode => Outcome switch { DiagnosticOutcome.Passed => 0, DiagnosticOutcome.Failed => 2, DiagnosticOutcome.TimedOut => 3, DiagnosticOutcome.ResetRequested => 4, _ => 5 };
}

// Blargg text_out protocol. Read cartridge RAM directly so observation never changes CPU open bus.
public static class DiagnosticRunner
{
    public static DiagnosticResult Run(NesMachine machine, long maxCycles, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(machine);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCycles);
        long start = machine.CpuCycles;
        bool seen = false;
        for (long elapsed = 0; elapsed < maxCycles; elapsed++)
        {
            if (cancellationToken.IsCancellationRequested) return Result(DiagnosticOutcome.Cancelled, null);
            machine.Tick();
            if (!HasSignature(machine.Cartridge)) continue;
            seen = true;
            byte status = machine.Cartridge.ReadCpu(0x6000);
            if (status < 0x80) return Result(status == 0 ? DiagnosticOutcome.Passed : DiagnosticOutcome.Failed, status);
            // Automatic reset is deferred until the board exposes a faithful reset sequence.
            if (status == 0x81) return Result(DiagnosticOutcome.ResetRequested, status);
        }
        return Result(DiagnosticOutcome.TimedOut, null);

        DiagnosticResult Result(DiagnosticOutcome outcome, byte? code) =>
            new(outcome, code, seen ? ReadMessage(machine.Cartridge) : "", machine.CpuCycles - start, seen);
    }
    static bool HasSignature(Cartridge cart) => cart.ReadCpu(0x6001) == 0xde && cart.ReadCpu(0x6002) == 0xb0 && cart.ReadCpu(0x6003) == 0x61;
    static string ReadMessage(Cartridge cart)
    {
        var text = new StringBuilder();
        for (int address = 0x6004; address < 0x8000; address++)
        {
            byte value = cart.ReadCpu((ushort)address);
            if (value == 0) break;
            // Preserve line breaks but do not forward terminal escape/control sequences from untrusted ROMs.
            text.Append(value is 10 or 13 || value is >= 32 and <= 126 ? (char)value : '?');
        }
        return text.ToString();
    }
}
