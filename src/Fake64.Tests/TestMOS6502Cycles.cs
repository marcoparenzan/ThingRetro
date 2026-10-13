namespace Fake64.Tests;

// The 6502 core alone, on a fake bus that records every access: one cycle, one access, as on the chip
[TestClass]
public sealed class MOS6502CycleTests
{
    sealed class RecordingBus : IBus
    {
        public readonly byte[] Memory = new byte[0x10000];
        public readonly List<(ushort Address, byte Value, bool Write)> Log = [];

        public byte Read(ushort address)
        {
            Log.Add((address, Memory[address], false));
            return Memory[address];
        }

        public void Write(ushort address, byte value)
        {
            Log.Add((address, value, true));
            Memory[address] = value;
        }
    }

    // CPU stopped at the given PC with the program in memory and an empty log
    static (MOS6502 Cpu, RecordingBus Bus) Cpu(ushort pc, params byte[] program)
    {
        var bus = new RecordingBus();
        bus.Memory[0xFFFC] = (byte)pc;
        bus.Memory[0xFFFD] = (byte)(pc >> 8);
        program.CopyTo(bus.Memory, pc);
        var cpu = new MOS6502 { Bus = bus };
        cpu.Reset();
        bus.Log.Clear();
        return (cpu, bus);
    }

    static void Tick(MOS6502 cpu, int cycles)
    {
        for (int i = 0; i < cycles; i++) cpu.Tick();
    }

    [TestMethod]
    public void AbsoluteIndexed_PageCross_ShouldDoDummyReadFromWrongPage()
    {
        var (cpu, bus) = Cpu(0x0200, 0xBD, 0xFF, 0x10); // LDA $10FF,X
        cpu.X = 1;
        bus.Memory[0x1100] = 0x42;

        Tick(cpu, 5);

        CollectionAssert.AreEqual(
            new ushort[] { 0x0200, 0x0201, 0x0202, 0x1000, 0x1100 },
            bus.Log.Select(access => access.Address).ToArray());
        Assert.AreEqual(0x42, (int)cpu.A);
        Assert.IsTrue(cpu.IsInstructionBoundary);
    }

    [TestMethod]
    public void ReadModifyWrite_ShouldWriteOriginalValueThenResult()
    {
        var (cpu, bus) = Cpu(0x0200, 0xEE, 0x00, 0x03); // INC $0300
        bus.Memory[0x0300] = 0x41;

        Tick(cpu, 6);

        var writes = bus.Log.Where(access => access.Write).ToArray();
        CollectionAssert.AreEqual(new[] { ((ushort)0x0300, (byte)0x41, true), ((ushort)0x0300, (byte)0x42, true) }, writes);
        Assert.AreEqual(6, bus.Log.Count);
    }

    [TestMethod]
    public void Jsr_ShouldPushReturnAddressDuringCycles3And4()
    {
        var (cpu, bus) = Cpu(0x0200, 0x20, 0x34, 0x12); // JSR $1234

        Tick(cpu, 6);

        CollectionAssert.AreEqual(
            new[] { ((ushort)0x01FD, (byte)0x02, true), ((ushort)0x01FC, (byte)0x02, true) },
            bus.Log.Where(access => access.Write).ToArray());
        Assert.AreEqual(0x1234, (int)cpu.PC);
    }

    [TestMethod]
    public void RdyLow_ShouldStopOnReadsButLetWritesComplete()
    {
        var (cpu, bus) = Cpu(0x0200, 0xEE, 0x00, 0x03, 0xEA); // INC $0300 : NOP
        bus.Memory[0x0300] = 0x41;
        bool rdy = true;
        cpu.RdyInput = () => rdy;

        Tick(cpu, 4);  // opcode, low operand, high operand, read of the value
        rdy = false;   // the VIC-II pulls BA low: two write cycles are left
        Tick(cpu, 10);

        Assert.AreEqual(0x42, (int)bus.Memory[0x0300]); // both writes happened
        Assert.AreEqual(6, bus.Log.Count);               // the fetch of NOP (a read) waits
        Assert.IsTrue(cpu.IsInstructionBoundary);

        rdy = true;
        Tick(cpu, 1);
        Assert.AreEqual(0x0203, (int)bus.Log[^1].Address);
    }

