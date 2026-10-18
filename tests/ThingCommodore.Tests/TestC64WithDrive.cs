using Thing64.C64;
using Thing64.Prg;

namespace ThingCommodore.Tests;

// A Thing64 (a real C64 KERNAL on a 6510) and a 1541 on the same IEC cable: BASIC's own LOAD, SAVE and DIRECTORY
[TestClass]
public sealed class C64WithDriveTests
{
    const long ComputerHz = 985_248;

    static byte[] Rom(string name)
    {
        var file = Path.Combine(AppContext.BaseDirectory, "roms", name);
        if (!File.Exists(file)) Assert.Inconclusive($"ROM missing: {file}");
        return File.ReadAllBytes(file);
    }

    static Board NewComputer(D64Image? disk = null, int device = 8)
    {
        var board = new Board(new SystemRoms(Rom("c-64-kernal.rom"), Rom("c-64-basic.rom"), Rom("c-64-chargen.rom")), startClock: false);
        if (disk is not null)
        {
            var drive = new Board1541(Rom("c-drive-1541.rom")) { DeviceNumber = device, Disk = disk };
            var cable = new IecBus();
            cable.Attach(() => board.Iec.AtnLow(), () => board.Iec.ClkLow(), () => board.Iec.DataLow(),
                input => board.Iec.ClkInputLow = input, input => board.Iec.DataInputLow = input);
            drive.Plug(cable);
            board.AfterCycle = new DriveClock(ComputerHz, drive.Tick).OnComputerCycle;
        }
        board.RunCycles(3 * ComputerHz);
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
        var text = new System.Text.StringBuilder();
        for (int row = 0; row < 25; row++)
        {
            for (int column = 0; column < 40; column++)
            {
                byte code = (byte)(board.Address((ushort)(0x0400 + row * 40 + column)) & 0x7F);   // reverse video is the same letter
                text.Append(code is >= 1 and <= 26 ? (char)('A' + code - 1) : code is >= 0x20 and < 0x60 ? (char)code : '.');
            }
            text.AppendLine();
        }
        return text.ToString();
    }

    static D64Image DiskWithProgram() => D64Writer.Create("BASIC DISK", [("HELLO", Hello)]);

    // 10 PRINT"HELLO FROM DISK"
    static readonly byte[] Hello =
    [
        0x01, 0x08,
        0x1D, 0x08, 0x0A, 0x00, 0x99, 0x22, 0x48, 0x45, 0x4C, 0x4C, 0x4F, 0x20, 0x46, 0x52, 0x4F, 0x4D, 0x20, 0x44, 0x49, 0x53, 0x4B, 0x22, 0x00,
        0x00, 0x00,
    ];

    [TestMethod]
    [Timeout(300_000)]
    public void Load_WithoutADrive_ShouldSayDeviceNotPresent()
    {
        var computer = NewComputer();

        Enter(computer, "LOAD\"$\",8", 2 * ComputerHz);

        StringAssert.Contains(Screen(computer), "DEVICE NOT PRESENT");
    }

    [TestMethod]
    [Timeout(300_000)]
    public void Directory_ShouldBeLoadedAndListedByBasic()
    {
        var computer = NewComputer(DiskWithProgram());

        Enter(computer, "LOAD\"$\",8", 12 * ComputerHz);
        Enter(computer, "LIST", ComputerHz);

        string screen = Screen(computer);
        StringAssert.Contains(screen, "BASIC DISK");
        StringAssert.Contains(screen, "\"HELLO\"");
        StringAssert.Contains(screen, "BLOCKS FREE");
    }

    [TestMethod]
    [Timeout(300_000)]
    public void Program_ShouldBeLoadedFromTheDiskAndRun()
    {
        var computer = NewComputer(DiskWithProgram());

        Enter(computer, "LOAD\"HELLO\",8", 12 * ComputerHz);
        Enter(computer, "RUN", ComputerHz);

        StringAssert.Contains(Screen(computer), "HELLO FROM DISK");
    }

    [TestMethod]
    [Timeout(300_000)]
    public void Program_ShouldBeSavedToTheDiskAndLoadedBack()
    {
        var disk = DiskWithProgram();
        var computer = NewComputer(disk);

        Enter(computer, "10 PRINT\"SAVED BY BASIC\"", ComputerHz / 2);
        Enter(computer, "SAVE\"KEEP\",8", 20 * ComputerHz);
        Assert.IsTrue(disk.Modified, "the disk was written");
        Enter(computer, "NEW", ComputerHz);
        Enter(computer, "LOAD\"KEEP\",8", 12 * ComputerHz);
        Enter(computer, "RUN", ComputerHz);

        StringAssert.Contains(Screen(computer), "SAVED BY BASIC");
    }

    // VALIDATE gives back the blocks that no file uses: here one that the BAM wrongly says is taken
    [TestMethod]
    [Timeout(300_000)]
    public void Validate_ShouldFreeABlockThatNoFileUses()
    {
        var disk = DiskWithProgram();
        int bam = 357 * D64Image.SECTOR_SIZE;   // track 18 sector 0
        int entry = bam + 4 + (5 - 1) * 4;                                  // track 5
        byte freeBefore = disk.Data[entry];
        disk.Data[entry]--;
        disk.Data[entry + 1] &= 0xFE;                                       // sector 0 of track 5 taken, by nobody
        var computer = NewComputer(disk);

        Enter(computer, "OPEN15,8,15,\"V\":CLOSE15", 40 * ComputerHz);

        Assert.AreEqual(freeBefore, disk.Data[entry], "the free count of the track");
        Assert.AreEqual(0x01, disk.Data[entry + 1] & 0x01, "sector 0 is free again");
    }
}
