using static Thing64.Tests.TestHelpers;

namespace Thing64.Tests;

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
        board.Poke(0xD019, 0x0F); // writing $D011 at line 0 with compare 0 has already set the flag
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
        board.Poke(0xD011, 0x9B); // bit 8 of the compare value = 1
        board.Poke(0xD012, 0x20); // compare value = $120 = 288
        board.Poke(0xD01A, 0x01);

        board.RunCycles(0x21 * CYCLES_PER_LINE);
        Assert.AreEqual(0, board.Peek(0xD019) & 0x01); // no interrupt at line $20

        board.RunCycles((288 - 0x21) * CYCLES_PER_LINE);
        Assert.AreEqual(0x01, board.Peek(0xD019) & 0x01);
        Assert.AreEqual(0x80, board.Peek(0xD011) & 0x80); // when read, bit 7 is bit 8 of the raster
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
    public void BadLines_ShouldHoldBaLowFortyThreeCyclesPerCharacterRow()
    {
        var board = NewBoard();
        board.ParkCpu();
        board.Poke(0xD011, 0x1B); // DEN, 25 rows, YSCROLL = 3

        board.RunCycles(LINES_PER_FRAME * CYCLES_PER_LINE);

        // 40 character code accesses + 3 cycles of warning: with the CPU in a loop of reads only
        // the CPU stays halted for all 43
        Assert.AreEqual(25 * 43, board.StolenCycles);
    }

    // Display off (no bad lines), sprites at Y 100: the DMA lasts 21 lines (42 with vertical expansion).
    // One sprite: 2 access cycles + 3 of warning per line. All 8: cycles 55-63 and 1-10 of every line
    [TestMethod]
    [DataRow(0x01, 0x00, 21 * 5)]
    [DataRow(0x01, 0x01, 42 * 5)]
    [DataRow(0x81, 0x00, 21 * 10)]
    [DataRow(0xFF, 0x00, 21 * 19)]
    public void SpriteDma_ShouldStealCyclesFromTheCpu(int enabled, int expandY, int expected)
    {
        var board = NewBoard();
        board.ParkCpu();
        board.Poke(0xD011, 0x0B);
        for (int n = 0; n < 8; n++) board.Poke((ushort)(0xD001 + n * 2), 100);
        board.Poke(0xD017, (byte)expandY);
        board.Poke(0xD015, (byte)enabled);

        board.RunCycles(LINES_PER_FRAME * CYCLES_PER_LINE);

        Assert.AreEqual(expected, board.StolenCycles);
    }

    [TestMethod]
    public void SpriteDma_ShouldStopTheCpuDuringTheAccesses()
    {
        // Sprite 0: BA low in cycles 55-59 (from 1), AEC low in cycles 58-59 where it reads pointer and data
        var board = NewBoard();
        board.ParkCpu();
        board.Poke(0xD011, 0x0B);
        board.Poke(0xD001, 100);
        board.Poke(0xD015, 0x01);
        board.RunCycles(101 * CYCLES_PER_LINE);   // start of line 101: DMA on

        var ba = new List<int>();
        var aec = new List<int>();
        for (int cycle = 1; cycle <= CYCLES_PER_LINE; cycle++)
        {
            if (!board.Vic.BusAvailable) ba.Add(cycle);
            if (!board.Vic.AddressEnable) aec.Add(cycle);
            board.RunCycles(1);
        }

        CollectionAssert.AreEqual(new[] { 55, 56, 57, 58, 59 }, ba);
        CollectionAssert.AreEqual(new[] { 58, 59 }, aec);
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

        board.Poke(0xD060, 0x05); // mirror of $D020

        Assert.AreEqual(0x05, board.Peek(0xD020) & 0x0F);
    }
}
