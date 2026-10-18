namespace ThingRetroEmu.Tests;

// The WD1770 on a fake drive: the head, the sensors and a .d81 disk
[TestClass]
public sealed class Wd1770Tests
{
    sealed class FakeDrive : IFloppyDrive
    {
        public bool Ready { get; set; } = true;
        public bool Track0 => Cylinder == 0;
        public bool WriteProtected { get; set; }
        public bool Index => false;
        public int Cylinder { get; set; } = 10;
        public int Side { get; set; }
        public IDiskMedia? Media { get; set; }
        public int Steps;
        public void Step(bool towardsCentre)
        {
            Steps++;
            Cylinder = Math.Clamp(Cylinder + (towardsCentre ? 1 : -1), 0, 81);
        }
    }

    static D81Image Disk()
    {
        var bytes = new byte[D81Image.SIZE];
        for (int sector = 0; sector < D81Image.TRACKS * D81Image.SIDES * D81Image.SECTORS; sector++)
            Array.Fill(bytes, (byte)(sector & 0xFF), sector * D81Image.SECTOR_SIZE, D81Image.SECTOR_SIZE);
        return new D81Image(bytes);
    }

    static (Wd1770 Chip, FakeDrive Drive) New(D81Image? disk = null)
    {
        var drive = new FakeDrive { Media = disk ?? Disk() };
        return (new Wd1770 { Drive = drive }, drive);
    }

    static void Run(Wd1770 chip, Func<bool> until, int limit = 5_000_000)
    {
        for (int i = 0; i < limit && !until(); i++) chip.Tick();
        Assert.IsTrue(until(), "the chip did not get there");
    }

    static void Command(Wd1770 chip, byte command)
    {
        chip.Write(0, command);
        Run(chip, () => chip.Intrq);
    }

    [TestMethod]
    public void Restore_ShouldStepToTrackZero()
    {
        var (chip, drive) = New();

        Command(chip, 0x00);

        Assert.AreEqual(0, drive.Cylinder);
        Assert.AreEqual(0, chip.Read(1));
        Assert.AreEqual(0x04, chip.Read(0) & 0x05, "track 0 and not busy");
    }

    [TestMethod]
    public void Seek_ShouldMoveTheHeadAndTheTrackRegister()
    {
        var (chip, drive) = New();
        chip.Write(1, 10);
        chip.Write(3, 25);

        Command(chip, 0x10);

        Assert.AreEqual(25, drive.Cylinder);
        Assert.AreEqual(25, chip.Read(1));
        Assert.AreEqual(15, drive.Steps);
    }

    [TestMethod]
    public void Seek_WithVerify_ShouldFailOnAnUnformattedTrack()
    {
        var disk = D81Image.Blank();
        var (chip, drive) = New(disk);
        chip.Write(1, 10);
        chip.Write(3, 12);

        Command(chip, 0x14);

        Assert.AreEqual(0x10, chip.Read(0) & 0x10, "seek error");
    }

    [TestMethod]
    public void ReadSector_ShouldDeliver512BytesThroughDrq()
    {
        var (chip, drive) = New();
        chip.Write(1, 10);                       // the track register says where the head is
        chip.Write(2, 3);                        // sector 3
        chip.Write(0, 0x80);
        var got = new List<byte>();

        for (long cycles = 0; cycles < 2_000_000 && !chip.Intrq; cycles++)
        {
            chip.Tick();
            if (chip.Drq) got.Add(chip.Read(3));
        }

        Assert.AreEqual(512, got.Count);
        int index = (10 * D81Image.SIDES + 0) * D81Image.SECTORS + 3 - 1;
        Assert.IsTrue(got.All(b => b == (byte)(index & 0xFF)));
        Assert.AreEqual(0, chip.Read(0) & 0x1D, "no error, not busy");
    }

    [TestMethod]
    public void ReadSector_WhenTheCpuIsSlow_ShouldSetLostData()
    {
        var (chip, drive) = New();
        chip.Write(1, 10);
        chip.Write(2, 1);
        chip.Write(0, 0x80);

        Run(chip, () => chip.Intrq);             // nobody reads the data register

        Assert.AreEqual(0x04, chip.Read(0) & 0x04);
    }

    [TestMethod]
    public void ReadSector_OnTheOtherSide_ShouldUseTheSideOfTheDrive()
    {
        var (chip, drive) = New();
        drive.Side = 1;
        chip.Write(1, 10);
        chip.Write(2, 1);
        chip.Write(0, 0x80);
        byte first = 0;
        bool taken = false;
        for (int i = 0; i < 2_000_000 && !chip.Intrq; i++)
        {
            chip.Tick();
            if (chip.Drq)
            {
                byte b = chip.Read(3);
                if (!taken) { first = b; taken = true; }
            }
        }

        Assert.AreEqual((byte)((10 * 2 + 1) * 10), first);
    }

    [TestMethod]
    public void ReadSector_WithTheWrongTrackRegister_ShouldNotFindIt()
    {
        var (chip, drive) = New();
        chip.Write(1, 40);                       // the head is on 10
        chip.Write(2, 1);

        Command(chip, 0x80);

        Assert.AreEqual(0x10, chip.Read(0) & 0x10, "record not found");
    }

