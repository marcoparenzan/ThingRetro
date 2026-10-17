using static Thing65.Tests.TestHelpers;

namespace Thing65.Tests;

// Sockets and traces of the board: any object with the right pinout fits the socket
[TestClass]
public sealed class SocketTests
{
    const long CYCLES_PER_SECOND = 985248;

    // A "chip" that fits the SID socket and records what goes through the bus
    sealed class SidProbe : I6581Pinout
    {
        public readonly List<(int Register, byte Value)> Writes = [];
        public int Ticks;

        public Func<byte> PotXInput { get; set; } = () => 0xFF;
        public Func<byte> PotYInput { get; set; } = () => 0xFF;
        public byte Read(int register) => (byte)(0x40 + register);
        public void Write(int register, byte value) => Writes.Add((register, value));
        public double AudioOutput => 0;
        public void Reset() { }
        public void Tick() => Ticks++;
    }

    [TestMethod]
    public void Socket_ShouldAcceptAnyChipWithTheSamePinout()
    {
        var board = NewBoard();
        var original = board.SidSocket.Remove();
        var probe = new SidProbe();

        board.SidSocket.Insert(probe);
        board.ParkCpu();
        board.Poke(0xD418, 0x0F);           // volume
        board.Poke(0xD438, 0x07);           // the SID registers repeat every 32 bytes
        board.RunCycles(100);

        Assert.IsInstanceOfType<MOS6581>(original);
        CollectionAssert.AreEqual(new[] { (0x18, (byte)0x0F), (0x18, (byte)0x07) }, probe.Writes);
        Assert.AreEqual(0x5B, (int)board.Peek(0xD41B));
        Assert.AreEqual(100, probe.Ticks);  // it receives the clock from the board
    }

    [TestMethod]
    public void Socket_ShouldRejectSecondChip()
    {
        var board = NewBoard();

        Assert.ThrowsException<InvalidOperationException>(() => board.SidSocket.Insert(new SidProbe()));
        Assert.IsFalse(board.SidSocket.IsEmpty);
    }

    [TestMethod]
    public void EmptySidSocket_ShouldStillBootAndReadOpenBus()
    {
        var board = NewBoard();
        board.SidSocket.Remove();
        board.Reset();

        board.RunCycles(3 * CYCLES_PER_SECOND);

        Assert.IsTrue(board.SidSocket.IsEmpty);
        Assert.AreEqual("READY.", board.ScreenLines()[5]);
        Assert.AreEqual(0xFF, (int)board.Peek(0xD41B));
    }

    [TestMethod]
    public void EmptyVicSocket_ShouldReleaseBusAndIrq()
    {
        var board = NewBoard();
        board.VicSocket.Remove();
        board.ParkCpu();

        board.RunCycles(312 * 63);

        Assert.AreEqual(0, board.StolenCycles);   // no BA low without a VIC-II
        Assert.AreEqual(0xFF, (int)board.Peek(0xD020));
        Assert.IsFalse(board.IrqLine);
    }

    [TestMethod]
    public void Cia2_ShouldTriggerNmiOncePerFallingEdge()
    {
        // NMI handler (through the KERNAL vector $0318) that counts the calls and does not acknowledge CIA 2:
        // the /NMI line stays low, but the NMI triggers only on the falling edge
        var board = NewBoard();
        board.Poke(0x0318, 0x00, 0xC1);
        board.Poke(0xC100, 0xEE, 0x00, 0xC2, 0x4C, 0x03, 0xC1); // INC $C200 : JMP *
        board.ParkCpu();
        board.Poke(0xDD0D, 0x81);
        board.Poke(0xDD04, 0x10, 0x00);
        board.Poke(0xDD0E, 0x11);

        board.RunCycles(2000);

        Assert.IsTrue(board.NmiLine);
        Assert.AreEqual(1, (int)board.Peek(0xC200));
    }

    [TestMethod]
    public void IrqLine_ShouldBeWiredOrOfVicAndCia1()
    {
        var board = NewBoard();
        board.ParkCpu();
        Assert.IsFalse(board.IrqLine);

        board.Poke(0xD01A, 0x01);
        board.Poke(0xD012, 0x10);
        board.RunCycles(0x11 * 63);
        Assert.IsTrue(board.Vic.Irq);
        Assert.IsFalse(board.Cia1.Irq);
        Assert.IsTrue(board.IrqLine);

        board.Poke(0xD019, 0x0F);
        Assert.IsFalse(board.IrqLine);
    }
}
