namespace Thing65.Tests;

// The DMAgic on a flat 1 MB memory with a separate I/O space, to see which one each access goes to
[TestClass]
public sealed class F018Tests
{
    sealed class Memory : IDmaMemory
    {
        public readonly byte[] Ram = new byte[0x100000];
        public readonly byte[] Io = new byte[0x100000];

        public byte Read(int address, bool io) => io ? Io[address] : Ram[address];

        public void Write(int address, bool io, byte value)
        {
            if (io) Io[address] = value; else Ram[address] = value;
        }
    }

    const int ListAddress = 0x0F000;

    static (F018 Dma, Memory Memory) Setup(bool f018b = true)
    {
        var memory = new Memory();
        return (new F018 { Memory = memory, F018B = f018b }, memory);
    }

    // F018B list: command, count, source (address, bank), destination (address, bank), sub-command, modulo
    static void List(Memory memory, int address, int command, int count, int source, int sourceBank,
                     int destination, int destinationBank, int subCommand = 0, int modulo = 0)
    {
        byte[] bytes =
        [
            (byte)command, (byte)count, (byte)(count >> 8),
            (byte)source, (byte)(source >> 8), (byte)sourceBank,
            (byte)destination, (byte)(destination >> 8), (byte)destinationBank,
            (byte)subCommand, (byte)modulo, (byte)(modulo >> 8),
        ];
        bytes.CopyTo(memory.Ram, address);
    }

    // Writes the registers as the manual says (bank, high byte, low byte which triggers) and runs the job
    static void Trigger(F018 dma, int listAddress)
    {
        dma.Write(2, (byte)(listAddress >> 16));
        dma.Write(1, (byte)(listAddress >> 8));
        dma.Write(0, (byte)listAddress);
    }

    static int Run(F018 dma, int listAddress = ListAddress)
    {
        Trigger(dma, listAddress);
        int ticks = 0;
        while (dma.Busy)
        {
            dma.Tick();
            ticks++;
            Assert.IsTrue(ticks < 1_000_000, "The job does not end");
        }
        return ticks;
    }

    [TestMethod]
    public void Copy_ShouldMoveABlockAndTakeTwoCyclesPerByte()
    {
        var (dma, memory) = Setup();
        for (int i = 0; i < 5; i++) memory.Ram[0x1000 + i] = (byte)(0x10 + i);
        List(memory, ListAddress, command: 0, count: 5, source: 0x1000, sourceBank: 0, destination: 0x2000, destinationBank: 0);

        int ticks = Run(dma);

        CollectionAssert.AreEqual(new byte[] { 0x10, 0x11, 0x12, 0x13, 0x14 }, memory.Ram[0x2000..0x2005]);
        Assert.AreEqual(12 + 2 * 5, ticks); // 12 list bytes, then a read and a write for each byte
    }

    [TestMethod]
    public void Copy_ShouldReachTheWholeMegabyteThroughTheBanks()
    {
        var (dma, memory) = Setup();
        memory.Ram[0x5_1234] = 0xAB;
        List(memory, ListAddress, command: 0, count: 1, source: 0x1234, sourceBank: 5, destination: 0x4321, destinationBank: 0x0A);

        Run(dma);

        Assert.AreEqual(0xAB, memory.Ram[0xA_4321]);
    }

    [TestMethod]
    public void Fill_ShouldUseTheLowByteOfTheSource()
    {
        var (dma, memory) = Setup();
        List(memory, ListAddress, command: 3, count: 80, source: 0x002A, sourceBank: 0, destination: 0x0800, destinationBank: 0);

        int ticks = Run(dma);

        Assert.IsTrue(memory.Ram[0x0800..0x0850].All(b => b == 0x2A));
        Assert.AreEqual(0, memory.Ram[0x0850]);
        Assert.AreEqual(12 + 80, ticks);
    }

    [TestMethod]
    public void ZeroCount_ShouldMean64KB()
    {
        var (dma, memory) = Setup();
        List(memory, ListAddress, command: 3, count: 0, source: 0x0077, sourceBank: 0, destination: 0x0000, destinationBank: 2);

        int ticks = Run(dma);

        Assert.AreEqual(12 + 0x10000, ticks);
        Assert.AreEqual(0x77, memory.Ram[0x2_0000]);
        Assert.AreEqual(0x77, memory.Ram[0x2_FFFF]);
        Assert.AreEqual(0, memory.Ram[0x3_0000]);
    }