    [TestMethod]
    public void AecLow_ShouldStopEvenWrites()
    {
        var (cpu, bus) = Cpu(0x0200, 0x8D, 0x00, 0x03); // STA $0300
        bool aec = true;
        cpu.AecInput = () => aec;

        Tick(cpu, 3);
        aec = false;   // the VIC-II uses the bus in the CPU's half of the cycle too
        Tick(cpu, 5);

        Assert.AreEqual(3, bus.Log.Count);
        aec = true;
        Tick(cpu, 1);
        Assert.IsTrue(bus.Log[^1].Write);
    }

    [TestMethod]
    public void Sei_ShouldStillServeAnIrqAlreadyPending()
    {
        // Interrupts are polled at the start of the last cycle: SEI is too late to block it
        var (cpu, bus) = Cpu(0x0200, 0x78, 0xEA); // SEI : NOP
        bus.Memory[0xFFFE] = 0x00;
        bus.Memory[0xFFFF] = 0x30;
        cpu.Status = 0x20;      // I = 0
        cpu.IrqInput = () => true;

        Tick(cpu, 2 + 7);       // SEI, then the interrupt sequence

        Assert.AreEqual(0x3000, (int)cpu.PC);
        Assert.AreEqual(0x02, (int)bus.Memory[0x01FD]); // return address $0201
        Assert.AreEqual(0x01, (int)bus.Memory[0x01FC]);
    }

    [TestMethod]
    public void Jam_ShouldHaltUntilReset()
    {
        var (cpu, bus) = Cpu(0x0200, 0x02, 0xEA); // JAM

        Tick(cpu, 20);

        Assert.IsTrue(cpu.IsJammed);
        Assert.AreEqual(1, bus.Log.Count);
        cpu.Reset();
        Assert.IsFalse(cpu.IsJammed);
    }

    [TestMethod]
    [DataRow(new byte[] { 0xA9, 0x01 }, 2)]             // LDA #
    [DataRow(new byte[] { 0xA5, 0x10 }, 3)]             // LDA zp
    [DataRow(new byte[] { 0xB5, 0x10 }, 4)]             // LDA zp,X
    [DataRow(new byte[] { 0xAD, 0x00, 0x10 }, 4)]       // LDA abs
    [DataRow(new byte[] { 0xA1, 0x10 }, 6)]             // LDA (zp,X)
    [DataRow(new byte[] { 0xB1, 0x10 }, 5)]             // LDA (zp),Y without page crossing
    [DataRow(new byte[] { 0x91, 0x10 }, 6)]             // STA (zp),Y
    [DataRow(new byte[] { 0x06, 0x10 }, 5)]             // ASL zp
    [DataRow(new byte[] { 0x1E, 0x00, 0x10 }, 7)]       // ASL abs,X
    [DataRow(new byte[] { 0x13, 0x10 }, 8)]             // SLO (zp),Y
    [DataRow(new byte[] { 0x6C, 0x00, 0x10 }, 5)]       // JMP (abs)
    [DataRow(new byte[] { 0x60 }, 6)]                   // RTS
    [DataRow(new byte[] { 0x40 }, 6)]                   // RTI
    [DataRow(new byte[] { 0x48 }, 3)]                   // PHA
    [DataRow(new byte[] { 0x68 }, 4)]                   // PLA
    [DataRow(new byte[] { 0x00 }, 7)]                   // BRK
    public void Instructions_ShouldTakeDocumentedCycles(byte[] program, int cycles)
    {
        var (cpu, bus) = Cpu(0x0200, program);

        int taken = 0;
        do { cpu.Tick(); taken++; } while (!cpu.IsInstructionBoundary);

        Assert.AreEqual(cycles, taken);
        Assert.AreEqual(cycles, bus.Log.Count); // one bus access per cycle
    }
}