    [TestMethod]
    public void ReadSector_WithASectorThatDoesNotExist_ShouldNotFindIt()
    {
        var (chip, drive) = New();
        chip.Write(1, 10);
        chip.Write(2, 11);

        Command(chip, 0x80);

        Assert.AreEqual(0x10, chip.Read(0) & 0x10);
    }

    [TestMethod]
    public void ReadSectors_Multiple_ShouldGoOnUntilTheLastSector()
    {
        var (chip, drive) = New();
        chip.Write(1, 10);
        chip.Write(2, 9);
        chip.Write(0, 0x90);
        int bytes = 0;

        for (long cycles = 0; cycles < 20_000_000 && !chip.Intrq; cycles++)
        {
            chip.Tick();
            if (chip.Drq) { chip.Read(3); bytes++; }
        }

        Assert.AreEqual(2 * 512, bytes, "sectors 9 and 10");
        Assert.AreEqual(0x10, chip.Read(0) & 0x10, "and then the sector 11 is not found");
    }

    [TestMethod]
    public void WriteSector_ShouldStoreWhatTheCpuGives()
    {
        var disk = Disk();
        var (chip, drive) = New(disk);
        chip.Write(1, 10);
        chip.Write(2, 5);
        chip.Write(0, 0xA0);
        byte value = 0;

        for (long cycles = 0; cycles < 2_000_000 && !chip.Intrq; cycles++)
        {
            chip.Tick();
            if (chip.Drq) chip.Write(3, value++);
        }

        var back = new byte[512];
        Assert.IsTrue(disk.ReadSector(10, 0, 5, back));
        for (int i = 0; i < 512; i++) Assert.AreEqual((byte)i, back[i], $"byte {i}");
        Assert.IsTrue(disk.Modified);
    }

    [TestMethod]
    public void WriteSector_OnAProtectedDisk_ShouldRefuse()
    {
        var disk = Disk();
        var drive = new FakeDrive { Media = new D81Image(disk.Data, writeProtected: true), WriteProtected = true };
        var chip = new Wd1770 { Drive = drive };
        chip.Write(1, 10);
        chip.Write(2, 5);

        Command(chip, 0xA0);

        Assert.AreEqual(0x40, chip.Read(0) & 0x40);
        Assert.IsFalse(drive.Media.WriteSector(10, 0, 5, new byte[512]), "the medium refuses too");
    }

    [TestMethod]
    public void Commands_WithoutADisk_ShouldReportNotReady()
    {
        var (chip, drive) = New();
        drive.Ready = false;

        Command(chip, 0x80);

        Assert.AreEqual(0x80, chip.Read(0) & 0x80);
    }

    [TestMethod]
    public void ReadAddress_ShouldGiveTheIdField()
    {
        var (chip, drive) = New();
        chip.Write(0, 0xC0);
        var id = new List<byte>();

        for (long cycles = 0; cycles < 2_000_000 && !chip.Intrq; cycles++)
        {
            chip.Tick();
            if (chip.Drq) id.Add(chip.Read(3));
        }

        Assert.AreEqual(6, id.Count);
        Assert.AreEqual(10, id[0], "track");
        Assert.AreEqual(0, id[1], "side");
        Assert.IsTrue(id[2] is >= 1 and <= 10, "sector");
        Assert.AreEqual(2, id[3], "512 bytes");
        Assert.AreEqual(10, chip.Read(2), "the track goes to the sector register");
    }

    [TestMethod]
    public void WriteTrack_ShouldFormatTheSectorsOfTheStream()
    {
        var disk = D81Image.Blank();
        var (chip, drive) = New(disk);
        drive.Cylinder = 3;
        // The stream a DOS writes: for each sector $FE track side sector size $F7, then $FB, the data and $F7
        var stream = new List<byte>();
        for (int sector = 1; sector <= 10; sector++)
        {
            stream.AddRange([0x4E, 0x4E, 0xF5, 0xF5, 0xF5, 0xFE, 3, 0, (byte)sector, 2, 0xF7, 0x4E, 0x4E, 0xF5, 0xF5, 0xF5, 0xFB]);
            stream.AddRange(Enumerable.Repeat((byte)(0xA0 + sector), 512));
            stream.AddRange([0xF7, 0x4E, 0x4E]);
        }
        chip.Write(0, 0xF0);
        int next = 0;

        for (long cycles = 0; cycles < 5_000_000 && !chip.Intrq; cycles++)
        {
            chip.Tick();
            if (chip.Drq) chip.Write(3, next < stream.Count ? stream[next++] : (byte)0x4E);
        }

        var back = new byte[512];
        for (int sector = 1; sector <= 10; sector++)
        {
            Assert.IsTrue(disk.ReadSector(3, 0, sector, back), $"sector {sector} exists");
            Assert.IsTrue(back.All(b => b == 0xA0 + sector), $"sector {sector} has its data");
        }
    }

    [TestMethod]
    public void ForceInterrupt_ShouldStopTheCommand()
    {
        var (chip, drive) = New();
        chip.Write(3, 70);
        chip.Write(0, 0x10);
        for (int i = 0; i < 20000; i++) chip.Tick();

        chip.Write(0, 0xD0);

        Assert.AreEqual(0, chip.Read(0) & 1, "not busy");
        int cylinder = drive.Cylinder;
        for (int i = 0; i < 100000; i++) chip.Tick();
        Assert.AreEqual(cylinder, drive.Cylinder, "and the head stays where it was");
    }
}
