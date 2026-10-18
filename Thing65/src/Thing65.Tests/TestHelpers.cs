using System.Text;
using ThingCommodore.Software;

namespace Thing65.Tests;

static class TestHelpers
{
    public const byte FLAG_C = 0x01;
    public const byte FLAG_Z = 0x02;
    public const byte FLAG_I = 0x04;
    public const byte FLAG_D = 0x08;
    public const byte FLAG_B = 0x10;
    public const byte FLAG_V = 0x40;
    public const byte FLAG_N = 0x80;

    // The real ROMs, copied from the app's roms folder next to the tests; read only once
    static readonly Lazy<SystemRoms> Roms = new(() =>
    {
        string folder = Path.Combine(AppContext.BaseDirectory, "roms");
        return new SystemRoms(
            kernal: File.ReadAllBytes(Path.Combine(folder, "kernal")),
            basic: File.ReadAllBytes(Path.Combine(folder, "basic")),
            chargen: File.ReadAllBytes(Path.Combine(folder, "chargen")));
    });

    // The samples folder of the repository (samples/, next to src/) and the files in it
    public static string SamplesFolder { get; } = FindSamples();

    static string FindSamples()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            string samples = Path.Combine(folder.FullName, "samples");
            if (File.Exists(Path.Combine(samples, "samples.json"))) return samples;
        }
        throw new DirectoryNotFoundException("samples folder not found above " + AppContext.BaseDirectory);
    }

    public static byte[] SampleFile(string fileName) => File.ReadAllBytes(Path.Combine(SamplesFolder, fileName));

    // Board without the real-time clock: the tests advance with RunCycles/StepInstruction
    public static Board NewBoard() => new Board(Roms.Value, startClock: false);

    // The stock chips in the sockets, with their concrete type to read their internal state
    extension(Board board)
    {
        public MOS6510 Cpu => (MOS6510)board.CpuSocket.Chip!;
        public MOS6526 Cia1 => (MOS6526)board.Cia1Socket.Chip!;
        public MOS6526 Cia2 => (MOS6526)board.Cia2Socket.Chip!;
        public MOS6569 Vic => (MOS6569)board.VicSocket.Chip!;
        public MOS6581 Sid => (MOS6581)board.SidSocket.Chip!;

        // Runs cycles until the current instruction completes and the next one starts
        public void StepInstruction()
        {
            var cpu = board.Cpu;
            long start = cpu.InstructionCount;
            do board.RunCycles(1); while (cpu.InstructionCount == start || !cpu.IsInstructionBoundary);
        }
    }

    public static void Poke(this Board board, ushort addr, params byte[] bytes)
    {
        foreach (var b in bytes) board.Address(addr++, b);
    }

    public static byte Peek(this Board board, ushort addr) => board.Address(addr);

    // Sets up the 6510 port as the KERNAL does ($00 = $2F: P0-P3 and P5 as outputs) and writes $01.
    // After reset the port is all input: without the direction, writing $01 does not change the pins
    public static void SetCpuPort(this Board board, byte value)
    {
        board.Poke(0x0000, 0x2F);
        board.Poke(0x0001, value);
    }

    // Loads a program into RAM and puts the CPU on its first instruction with IRQs disabled
    public static void Load(this Board board, ushort addr, params byte[] program)
    {
        // Like a debugger: the PC is changed only between two instructions, not in the middle of one
        while (!board.Cpu.IsInstructionBoundary) board.RunCycles(1);
        board.Poke(addr, program);
        board.Cpu.PC = addr;
        board.Cpu.Status = 0x24; // I + bit 5
    }

    // Keeps the CPU in a loop (JMP $C000) so that the KERNAL does not touch the chip registers
    public static void ParkCpu(this Board board)
    {
        board.Load(0xC000, 0x4C, 0x00, 0xC0);
    }

    public static long CyclesOf(this Board board, Action step)
    {
        long before = board.TotalCycles;
        step();
        return board.TotalCycles - before;
    }

    // Converts video memory ($0400) into text, one string per row
    public static string[] ScreenLines(this Board board)
    {
        var lines = new string[25];
        for (int row = 0; row < 25; row++)
        {
            var sb = new StringBuilder();
            for (int col = 0; col < 40; col++)
            {
                int c = board.Peek((ushort)(0x0400 + row * 40 + col)) & 0x7F;
                sb.Append(c == 0 ? '@' : c < 27 ? (char)('A' + c - 1) : c < 64 ? (char)c : '.');
            }
            lines[row] = sb.ToString().TrimEnd();
        }
        return lines;
    }

    // Puts text into the KERNAL keyboard buffer (at most 10 characters)
    public static void Type(this Board board, string text) => board.TypeText(text);
}
