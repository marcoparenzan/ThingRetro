using static Fake64.Tests.TestHelpers;

namespace Fake64.Tests;

[TestClass]
public sealed class MOS6569Tests
{
    const int CYCLES_PER_LINE = 63;
    const int LINES_PER_FRAME = 312;

    [TestMethod]
    public void RasterRegister_ShouldReadCurrentLine_NotCompareValue()
    {
        var board = NewBoard();
        board.ParkCpu();
        board.Poke(0xD012, 0x80);

        board.RunCycles(10 * CYCLES_PER_LINE);

        Assert.AreEqual(10, (int)board.Peek(0xD012));
    }

    [TestMethod]
    public void RasterIrq_ShouldFireOnCompareLine()
    {
        var board = NewBoard();
        board.ParkCpu();
        board.Poke(0xD011, 0x1B);
        board.Poke(0xD012, 100);
        board.Poke(0xD019, 0x0F); // scrivendo $D011 alla riga 0 con confronto 0 il flag è già scattato
        board.Poke(0xD01A, 0x01);

        board.RunCycles(99 * CYCLES_PER_LINE);
        Assert.AreEqual(0, board.Peek(0xD019) & 0x01);

        board.RunCycles(CYCLES_PER_LINE);
        Assert.AreEqual(0x81, board.Peek(0xD019) & 0x81);
        Assert.IsTrue(board.IrqLine);

        board.Poke(0xD019, 0x01); // acknowledge
        Assert.AreEqual(0, board.Peek(0xD019) & 0x81);
        Assert.IsFalse(board.IrqLine);
    }

    [TestMethod]
    public void RasterIrq_ShouldUseBit7OfD011AsNinthCompareBit()
    {
        var board = NewBoard();
        board.ParkCpu();
        board.Poke(0xD011, 0x9B); // bit 8 del confronto = 1
        board.Poke(0xD012, 0x20); // confronto = $120 = 288
        board.Poke(0xD01A, 0x01);

        board.RunCycles(0x21 * CYCLES_PER_LINE);
        Assert.AreEqual(0, board.Peek(0xD019) & 0x01); // nessun interrupt alla riga $20

        board.RunCycles((288 - 0x21) * CYCLES_PER_LINE);
        Assert.AreEqual(0x01, board.Peek(0xD019) & 0x01);
        Assert.AreEqual(0x80, board.Peek(0xD011) & 0x80); // in lettura il bit 7 è il bit 8 del raster
    }

    [TestMethod]
    public void RasterIrq_ShouldNotAssertIrqLineWhenDisabled()
    {
        var board = NewBoard();
        board.ParkCpu();
        board.Poke(0xD012, 5);

        board.RunCycles(10 * CYCLES_PER_LINE);

        Assert.AreEqual(0x01, board.Peek(0xD019) & 0x81);
        Assert.IsFalse(board.IrqLine);
    }

    [TestMethod]
    public void BadLines_ShouldStealFortyCyclesPerCharacterRow()
    {
        var board = NewBoard();
        board.ParkCpu();
        board.Poke(0xD011, 0x1B); // DEN, 25 righe, YSCROLL = 3

        board.RunCycles(LINES_PER_FRAME * CYCLES_PER_LINE);

        Assert.AreEqual(25 * 40, board.StolenCycles);
    }

    [TestMethod]
    public void BadLines_ShouldNotOccurWithDisplayDisabled()
    {
        var board = NewBoard();
        board.ParkCpu();
        board.Poke(0xD011, 0x0B); // DEN = 0

        board.RunCycles(LINES_PER_FRAME * CYCLES_PER_LINE);

        Assert.AreEqual(0, board.StolenCycles);
    }

    [TestMethod]
    public void VicRegisters_ShouldBeMirroredEvery64Bytes()
    {
        var board = NewBoard();
        board.ParkCpu();

        board.Poke(0xD060, 0x05); // mirror di $D020

        Assert.AreEqual(0x05, board.Peek(0xD020) & 0x0F);
    }
}
