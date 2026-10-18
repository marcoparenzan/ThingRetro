using System.Text;
using Thing64.C64;
using Thing64.Prg;
using ThingCommodore.Drives;
using ThingRetroEmu.Buses;
using ThingRetroEmu.Media;

namespace ThingCommodore.Tests;

// A Thing64 and a 1581 on the same IEC cable: the real KERNAL, the real 1581 DOS, a .d81 disk
[TestClass]
public sealed class C64With1581Tests
{
    const long ComputerHz = 985_248;

    static byte[] Rom(string name)
    {
        var file = Path.Combine(AppContext.BaseDirectory, "roms", name);
        if (!File.Exists(file)) Assert.Inconclusive($"ROM missing: {file}");
        return File.ReadAllBytes(file);
    }

    // 10 PRINT"HELLO FROM 1581"
    static readonly byte[] Hello =
    [
        0x01, 0x08,
        0x1C, 0x08, 0x0A, 0x00, 0x99, 0x22, 0x48, 0x45, 0x4C, 0x4C, 0x4F, 0x20, 0x46, 0x52, 0x4F, 0x4D, 0x20, 0x31, 0x35, 0x38, 0x31, 0x22, 0x00,
        0x00, 0x00,
    ];

    static Board NewComputer(D81Image? disk, int device = 8)
    {
        var board = new Board(new SystemRoms(Rom("c-64-kernal.rom"), Rom("c-64-basic.rom"), Rom("c-64-chargen.rom")), startClock: false);
        if (disk is not null)
        {
            var drive = new Board1581(Rom("c-drive-1581.rom")) { DeviceNumber = device, Disk = disk };
            var cable = new IecBus();
            cable.Attach(() => board.Iec.AtnLow(), () => board.Iec.ClkLow(), () => board.Iec.DataLow(),
                input => board.Iec.ClkInputLow = input, input => board.Iec.DataInputLow = input);
            drive.Plug(cable);
            board.AfterCycle = new DriveClock(ComputerHz, drive.Tick, Board1581.CLOCK_HZ).OnComputerCycle;
        }
        board.RunCycles(4 * ComputerHz);
        return board;
    }

    // Types a line like a person, in pieces: the keyboard buffer holds 10 characters
    static void Enter(Board board, string line, long settleCycles)
    {
        for (int i = 0; i < line.Length; i += 9)
        {
            board.TypeText(line.Substring(i, Math.Min(9, line.Length - i)));
            board.RunCycles(ComputerHz / 10);
        }
        board.TypeText("\r");
        board.RunCycles(settleCycles);
    }

    static string Screen(Board board)
    {
        var text = new StringBuilder();
        for (int row = 0; row < 25; row++)
        {
            for (int column = 0; column < 40; column++)
            {
                byte code = (byte)(board.Address((ushort)(0x0400 + row * 40 + column)) & 0x7F);
                text.Append(code is >= 1 and <= 26 ? (char)('A' + code - 1) : code is >= 0x20 and < 0x60 ? (char)code : '.');
            }
            text.AppendLine();
        }
        return text.ToString();
    }

    static D81Image DiskWithProgram() => D81Writer.Create("BASIC 1581", [("HELLO", Hello)]);

    [TestMethod]
    [Timeout(600_000)]
    public void Directory_ShouldListTheDisk()
    {
        var computer = NewComputer(DiskWithProgram());

        Enter(computer, "LOAD\"$\",8", 10 * ComputerHz);
        Enter(computer, "LIST", ComputerHz);

        var screen = Screen(computer);
        StringAssert.Contains(screen, "BASIC 1581");
        StringAssert.Contains(screen, "\"HELLO\"");
        StringAssert.Contains(screen, "BLOCKS FREE");
    }

    [TestMethod]
    [Timeout(600_000)]
    public void Load_ShouldBringTheProgramAndRunIt()
    {
        var computer = NewComputer(DiskWithProgram());

        Enter(computer, "LOAD\"HELLO\",8", 10 * ComputerHz);
        Enter(computer, "RUN", 2 * ComputerHz);

        StringAssert.Contains(Screen(computer), "HELLO FROM 1581");
    }

