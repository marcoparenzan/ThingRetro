using Thing65.Prg;
using static Thing65.Tests.TestHelpers;

namespace Thing65.Tests;

[TestClass]
public sealed class ProgramLoadingTests
{
    const long CYCLES_PER_SECOND = 985248;
    const int FRAME_CYCLES = 312 * 63;

    // The programs come from the samples in samples/ (sources in samples/src)
    static byte[] Sample(string fileName) => SampleFile(fileName);

    private static Board BootedBoard()
    {
        var board = NewBoard();
        board.RunCycles(3 * CYCLES_PER_SECOND);
        return board;
    }

    private static string Screen(Board board) => string.Join("\n", board.ScreenLines());

    [TestMethod]
    public void LoadPrg_ShouldCopyBytesAtLoadAddress()
    {
        var board = NewBoard();

        var (start, end) = board.LoadPrg(new byte[] { 0x00, 0xC0, 0x11, 0x22, 0x33 });

        Assert.AreEqual(0xC000, (int)start);
        Assert.AreEqual(0xC003, (int)end);
        Assert.AreEqual(0x11, (int)board.Peek(0xC000));
        Assert.AreEqual(0x33, (int)board.Peek(0xC002));
    }

    [TestMethod]
    public void LoadPrg_ShouldWriteRamUnderRom()
    {
        var board = NewBoard();

        board.LoadPrg(new byte[] { 0x00, 0xA0, 0x42 });

        board.SetCpuPort(0x36); // BASIC ROM switched off
        Assert.AreEqual(0x42, (int)board.Peek(0xA000));
    }

    [TestMethod]
    public void LoadPrg_ShouldRejectTooShortOrOverflowingFiles()
    {
        var board = NewBoard();

        Assert.ThrowsException<ArgumentException>(() => board.LoadPrg(new byte[] { 0x01, 0x08 }));
        Assert.ThrowsException<ArgumentException>(() => board.LoadPrg(new byte[] { 0xFF, 0xFF, 0x01, 0x02 }));
    }

    [TestMethod]
    public void LoadPrg_BasicProgram_ShouldUpdateBasicPointers()
    {
        var board = BootedBoard();

        var (start, end) = board.LoadPrg(Sample("math.prg"));

        Assert.IsTrue(board.IsBasicProgram(start));
        Assert.AreEqual((int)end, board.Peek(0x2D) | board.Peek(0x2E) << 8); // VARTAB
        Assert.AreEqual((int)end, board.Peek(0x31) | board.Peek(0x32) << 8); // STREND
    }

    [TestMethod]
    public void LoadPrg_BasicProgram_ShouldListAndRun()
    {
        var board = BootedBoard();
        board.LoadPrg(Sample("math.prg"));

        board.TypeText("LIST\r");
        board.RunCycles(CYCLES_PER_SECOND / 2);
        board.TypeText("RUN\r");
        board.RunCycles(CYCLES_PER_SECOND / 2);

        var screen = Screen(board);
        StringAssert.Contains(screen, "10 PRINT 6*7\n");   // line 20 contains ^, the C64 up arrow
        StringAssert.Contains(screen, "30 PRINT \"PI\";4*ATN(1)");
        StringAssert.Contains(screen, "RUN\n 42\n 1024\n");
    }

    [TestMethod]
    public void LoadPrg_MachineCode_ShouldRunWithSys()
    {
        var board = BootedBoard();
        var (start, _) = board.LoadPrg(Sample("sys.prg"));
        Assert.AreEqual(0xC000, (int)start);
        Assert.IsFalse(board.IsBasicProgram(start));

        board.TypeText("SYS49152\r");
        board.RunCycles(CYCLES_PER_SECOND / 2);

        Assert.AreEqual(0, board.Peek(0xD020) & 0x0F);
        Assert.AreEqual(11, board.Peek(0xD021) & 0x0F);
        StringAssert.Contains(Screen(board), "HELLO FROM $C000! (SYS 49152)\n\nREADY.");
    }

    [TestMethod]
    public void RasterBars_ShouldSplitTheBorderAndKeepBasicAlive()
    {
        var board = BootedBoard();
        board.LoadPrg(Sample("rasterbars.prg"));
        board.TypeText("RUN\r");
        board.RunCycles(CYCLES_PER_SECOND / 2);
        board.RunCycles(FRAME_CYCLES); // one full frame with the raster bars active

        const byte RED = 2, LIGHT_BLUE = 14;
        byte BorderAt(int rasterLine) => board.Vic.PixelAt(0, rasterLine - 9);
        Assert.AreEqual(LIGHT_BLUE, BorderAt(98));
        Assert.AreEqual(RED, BorderAt(102));
        Assert.AreEqual(RED, BorderAt(148));
        Assert.AreEqual(LIGHT_BLUE, BorderAt(152));

        // The KERNAL keeps receiving the CIA1 IRQ: keyboard and BASIC still work
        board.TypeText("?1+1\r");
        board.RunCycles(CYCLES_PER_SECOND / 2);
        StringAssert.Contains(Screen(board), "?1+1\n 2\n");
    }

    [TestMethod]
    public void RasterBars_ShouldStayOnTheSameLinesFrameAfterFrame()
    {
        // The color change falls in the middle of a line: it must stay still instead of moving whole lines.
        // A few different frames are allowed, when the CIA IRQ delays the raster one (as on the real C64)
        var board = BootedBoard();
        board.LoadPrg(Sample("rasterbars.prg"));
        board.TypeText("RUN\r");
        board.RunCycles(CYCLES_PER_SECOND / 2);

        var topLines = new List<int>();
        for (int frame = 0; frame < 100; frame++)
        {
            board.RunCycles(FRAME_CYCLES);
            topLines.Add(Enumerable.Range(90, 40).First(line => board.Vic.PixelAt(0, line - 9) == 2));
        }

        int mostCommon = topLines.GroupBy(line => line).OrderByDescending(group => group.Count()).First().Key;
        Assert.IsTrue(topLines.Count(line => line == mostCommon) >= 95, string.Join(",", topLines));
    }

    [TestMethod]
    public void TypeText_ShouldRejectMoreThanTenCharacters()
    {
        var board = NewBoard();

        Assert.ThrowsException<ArgumentException>(() => board.TypeText("12345678901"));
    }
}
