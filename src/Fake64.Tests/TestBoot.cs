using static Fake64.Tests.TestHelpers;

namespace Fake64.Tests;

// Test di integrazione: KERNAL e BASIC reali, eseguiti ciclo per ciclo senza clock in tempo reale
[TestClass]
public sealed class BootTests
{
    const long CYCLES_PER_SECOND = 985248; // PAL
    const long BOOT_CYCLES = 3 * CYCLES_PER_SECOND;

    private static Board BootedBoard()
    {
        var board = NewBoard();
        board.RunCycles(BOOT_CYCLES);
        return board;
    }

    private static void Enter(Board board, string line)
    {
        // Il buffer di tastiera contiene 10 caratteri: le righe più lunghe vengono inserite a pezzi
        for (int i = 0; i < line.Length; i += 9)
        {
            board.Type(line.Substring(i, Math.Min(9, line.Length - i)));
            board.RunCycles(CYCLES_PER_SECOND / 10);
        }
        board.Type("\r");
        board.RunCycles(CYCLES_PER_SECOND / 2);
    }

    [TestMethod]
    public void Boot_ShouldShowBannerAndReady()
    {
        var board = BootedBoard();

        var screen = board.ScreenLines();

        Assert.AreEqual("    **** COMMODORE 64 BASIC V2 ****", screen[1]);
        Assert.AreEqual(" 64K RAM SYSTEM  38911 BASIC BYTES FREE", screen[3]);
        Assert.AreEqual("READY.", screen[5]);
    }

    [TestMethod]
    public void JiffyClock_ShouldTickSixtyTimesPerSecond()
    {
        var board = BootedBoard();
        int Jiffy() => board.Peek(0xA0) << 16 | board.Peek(0xA1) << 8 | board.Peek(0xA2);

        int before = Jiffy();
        board.RunCycles(CYCLES_PER_SECOND);

        Assert.AreEqual(60, Jiffy() - before, 1);
    }

    [TestMethod]
    public void Basic_ShouldEvaluateExpressionsAndLoops()
    {
        var board = BootedBoard();

        Enter(board, "?2+3");
        Enter(board, "A=7:?A*6");
        Enter(board, "FORI=1TO3:?I;:NEXT");

        var screen = string.Join("\n", board.ScreenLines());
        StringAssert.Contains(screen, "?2+3\n 5\n");
        StringAssert.Contains(screen, "A=7:?A*6\n 42\n");
        StringAssert.Contains(screen, "FORI=1TO3:?I;:NEXT\n 1  2  3\n");
    }

    [TestMethod]
    public void Basic_ShouldRunStoredProgram()
    {
        var board = BootedBoard();

        Enter(board, "10 S=0");
        Enter(board, "20 FORI=1TO10:S=S+I:NEXT");
        Enter(board, "30 PRINTS");
        Enter(board, "RUN");

        var screen = string.Join("\n", board.ScreenLines());
        StringAssert.Contains(screen, "RUN\n 55\n");
    }
}
