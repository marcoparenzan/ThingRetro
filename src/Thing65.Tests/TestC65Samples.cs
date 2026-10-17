using Thing65.Build;

namespace Thing65.Tests;

// The samples of the C65 (samples/c65): the programs match their sources, and each one does what it says on the real ROM.
// The running tests need the C65 ROM (Inconclusive without it)
[TestClass]
public sealed class C65SamplesTests
{
    static string Folder => Path.Combine(TestHelpers.SamplesFolder, "c65");

    static readonly string RomFile = Path.Combine(AppContext.BaseDirectory, "roms", "c-65-19910828.rom");
    const string CR = "\r";

    [TestMethod]
    public void Programs_ShouldMatchTheirSources()
    {
        var manifest = BuildManifest.Load(Path.Combine(Folder, "samples.json"));
        Assert.AreEqual("4510", manifest.Cpu);
        Assert.IsTrue(manifest.Outputs.Count >= 4);

        foreach (var output in manifest.Outputs)
        {
            byte[] built = manifest.Build(output);
            byte[] file = File.ReadAllBytes(manifest.PathOf(output.File));
            CollectionAssert.AreEqual(file, built, $"{output.File} is not what {output.Source} assembles to: run thing65 build samples/c65/samples.json");
            Assert.AreEqual(0x00, built[0], $"{output.File} loads at $1800");
            Assert.AreEqual(0x18, built[1]);
        }
    }

    // Boots, puts the program on a disk, loads it into bank 0 and starts it
    static BoardC65 Run(string name)
    {
        if (!File.Exists(RomFile)) Assert.Inconclusive("C65 ROM missing: copy c-65-19910828.rom into roms");
        var board = new BoardC65(new SystemRom128(File.ReadAllBytes(RomFile)), startClock: false);
        board.RunCycles(9_000_000);

        board.Disk = D81Writer.Create(name.ToUpperInvariant(), File.ReadAllBytes(Path.Combine(Folder, name + ".prg")));
        board.TypeText($"BLOAD\"{name.ToUpperInvariant()}\",B0" + CR);
        while (board.IsTyping) board.RunCycles(1000);
        board.RunCycles(3_000_000);
        board.TypeText("SYS 6144" + CR);
        while (board.IsTyping) board.RunCycles(1000);
        board.RunCycles(1_500_000);
        return board;
    }

    [TestMethod]
    [Timeout(120_000)]
    public void Dma65_ShouldFillAndCopyALine()
    {
        var board = Run("dma65");

        for (int i = 0; i < 80; i++)
        {
            Assert.AreEqual(0x2A, board.Ram(0x0800 + 8 * 80 + i), $"line 8, column {i}");
            Assert.AreEqual(0x2A, board.Ram(0x0800 + 9 * 80 + i), $"line 9, column {i}");
        }
        Assert.AreNotEqual(0x2A, board.Ram(0x0800 + 10 * 80));
    }

    [TestMethod]
    [Timeout(120_000)]
    public void Map65_ShouldWriteThroughTheWindowAndRestoreTheMap()
    {
        var board = Run("map65");

        Assert.AreEqual(0x37, board.Ram(0x12000), "the byte went to bank 1, through the mapped block");
        string screen = string.Join('\n', Enumerable.Range(0, 25).Select(row =>
            new string(Enumerable.Range(0, 80).Select(c => (char)board.Ram(0x0800 + row * 80 + c)).ToArray())));
        StringAssert.Contains(screen, "7"); // printed after the map was put back: the KERNAL works
        var cpu = (MOS4510)board.CpuSocket.Chip!;
        Assert.IsTrue(cpu.InstructionCount > 0);
    }

    [TestMethod]
    [Timeout(120_000)]
    public void Vic3_ShouldChangeThePaletteAndSetAttributes()
    {
        var board = Run("vic3");
        var vic = (CSG4567)board.VicSocket.Chip!;

        Assert.AreEqual(0x0F, vic.Palette(0, 2));
        Assert.AreEqual(0x07, vic.Palette(1, 2));
        Assert.AreEqual(0x00, vic.Palette(2, 2));
        Assert.AreNotEqual(0, vic.ControlA & 0x04, "PAL");
        Assert.AreNotEqual(0, vic.ControlB & 0x20, "ATTR");

        int row = 12 * 80;
        Assert.AreEqual(0x12, board.Ram(0x0800 + row + 10), "'r' as a screen code");
        Assert.AreEqual(0x21, board.Ram(0x1F800 + row + 10), "reverse + white");
        Assert.AreEqual(0x81, board.Ram(0x1F800 + row + 30), "underline + white");
    }
}