    [TestMethod]
    public void DescendingCopy_ShouldStartFromTheEnd()
    {
        var (dma, memory) = Setup();
        for (int i = 0; i < 4; i++) memory.Ram[0x1000 + i] = (byte)(1 + i);
        // Overlapping copy upwards by one byte only works if both pointers go down: start at the last byte
        List(memory, ListAddress, command: 0x30, count: 4, source: 0x1003, sourceBank: 0, destination: 0x1004, destinationBank: 0);

        Run(dma);

        CollectionAssert.AreEqual(new byte[] { 1, 1, 2, 3, 4 }, memory.Ram[0x1000..0x1005]);
    }

    [TestMethod]
    public void HoldDestination_ShouldWriteAlwaysToTheSameAddress()
    {
        var (dma, memory) = Setup();
        memory.Ram[0x1000] = 1;
        memory.Ram[0x1001] = 2;
        memory.Ram[0x1002] = 3;
        // Destination hold: sub-command bit 3
        List(memory, ListAddress, command: 0, count: 3, source: 0x1000, sourceBank: 0, destination: 0x3000, destinationBank: 0, subCommand: 0x08);

        Run(dma);

        Assert.AreEqual(3, memory.Ram[0x3000]);
        Assert.AreEqual(0, memory.Ram[0x3001]);
    }

    [TestMethod]
    public void IoBit_ShouldSelectTheIoSpace()
    {
        var (dma, memory) = Setup();
        memory.Io[0xD020] = 0x0E; // the "source" in I/O space
        List(memory, ListAddress, command: 0, count: 1, source: 0xD020, sourceBank: 0x80, destination: 0x2000, destinationBank: 0);
        Run(dma);
        Assert.AreEqual(0x0E, memory.Ram[0x2000]);

        List(memory, ListAddress, command: 3, count: 1, source: 0x0005, sourceBank: 0, destination: 0xD021, destinationBank: 0x80);
        Run(dma);
        Assert.AreEqual(0x05, memory.Io[0xD021]);
        Assert.AreEqual(0, memory.Ram[0xD021]);
    }

    // The window example of the manual: a 3 columns by 2 rows window of a screen 8 characters wide, saved
    // linearly. The modulo is the width - columns + 1: it replaces the +1 after the last byte of a row
    [TestMethod]
    public void SourceModulo_ShouldCopyARectangle()
    {
        var (dma, memory) = Setup();
        for (int i = 0; i < 32; i++) memory.Ram[0x0800 + i] = (byte)i; // 8 columns x 4 rows
        int origin = 0x0800 + 1 + 1 * 8;                                // column 1, row 1
        List(memory, ListAddress, command: 0, count: 2 * 256 + 3, source: origin, sourceBank: 0,
             destination: 0x1900, destinationBank: 0, subCommand: 0x01, modulo: 8 - 3 + 1);

        int ticks = Run(dma);

        CollectionAssert.AreEqual(new byte[] { 9, 10, 11, 17, 18, 19 }, memory.Ram[0x1900..0x1906]);
        Assert.AreEqual(12 + 2 * 6, ticks);
    }

    [TestMethod]
    public void DestinationModulo_ShouldRestoreARectangle()
    {
        var (dma, memory) = Setup();
        byte[] saved = [9, 10, 11, 17, 18, 19];
        saved.CopyTo(memory.Ram, 0x1900);
        // Destination modulo: sub-command bit 2
        List(memory, ListAddress, command: 0, count: 2 * 256 + 3, source: 0x1900, sourceBank: 0,
             destination: 0x0809, destinationBank: 0, subCommand: 0x04, modulo: 8 - 3 + 1);

        Run(dma);

        CollectionAssert.AreEqual(new byte[] { 9, 10, 11 }, memory.Ram[0x0809..0x080C]);
        CollectionAssert.AreEqual(new byte[] { 17, 18, 19 }, memory.Ram[0x0811..0x0814]);
        Assert.AreEqual(0, memory.Ram[0x080C]); // not disturbed
    }

    [TestMethod]
    public void FillWithModulo_ShouldClearAWindow()
    {
        var (dma, memory) = Setup();
        List(memory, ListAddress, command: 3, count: 2 * 256 + 3, source: 0x0020, sourceBank: 0,
             destination: 0x0809, destinationBank: 0, subCommand: 0x04, modulo: 6);

        Run(dma);

        Assert.IsTrue(memory.Ram[0x0809..0x080C].All(b => b == 0x20));
        Assert.AreEqual(0, memory.Ram[0x080C]);
        Assert.IsTrue(memory.Ram[0x0811..0x0814].All(b => b == 0x20));
    }

