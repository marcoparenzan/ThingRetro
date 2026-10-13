using static Fake64.Tests.TestHelpers;

namespace Fake64.Tests;

// Integration tests: the real KERNAL and BASIC, run cycle by cycle without the real-time clock
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
        // The keyboard buffer holds 10 characters: longer lines are typed in pieces
        for (int i = 0; i < line.Length; i += 9)
        {
            board.Type(line.Substring(i, Math.Min(9, line.Length - i)));
            board.RunCycles(CYCLES_PER_SECOND / 10);
        }
        board.Type("\r");
        board.RunCycles(CYCLES_PER_SECOND / 2);
    }

    [TestMethod]
    public void SystemRoms_ShouldRejectWrongSizes()
    {
        var rom8k = new byte[0x2000];
        var rom4k = new byte[0x1000];

        Assert.ThrowsException<ArgumentException>(() => new SystemRoms(rom4k, rom8k, rom4k));
        Assert.ThrowsException<ArgumentException>(() => new SystemRoms(rom8k, rom4k, rom4k));
        Assert.ThrowsException<ArgumentException>(() => new SystemRoms(rom8k, rom8k, rom8k));
        Assert.ThrowsException<ArgumentNullException>(() => new SystemRoms(null!, rom8k, rom4k));
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

    [TestMethod]
    public void Stop_ShouldEndTheRealTimeClock()
    {
        var board = NewBoard();
        board.Start();
        Thread.Sleep(200);
        board.Stop();
        long cycles = board.TotalCycles;
        Thread.Sleep(100);

        Assert.IsTrue(cycles > 0, "the clock never ran");
        Assert.AreEqual(cycles, board.TotalCycles);   // stopped: no more cycles
    }

    // Measures time: it runs alone, other tests in parallel would slow it down
    [TestMethod]
    [DoNotParallelize]
    public void FastForward_ShouldBootWithoutWaitingForRealTime()
    {
        var board = NewBoard();
        board.Start();
        var done = new ManualResetEventSlim();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        Exception? failure = null;
        board.Enqueue(() =>
        {
            try
            {
                board.FastForward(TimeSpan.FromSeconds(3));
            }
            catch (Exception e)
            {
                failure = e;
            }
            done.Set();
        });

        Assert.IsTrue(done.Wait(TimeSpan.FromSeconds(10)), "not run");
        Assert.IsNull(failure, failure?.ToString());
        board.Stop();
        Assert.IsTrue(clock.Elapsed < TimeSpan.FromSeconds(3), $"fast forward took {clock.Elapsed}");
        CollectionAssert.Contains(board.ScreenLines(), "READY.");
    }
}
