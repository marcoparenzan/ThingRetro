using ThingCommodore.Hosting;
using ThingRetroEmu.Buses;
using ThingRetroEmu.Media;
using ThingRetroEmu.Ports;

namespace ThingCommodore.Tests;

// The rack of peripherals as a desktop app uses it: a computer that does nothing, with its connectors, and the rack on them
[TestClass]
public sealed class PeripheralRackTests
{
    sealed class Computer
    {
        public IecPort Iec { get; } = new();
        public CassettePort Cassette { get; } = new();
        public Action? AfterCycle;
        public PeripheralRack Rack { get; }

        public Computer()
        {
            // Everything runs at once: there is no emulation thread to hand the work to
            Rack = new PeripheralRack(Iec, Cassette, action => action(), cycle => AfterCycle = cycle, Rom, new byte[4096]);
        }

        public void Run(int cycles)
        {
            for (int i = 0; i < cycles; i++) AfterCycle?.Invoke();
        }

        static byte[]? Rom(string name)
        {
            var file = Path.Combine(AppContext.BaseDirectory, "roms", name);
            return File.Exists(file) ? File.ReadAllBytes(file) : null;
        }
    }

    [TestMethod]
    public void Attach_ShouldPutTheDriveOnTheCableAndItAnswersTheAttention()
    {
        var computer = new Computer();
        if (!computer.Rack.Disk1541.RomAvailable) Assert.Inconclusive("the 1541 ROM is not in the roms folder");

        Assert.IsTrue(computer.Rack.Disk1541.Attach(9));
        computer.Run(2_000_000);                         // the DOS of the drive starts and waits
        computer.Iec.AtnLow = () => true;                // the computer calls: the drive answers with DATA low
        computer.Iec.ClkLow = () => true;
        computer.Run(1_000);

        Assert.IsTrue(computer.Iec.DataInputLow(), "the 1541 acknowledges attention");
        Assert.AreEqual(9, computer.Rack.Disk1541.Device);
    }

    [TestMethod]
    public void Detach_ShouldTakeTheDriveOffTheCable()
    {
        var computer = new Computer();
        if (!computer.Rack.Disk1541.RomAvailable) Assert.Inconclusive("the 1541 ROM is not in the roms folder");
        computer.Rack.Disk1541.Attach(8);

        computer.Rack.Disk1541.Detach();

        Assert.AreEqual(0, computer.Rack.Disk1541.Device);
    }

    [TestMethod]
    public void Attach_ShouldFailWithoutTheRom()
    {
        var rack = new PeripheralRack(new IecPort(), null, action => action(), _ => { }, _ => null, new byte[4096]);

        Assert.IsFalse(rack.Disk1541.Attach(8));
        Assert.AreEqual(0, rack.Disk1541.Device);
    }

    [TestMethod]
    public void Insert_ShouldPutTheDiskInAndEjectTakeItOut()
    {
        var computer = new Computer();
        string folder = NewFolder();
        try
        {
            string path = Path.Combine(folder, "a.d64");
            File.WriteAllBytes(path, D64Writer.Create("APP", []).Data);

            computer.Rack.Disk1541.Insert(path);
            Assert.IsTrue(computer.Rack.Disk1541.HasDisk);
            Assert.AreEqual("a.d64", computer.Rack.Disk1541.DiskName);

            computer.Rack.Disk1541.Eject();
            Assert.IsFalse(computer.Rack.Disk1541.HasDisk);
        }
        finally { Directory.Delete(folder, true); }
    }

    [TestMethod]
    public void Insert_ShouldRefuseAFileThatIsNotADisk()
    {
        var computer = new Computer();
        string folder = NewFolder();
        try
        {
            string path = Path.Combine(folder, "bad.d81");
            File.WriteAllBytes(path, new byte[10]);

            Assert.ThrowsExactly<ArgumentException>(() => computer.Rack.Disk1581.Insert(path));
            Assert.IsFalse(computer.Rack.Disk1581.HasDisk);
        }
        finally { Directory.Delete(folder, true); }
    }

    [TestMethod]
    public void Tape_ShouldBeInsertedPressedAndSavedBack()
    {
        var computer = new Computer();
        string folder = NewFolder();
        try
        {
            string path = Path.Combine(folder, "t.tap");
            File.WriteAllBytes(path, new TapImage().ToBytes());

            computer.Rack.InsertTape(path);
            Assert.IsNotNull(computer.Rack.Tape.Tape);
            computer.Rack.PressTape(ThingCommodore.Tape.DatasetteKey.Play);
            Assert.AreEqual(ThingCommodore.Tape.DatasetteKey.Play, computer.Rack.Tape.Key);

            computer.Rack.Tape.Tape!.Append(1000);
            Assert.IsTrue(computer.Rack.TapeModified);
            computer.Rack.SaveTape();
            Assert.IsFalse(computer.Rack.TapeModified);
            Assert.AreEqual(1, new TapImage(File.ReadAllBytes(path)).Count);
        }
        finally { Directory.Delete(folder, true); }
    }

    [TestMethod]
    public void SetPrinter_ShouldPutThePrinterOnTheCableAsDevice4OrTakeItOff()
    {
        var computer = new Computer();

        computer.Rack.SetPrinter(4);
        Assert.AreEqual(4, computer.Rack.PrinterDevice);
        computer.Rack.SetPrinter(0);
        Assert.AreEqual(0, computer.Rack.PrinterDevice);
        Assert.ThrowsExactly<ArgumentException>(() => computer.Rack.SetPrinter(7));
    }

    static string NewFolder()
    {
        string folder = Path.Combine(Path.GetTempPath(), "rack-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }
}