    [TestMethod]
    public void Chain_ShouldRunTheNextListRightAfter()
    {
        var (dma, memory) = Setup();
        List(memory, ListAddress, command: 0x07, count: 2, source: 0x0011, sourceBank: 0, destination: 0x2000, destinationBank: 0);      // fill, chain
        List(memory, ListAddress + 12, command: 0x03, count: 2, source: 0x0022, sourceBank: 0, destination: 0x3000, destinationBank: 0); // fill, last

        int ticks = Run(dma);

        Assert.AreEqual(0x11, memory.Ram[0x2001]);
        Assert.AreEqual(0x22, memory.Ram[0x3001]);
        Assert.AreEqual(2 * (12 + 2), ticks);
    }

    [TestMethod]
    public void F018AList_ShouldTakeDirectionAndModesFromTheBankBytes()
    {
        var (dma, memory) = Setup(f018b: false);
        for (int i = 0; i < 4; i++) memory.Ram[0x1000 + i] = (byte)(1 + i);
        // 11 bytes: command, count, source (bank bit 6 = descending), destination (same), modulo
        byte[] bytes = [0x00, 4, 0, 0x03, 0x10, 0x40, 0x04, 0x10, 0x40, 0, 0];
        bytes.CopyTo(memory.Ram, ListAddress);

        int ticks = Run(dma);

        CollectionAssert.AreEqual(new byte[] { 1, 1, 2, 3, 4 }, memory.Ram[0x1000..0x1005]);
        Assert.AreEqual(11 + 2 * 4, ticks);
    }

    [TestMethod]
    public void ListBank_ShouldLocateTheListInTheFirstMegabyte()
    {
        var (dma, memory) = Setup();
        List(memory, 0x3_0100, command: 3, count: 1, source: 0x0042, sourceBank: 0, destination: 0x2000, destinationBank: 0);

        Run(dma, 0x3_0100);

        Assert.AreEqual(0x42, memory.Ram[0x2000]);
    }

    [TestMethod]
    public void Status_ShouldShowBusyWhileTheJobRuns()
    {
        var (dma, memory) = Setup();
        List(memory, ListAddress, command: 0x03, count: 3, source: 0x0001, sourceBank: 0, destination: 0x2000, destinationBank: 0);
        Assert.AreEqual(0, dma.Read(3) & 0x80);

        Trigger(dma, ListAddress);

        Assert.AreNotEqual(0, dma.Read(3) & 0x80);
        Assert.IsTrue(dma.Busy);
        while (dma.Busy) dma.Tick();
        Assert.AreEqual(0, dma.Read(3) & 0x80);
    }

    [TestMethod]
    public void WritingTheStatus_ShouldCancelTheJob()
    {
        var (dma, memory) = Setup();
        List(memory, ListAddress, command: 3, count: 100, source: 0x0001, sourceBank: 0, destination: 0x2000, destinationBank: 0);
        Trigger(dma, ListAddress);
        for (int i = 0; i < 20; i++) dma.Tick();

        dma.Write(3, 0);

        Assert.IsFalse(dma.Busy);
        Assert.AreEqual(0, memory.Ram[0x2050]);
    }

    [TestMethod]
    public void BusGrant_False_ShouldMakeTheJobWait()
    {
        var (dma, memory) = Setup();
        bool grant = false;
        dma.BusGrant = () => grant;
        List(memory, ListAddress, command: 3, count: 1, source: 0x0055, sourceBank: 0, destination: 0x2000, destinationBank: 0);
        Trigger(dma, ListAddress);

        for (int i = 0; i < 50; i++) dma.Tick();
        Assert.IsTrue(dma.Busy);
        Assert.AreEqual(0, memory.Ram[0x2000]);

        grant = true;
        while (dma.Busy) dma.Tick();
        Assert.AreEqual(0x55, memory.Ram[0x2000]);
    }

    [TestMethod]
    [DataRow(0x80, 0xCC, 0xAA, 0x88, DisplayName = "AND")]
    [DataRow(0xE0, 0xCC, 0xAA, 0xEE, DisplayName = "OR")]
    [DataRow(0x60, 0xCC, 0xAA, 0x66, DisplayName = "XOR")]
    [DataRow(0x20, 0xCC, 0xAA, 0x22, DisplayName = "NOT source AND destination")]
    public void Mix_ShouldApplyTheMinterms(int command, int source, int destination, int expected)
    {
        var (dma, memory) = Setup();
        memory.Ram[0x1000] = (byte)source;
        memory.Ram[0x2000] = (byte)destination;
        List(memory, ListAddress, command: command | 1, count: 1, source: 0x1000, sourceBank: 0, destination: 0x2000, destinationBank: 0);

        int ticks = Run(dma);

        Assert.AreEqual(expected, memory.Ram[0x2000]);
        Assert.AreEqual(12 + 3, ticks); // two reads and a write
        Assert.AreEqual(source, memory.Ram[0x1000]);
    }

