using ThingCommodore.Drives;
using ThingRetroEmu.Buses;
using ThingRetroEmu.Media;
using Thing65.Computer;

namespace Thing65.Tests;

// The 1541 on the C65's cable as the desktop app plugs it: the board runs the drive cycle by cycle
[TestClass]
public sealed class Drive1541HostTests
{
    static byte[]? DriveRom()
    {
        string? file = Drive1541Host.FindRom(Path.Combine(AppContext.BaseDirectory, "roms"));
        return file is null ? null : File.ReadAllBytes(file);
    }

    // A computer that does nothing: a ROM of NOPs, so that it does not touch the serial lines
    static BoardC65 NewBoard()
    {
        var image = new byte[SystemRom128.SIZE];
        Array.Fill(image, (byte)0xEA);
        image[0xFFFC] = 0x00;
        image[0xFFFD] = 0xE0;
        return new BoardC65(new SystemRom128(image), startClock: false);
    }

    [TestMethod]
    public void Drive_ShouldRunWithTheBoardAndAnswerOnTheCable()
    {
        var rom = DriveRom();
        if (rom is null) Assert.Inconclusive("the 1541 ROM is not in the roms folder");
        var board = NewBoard();
        var host = new Drive1541Host(board, rom, 9);

        board.RunCycles(2_000_000);                      // the drive's DOS starts and waits
        board.Iec.AtnLow = () => true;                   // the computer calls (ATN and CLK low): the drive answers with DATA low
        board.Iec.ClkLow = () => true;
        board.RunCycles(1_000);

        Assert.IsTrue(board.Iec.DataInputLow(), "the 1541 acknowledges attention");
        Assert.AreEqual(9, host.DeviceNumber);
    }

    [TestMethod]
    public void Insert_ShouldPutTheDiskInAndEjectTakeItOut()
    {
        var rom = DriveRom();
        if (rom is null) Assert.Inconclusive("the 1541 ROM is not in the roms folder");
        var host = new Drive1541Host(NewBoard(), rom, 9);
        var disk = D64Writer.Create("APP", []);

        host.Insert(disk, null);
        Assert.AreSame(disk, host.Image);

        host.Eject();
        Assert.IsNull(host.Image);
    }

    [TestMethod]
    public void Save_ShouldWriteAModifiedDiskToItsFileAndSaveAsToANewOne()
    {
        var rom = DriveRom();
        if (rom is null) Assert.Inconclusive("the 1541 ROM is not in the roms folder");
        string folder = Path.Combine(Path.GetTempPath(), "thing65-d64-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string path = Path.Combine(folder, "a.d64");
            var disk = D64Writer.Create("APP", []);
            File.WriteAllBytes(path, disk.Data);
            var host = new Drive1541Host(NewBoard(), rom, 9);
            host.Insert(new D64Image(File.ReadAllBytes(path)), path);

            Assert.IsFalse(host.Save(), "nothing changed");
            host.Image!.WriteSector(1, 0, Enumerable.Repeat((byte)0x42, 256).ToArray());
            Assert.IsTrue(host.Modified);
            Assert.IsTrue(host.Save());

            Assert.AreEqual(0x42, File.ReadAllBytes(path)[0]);
            Assert.IsFalse(host.Modified);
            string copy = Path.Combine(folder, "b.d64");
            host.SaveAs(copy);
            Assert.AreEqual(host.Image.Data.Length, new FileInfo(copy).Length);
            Assert.AreEqual(copy, host.Path);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
