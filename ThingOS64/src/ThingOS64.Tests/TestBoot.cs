using static ThingOS64.Tests.OsBoard;

namespace ThingOS64.Tests;

[TestClass]
public sealed class TestBoot
{
    [TestMethod]
    public void Roms_ShouldFitTheSocketsAndPointTheVectorsIntoTheLowerRom()
    {
        var roms = Roms.Value;

        Assert.AreEqual(OsRoms.ROM_SIZE, roms.Kernal.Length);
        Assert.AreEqual(OsRoms.ROM_SIZE, roms.Basic.Length);
        Assert.AreEqual(OsRoms.CHARGEN_SIZE, roms.Chargen.Length);
        int Vector(int address) => roms.Kernal[address - 0xE000] | roms.Kernal[address - 0xE000 + 1] << 8;
        Assert.AreEqual(Symbol("nmi"), Vector(0xFFFA));
        Assert.AreEqual(Symbol("reset"), Vector(0xFFFC));
        Assert.AreEqual(Symbol("irq"), Vector(0xFFFE));
    }

    [TestMethod]
    public void Chargen_ShouldHoldTheReverseOfTheFirstHalfInTheSecond()
    {
        var chargen = Roms.Value.Chargen;

        for (int i = 0; i < 0x400; i++)
            Assert.AreEqual((byte)~chargen[i], chargen[0x400 + i], $"byte {i}");
        CollectionAssert.AreEqual(chargen[..0x800], chargen[0x800..]);
        Assert.IsTrue(chargen[(0x20 * 8)..(0x21 * 8)].All(b => b == 0), "space is empty");
        Assert.IsTrue(chargen[(0xA0 * 8)..(0xA1 * 8)].All(b => b == 0xFF), "reverse space is solid");
    }

    [TestMethod]
    public void Boot_ShouldDrawTheMenuBarAndTheDesktop()
    {
        var board = Booted();

        StringAssert.StartsWith(board.ScreenRow(0), " ThingOS   File   Disk");
        Assert.AreEqual(0, board.ColorAt(1, 0), "menu bar text is black");
        for (int row = 1; row < 25; row++)
        {
            Assert.AreEqual(0xA0, board.ScreenCode(0, row), $"desktop, row {row}");
            Assert.AreEqual(0xA0, board.ScreenCode(39, row), $"desktop, row {row}");
            Assert.AreEqual(14, board.ColorAt(20, row), $"desktop color, row {row}");
        }
    }

    [TestMethod]
    public void Boot_ShouldSetTheVideoChipForTheOsCharacterSet()
    {
        var board = Booted();

        Assert.AreEqual(0x14, board.Peek(0xD018) & 0xFE, "screen $0400, characters $1000");
        Assert.AreEqual(0x1B, board.Peek(0xD011) & 0x7F, "text mode, 25 rows");
        Assert.AreEqual(1, board.Peek(0xD021) & 0x0F, "white background");
        Assert.AreEqual(0x03, board.Peek(0xD015), "pointer sprites on");
    }

    [TestMethod]
    public void Irq_ShouldRunOncePerFrame()
    {
        var board = Booted();
        int before = board.PeekWord(Symbol("ticks"));

        board.RunFrames(50);

        Assert.AreEqual(50, board.PeekWord(Symbol("ticks")) - before, 1);
    }
}