    [TestMethod]
    public void Swap_ShouldExchangeTwoBlocks()
    {
        var (dma, memory) = Setup();
        byte[] a = [1, 2, 3], b = [9, 8, 7];
        a.CopyTo(memory.Ram, 0x1000);
        b.CopyTo(memory.Ram, 0x2000);
        List(memory, ListAddress, command: 2, count: 3, source: 0x1000, sourceBank: 0, destination: 0x2000, destinationBank: 0);

        int ticks = Run(dma);

        CollectionAssert.AreEqual(b, memory.Ram[0x1000..0x1003]);
        CollectionAssert.AreEqual(a, memory.Ram[0x2000..0x2003]);
        Assert.AreEqual(12 + 4 * 3, ticks);
    }

    // XY_MOD: a box of a 320x200 one-bitplane screen at $2000. X is in bytes, Y in lines; the modulo is 128 - width + 1
    [TestMethod]
    public void XyMod_ShouldFillABoxOfABitplane()
    {
        var (dma, memory) = Setup();
        int destination = 3 * 128 + 5;                    // line 3, byte 5
        List(memory, ListAddress, command: 3, count: 4 * 256 + 2, source: 0x00FF, sourceBank: 0,   // fill $FF, 2 bytes x 4 rows
             destination: destination, destinationBank: 0, subCommand: 0x0C, modulo: (128 - 2 + 1) | (0x20 << 8)); // XY_MOD; screen at $2000 (misc bits 4-6 = 2)

        Run(dma);

        int Cell(int x, int y) => 0x4000 + (y >> 3) * 40 * 8 + x * 8 + (y & 7); // screen location 2 << 13 = $4000
        for (int y = 3; y < 7; y++)
            for (int x = 5; x < 7; x++)
                Assert.AreEqual(0xFF, memory.Ram[Cell(x, y)], $"x={x} y={y}");
        Assert.AreEqual(0, memory.Ram[Cell(7, 3)]);
        Assert.AreEqual(0, memory.Ram[Cell(5, 7)]);
        Assert.AreEqual(0, memory.Ram[Cell(4, 3)]);
    }

    [TestMethod]
    public void XyMod_BarrelShifter_ShouldCarryBitsIntoTheNextByte()
    {
        var (dma, memory) = Setup();
        // Copy 2 bytes of a row, linear source, XY destination, shifting right by 4 bits
        memory.Ram[0x1000] = 0b10101111;
        memory.Ram[0x1001] = 0b00000000;
        List(memory, ListAddress, command: 0, count: 1 * 256 + 2, source: 0x1000, sourceBank: 0,
             destination: 0, destinationBank: 0, subCommand: 0x0C, modulo: 0 | (0x20 << 8 | 0x04 << 8)); // misc: screen at $4000, shift 4

        Run(dma);

        // First byte: zeros come in on the left; the next one gets the bits that fell out of the first
        Assert.AreEqual(0b00001010, memory.Ram[0x4000]);
        Assert.AreEqual(0b11110000, memory.Ram[0x4000 + 8]);
    }

    [TestMethod]
    public void IntBit_ShouldStopTheJobWhileAnInterruptIsPendingAndResumeOnStatusRead()
    {
        var (dma, memory) = Setup();
        bool interrupt = false;
        dma.InterruptRequest = () => interrupt;
        List(memory, ListAddress, command: 0x0B, count: 10, source: 0x0007, sourceBank: 0, destination: 0x2000, destinationBank: 0); // fill, INT
        Trigger(dma, ListAddress);
        for (int i = 0; i < 12 + 3; i++) dma.Tick();
        Assert.AreEqual(3, memory.Ram[0x2000..0x200A].Count(b => b == 7));

        interrupt = true;
        for (int i = 0; i < 20; i++) dma.Tick();
        Assert.IsFalse(dma.Busy, "the CPU gets the bus back to serve the interrupt");
        Assert.AreEqual(3, memory.Ram[0x2000..0x200A].Count(b => b == 7), "no progress while stopped");

        interrupt = false;
        Assert.AreNotEqual(0, dma.Read(3) & 0x80, "the status still says busy");
        Assert.IsTrue(dma.Busy);
        while (dma.Busy) dma.Tick();
        Assert.IsTrue(memory.Ram[0x2000..0x200A].All(b => b == 7));
    }

    [TestMethod]
    public void IntBit_Clear_ShouldIgnoreInterrupts()
    {
        var (dma, memory) = Setup();
        dma.InterruptRequest = () => true;
        List(memory, ListAddress, command: 3, count: 5, source: 0x0009, sourceBank: 0, destination: 0x2000, destinationBank: 0);

        Run(dma);

        Assert.IsTrue(memory.Ram[0x2000..0x2005].All(b => b == 9));
    }
}