    [TestMethod]
    [Timeout(600_000)]
    public void Load_FromDevice9_ShouldAnswerOnlyToItsNumber()
    {
        var computer = NewComputer(DiskWithProgram(), device: 9);

        Enter(computer, "LOAD\"HELLO\",8", 3 * ComputerHz);
        Assert.IsTrue(Screen(computer).Contains("DEVICE NOT PRESENT"), "nothing is device 8");
        Enter(computer, "LOAD\"HELLO\",9", 10 * ComputerHz);
        Enter(computer, "RUN", 2 * ComputerHz);

        StringAssert.Contains(Screen(computer), "HELLO FROM 1581");
    }

    [TestMethod]
    [Timeout(900_000)]
    public void Save_ShouldWriteTheProgramToTheDisk()
    {
        var disk = DiskWithProgram();
        var computer = NewComputer(disk);

        Enter(computer, "10 PRINT\"SAVED\"", ComputerHz / 2);
        Enter(computer, "SAVE\"NEWFILE\",8", 15 * ComputerHz);
        Enter(computer, "LOAD\"$\",8", 10 * ComputerHz);
        Enter(computer, "LIST", ComputerHz);

        Assert.IsTrue(disk.Modified, "the DOS wrote to the disk");
        var screen = Screen(computer);
        StringAssert.Contains(screen, "\"NEWFILE\"");
        StringAssert.Contains(screen, "\"HELLO\"");
    }

    [TestMethod]
    [Timeout(900_000)]
    public void Save_ThenLoadAfterAReset_ShouldGiveTheProgramBack()
    {
        var disk = DiskWithProgram();
        var computer = NewComputer(disk);
        Enter(computer, "10 PRINT\"ROUND TRIP\"", ComputerHz / 2);
        Enter(computer, "SAVE\"TRIP\",8", 15 * ComputerHz);

        // a new machine with the same disk: whatever was written is on it
        var second = NewComputer(new D81Image(disk.Data));
        Enter(second, "LOAD\"TRIP\",8", 10 * ComputerHz);
        Enter(second, "RUN", 2 * ComputerHz);

        StringAssert.Contains(Screen(second), "ROUND TRIP");
    }

    [TestMethod]
    [Timeout(900_000)]
    public void Format_ShouldMakeABlankDiskUsable()
    {
        var disk = D81Image.Blank();
        var computer = NewComputer(disk);

        Enter(computer, "OPEN15,8,15,\"N:FRESH,01\":CLOSE15", 240 * ComputerHz);   // 160 tracks, each written and verified
        Enter(computer, "LOAD\"$\",8", 10 * ComputerHz);
        Enter(computer, "LIST", ComputerHz);

        var screen = Screen(computer);
        StringAssert.Contains(screen, "FRESH");
        StringAssert.Contains(screen, "3160 BLOCKS FREE");
    }

    [TestMethod]
    [Timeout(600_000)]
    public void StatusChannel_ShouldAnswerWithTheDosVersion()
    {
        var computer = NewComputer(DiskWithProgram());

        Enter(computer, "10 OPEN15,8,15:INPUT#15,A,B$,C,D:PRINT A;B$;C;D", ComputerHz / 2);
        Enter(computer, "RUN", 5 * ComputerHz);

        StringAssert.Contains(Screen(computer), "73 COPYRIGHT CBM DOS V10 1581");
    }

    [TestMethod]
    [Timeout(600_000)]
    public void Directory_WithoutADisk_ShouldReportTheDriveNotReady()
    {
        var board = new Board(new SystemRoms(Rom("c-64-kernal.rom"), Rom("c-64-basic.rom"), Rom("c-64-chargen.rom")), startClock: false);
        var drive = new Board1581(Rom("c-drive-1581.rom"));
        var cable = new IecBus();
        cable.Attach(() => board.Iec.AtnLow(), () => board.Iec.ClkLow(), () => board.Iec.DataLow(),
            input => board.Iec.ClkInputLow = input, input => board.Iec.DataInputLow = input);
        drive.Plug(cable);
        board.AfterCycle = new DriveClock(ComputerHz, drive.Tick, Board1581.CLOCK_HZ).OnComputerCycle;
        board.RunCycles(4 * ComputerHz);

        Enter(board, "10 OPEN1,8,0,\"$\"", ComputerHz / 2);
        Enter(board, "20 OPEN15,8,15:INPUT#15,A,B$,C,D:PRINT A;B$;C;D", ComputerHz / 2);
        Enter(board, "RUN", 6 * ComputerHz);

        StringAssert.Contains(Screen(board), "74 DRIVE NOT READY");
    }
}
