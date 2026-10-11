using System.Text;

namespace Fake64.Tests;

static class TestHelpers
{
    public const byte FLAG_C = 0x01;
    public const byte FLAG_Z = 0x02;
    public const byte FLAG_I = 0x04;
    public const byte FLAG_D = 0x08;
    public const byte FLAG_B = 0x10;
    public const byte FLAG_V = 0x40;
    public const byte FLAG_N = 0x80;

    // Board senza clock in tempo reale: i test avanzano con RunCycles/StepInstruction
    public static Board NewBoard() => new Board(null, startClock: false);

    public static void Poke(this Board board, ushort addr, params byte[] bytes)
    {
        foreach (var b in bytes) board.Address(addr++, b);
    }

    public static byte Peek(this Board board, ushort addr) => board.Address(addr);

    // Carica un programma in RAM e posiziona la CPU sulla prima istruzione con gli IRQ disabilitati
    public static void Load(this Board board, ushort addr, params byte[] program)
    {
        board.Poke(addr, program);
        board.Cpu.PC = addr;
        board.Cpu.Status = 0x24; // I + bit 5
    }

    // Tiene la CPU in un loop (JMP $C000) così il KERNAL non tocca i registri dei chip
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

    // Converte la memoria video ($0400) in testo, una stringa per riga
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

    // Inserisce testo nel buffer di tastiera del KERNAL (max 10 caratteri)
    public static void Type(this Board board, string text) => board.TypeText(text);
}
