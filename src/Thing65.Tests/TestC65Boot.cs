namespace Thing65.Tests;

// The real C65 ROM (not in the repository: each user installs their own, see the README). Without it: Inconclusive
[TestClass]
public sealed class C65BootTests
{
    static readonly string RomFile = Path.Combine(AppContext.BaseDirectory, "roms", "c-65-19910828.rom");

    static BoardC65 NewBoard()
    {
        if (!File.Exists(RomFile)) Assert.Inconclusive("C65 ROM missing: copy c-65-19910828.rom into src/Thing65.Computer/roms");
        return new BoardC65(new SystemRom128(File.ReadAllBytes(RomFile)), startClock: false);
    }

    // The 80x25 text screen of the C65 mode: characters at $0800 as screen codes
    static string Screen(BoardC65 board, int columns = 80, int rows = 25)
    {
        var text = new System.Text.StringBuilder();
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                byte code = board.Ram(0x0800 + row * columns + column);
                text.Append(code is >= 1 and <= 26 ? (char)('A' + code - 1) : code is >= 0x20 and < 0x60 ? (char)code : '.');
            }
            text.AppendLine();
        }
        return text.ToString();
    }

    [TestMethod]
    [Timeout(120_000)]
    public void Boot_ShouldReachTheReadyPromptOfBasic10()
    {
        var board = NewBoard();

        board.RunCycles(9_000_000); // about 9 s of the machine: the memory test and the banner take 7

        string screen = Screen(board);
        StringAssert.Contains(screen, "BASIC 10.0");
        StringAssert.Contains(screen, "READY.");
        StringAssert.Contains(screen, "NO C1565 DRIVE"); // no disk in the drive
    }

    [TestMethod]
    [Timeout(120_000)]
    public void Boot_ShouldKeepRunningAfterReady()
    {
        var board = NewBoard();
        var cpu = (MOS4510)board.CpuSocket.Chip!;
        board.RunCycles(9_000_000);

        long before = cpu.InstructionCount;
        board.RunCycles(1_000_000);

        Assert.IsTrue(cpu.InstructionCount - before > 100_000, "BASIC waits for a key in its input loop");
    }

    [TestMethod]
    [Timeout(120_000)]
    public unsafe void Snapshot_ToFile()
    {
        var board = NewBoard();
        board.RunCycles(9_000_000);
        var vic = board.VicSocket.Chip!;
        var bitmap = new System.Drawing.Bitmap(CSG4567.SCREEN_WIDTH, CSG4567.SCREEN_HEIGHT, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var data = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, bitmap.PixelFormat);
        vic.Render((byte*)data.Scan0);
        bitmap.UnlockBits(data);
        bitmap.Save(Environment.GetEnvironmentVariable("C65_SNAPSHOT") ?? Path.GetTempFileName() + ".png", System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine($"D011={vic.Read(0x11):X2} D016={vic.Read(0x16):X2} D018={vic.Read(0x18):X2} D030={vic.ControlA:X2} D031={vic.ControlB:X2} newmode={vic.NewMode}");
    }
}
