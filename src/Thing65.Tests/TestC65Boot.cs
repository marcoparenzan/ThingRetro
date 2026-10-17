namespace Thing65.Tests;

// The real C65 ROM (not in the repository: each user installs their own, see the README). Without it: Inconclusive
[TestClass]
public sealed class C65BootTests
{
    static readonly string RomFile = Path.Combine(AppContext.BaseDirectory, "roms", "c-65-19910828.rom");

    const int BootCycles = 9_000_000; // about 9 s of the machine: the memory test and the banner take 7

    static BoardC65 NewBoard()
    {
        if (!File.Exists(RomFile)) Assert.Inconclusive("C65 ROM missing: copy c-65-19910828.rom into src/Thing65.Computer/roms");
        return new BoardC65(new SystemRom128(File.ReadAllBytes(RomFile)), startClock: false);
    }

    static BoardC65 BootedBoard()
    {
        var board = NewBoard();
        board.RunCycles(BootCycles);
        return board;
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

    // Types the text and runs the machine until it has been typed and the ROM has had time to act on it
    static void Type(BoardC65 board, string text, int settleCycles = 600_000)
    {
        board.TypeText(text);
        while (board.IsTyping) board.RunCycles(1000);
        board.RunCycles(settleCycles);
    }

    const string CR = "\r";

    [TestMethod]
    [Timeout(120_000)]
    public void Boot_ShouldReachTheReadyPromptOfBasic10()
    {
        var board = BootedBoard();

        string screen = Screen(board);
        StringAssert.Contains(screen, "BASIC 10.0");
        StringAssert.Contains(screen, "READY.");
        StringAssert.Contains(screen, "NO C1565 DRIVE"); // no disk in the drive
    }

    [TestMethod]
    [Timeout(120_000)]
    public void Boot_ShouldKeepRunningAfterReady()
    {
        var board = BootedBoard();
        var cpu = (MOS4510)board.CpuSocket.Chip!;

        long before = cpu.InstructionCount;
        board.RunCycles(1_000_000);

        Assert.IsTrue(cpu.InstructionCount - before > 100_000, "BASIC waits for a key in its input loop");
    }

    [TestMethod]
    [Timeout(120_000)]
    public void Basic_ShouldPrintInDirectMode()
    {
        var board = BootedBoard();

        Type(board, "PRINT 2+3" + CR);
        Type(board, "PRINT\"HELLO\"" + CR);

        string screen = Screen(board);
        StringAssert.Contains(screen, " 5 ");
        StringAssert.Contains(screen, "HELLO");
    }

    [TestMethod]
    [Timeout(120_000)]
    public void Basic_ShouldPokeIntoMemory()
    {
        var board = BootedBoard();
        board.Ram(0x1000, 0);

        Type(board, "POKE 4096,65" + CR);

        Assert.AreEqual(65, board.Ram(0x1000));
    }

    [TestMethod]
    [Timeout(120_000)]
    public void Basic_ShouldRunAProgram()
    {
        var board = BootedBoard();

        Type(board, "10 FOR I=1 TO 3:PRINT I:NEXT" + CR);
        Type(board, "RUN" + CR, 1_500_000);

        string screen = Screen(board);
        StringAssert.Contains(screen, " 1 ");
        StringAssert.Contains(screen, " 2 ");
        StringAssert.Contains(screen, " 3 ");
    }

    // 10 PRINT"HI" as BASIC 10 stores it: load address $2001, line link, line number, tokens
    static readonly byte[] HiProgram = [0x01, 0x20, 0x0B, 0x20, 0x0A, 0x00, 0x99, 0x22, 0x48, 0x49, 0x22, 0x00, 0x00, 0x00];

    [TestMethod]
    [Timeout(120_000)]
    public void Disk_ShouldShowTheDirectory()
    {
        var board = BootedBoard();
        board.Disk = D81Writer.Create("HI", HiProgram);

        Type(board, "DIRECTORY" + CR, 2_000_000);

        string screen = Screen(board);
        StringAssert.Contains(screen, "\"HI\"");
        StringAssert.Contains(screen, "PRG");
    }

    [TestMethod]
    [Timeout(120_000)]
    public void Disk_ShouldLoadAndRunAProgram()
    {
        var board = BootedBoard();
        board.Disk = D81Writer.Create("HI", HiProgram);

        Type(board, "DLOAD\"HI\"" + CR, 3_000_000);
        Type(board, "RUN" + CR, 1_000_000);

        StringAssert.Contains(Screen(board), "HI");
        var lines = Screen(board).Split(Environment.NewLine);
        Assert.IsTrue(lines.Any(line => line.TrimStart().StartsWith("HI")), Screen(board));
    }

    // The first program assembled for the 4510 runs on the C65: from source to screen
    [TestMethod]
    [Timeout(120_000)]
    public void Assembler_ShouldMakeAProgramThatRunsOnTheC65()
    {
        string source = Path.Combine(TestHelpers.SamplesFolder, "c65", "hello65.asm");
        var program = Thing65.Build.ProgramBuilder.Assemble(source).Prg;
        Assert.AreEqual(0x00, program[0]); // load address $1800
        Assert.AreEqual(0x18, program[1]);

        var board = BootedBoard();
        board.Disk = D81Writer.Create("HELLO65", program);
        Type(board, "BLOAD\"HELLO65\",B0" + CR, 3_000_000);
        Type(board, "SYS 6144" + CR, 1_000_000);

        string screen = Screen(board);
        StringAssert.Contains(screen, "HELLO FROM THE 4510! INW COUNTED 3");
    }

    sealed class TracingCpu : I4510Pinout
    {
        public readonly MOS4510 Inner = new();
        public readonly List<string> Log = [];
        public bool Enabled;
        long last;
        public IMappedBus? Bus { get => Inner.Bus; set => Inner.Bus = value; }
        public Func<bool> IrqInput { get => Inner.IrqInput; set => Inner.IrqInput = value; }
        public Func<bool> NmiInput { get => Inner.NmiInput; set => Inner.NmiInput = value; }
        public Func<bool> AecInput { get => Inner.AecInput; set => Inner.AecInput = value; }
        public Func<bool> DmaInput { get => Inner.DmaInput; set => Inner.DmaInput = value; }
        public void Reset() => Inner.Reset();
        public void Tick()
        {
            Inner.Tick();
            if (Enabled && Inner.IsInstructionBoundary && Inner.InstructionCount != last)
            {
                last = Inner.InstructionCount;
                Log.Add(Inner.DebugInfo.Item2);
            }
        }
    }

    // The 40-column text screen of the C64 mode: characters at $0400
    static string Screen64(BoardC65 board)
    {
        var text = new System.Text.StringBuilder();
        for (int row = 0; row < 25; row++)
        {
            for (int column = 0; column < 40; column++)
            {
                byte code = board.Ram(0x0400 + row * 40 + column);
                text.Append(code is >= 1 and <= 26 ? (char)('A' + code - 1) : code is >= 0x20 and < 0x60 ? (char)code : '.');
            }
            text.AppendLine();
        }
        return text.ToString();
    }

    [TestMethod]
    [Timeout(120_000)]
    public void Go64_ShouldBootTheC64ModeAndRunBasicV2()
    {
        var board = BootedBoard();
        Type(board, "GO64" + CR, 1_000_000);
        Type(board, "Y" + CR, 6_000_000);

        StringAssert.Contains(Screen64(board), "**** COMMODORE 64 BASIC V2 ****");
        StringAssert.Contains(Screen64(board), "38911 BASIC BYTES FREE");

        Type(board, "PRINT 2+3" + CR);
        StringAssert.Contains(Screen64(board), " 5 ");
        Assert.IsFalse(board.VicSocket.Chip!.NewMode, "C64 mode is the VIC-II mode");
    }

    // The slow one: HEADER formats 160 tracks, a revolution (200 ms of the machine) each
    [TestMethod]
    [Timeout(600_000)]
    public void Disk_ShouldBeFormattedWrittenAndReadOnAnotherMachine()
    {
        var board = BootedBoard();
        var disk = D81Image.Blank();
        board.Disk = disk;

        Type(board, "DIRECTORY" + CR, 1_500_000);
        Assert.IsFalse(Screen(board).Contains("BLOCKS FREE"), "an unformatted disk has no directory");

        Type(board, "HEADER\"TEST\",I65,D0" + CR, 500_000);
        Type(board, "Y" + CR, 160_000_000);
        Type(board, "DIRECTORY" + CR, 3_000_000);
        StringAssert.Contains(Screen(board), "3160 BLOCKS FREE");

        Type(board, "10 PRINT\"SAVED\"" + CR);
        Type(board, "DSAVE\"KEEP\"" + CR, 6_000_000);
        Type(board, "DIRECTORY" + CR, 3_000_000);
        StringAssert.Contains(Screen(board), "\"KEEP\"");
        Assert.IsTrue(disk.Modified);

        // The disk is a file now: another machine reads it
        var copy = new D81Image((byte[])disk.Data.Clone());
        var other = BootedBoard();
        other.Disk = copy;
        Type(other, "DLOAD\"KEEP\"" + CR, 3_000_000);
        Type(other, "RUN" + CR, 1_000_000);
        StringAssert.Contains(Screen(other), "SAVED");
    }
}
