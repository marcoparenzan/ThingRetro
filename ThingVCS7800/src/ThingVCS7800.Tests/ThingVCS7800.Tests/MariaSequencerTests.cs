using ThingRetroEmu.Chips;
using ThingVCS;
using ThingVCS7800.A7800;
namespace ThingVCS7800.Tests;

public class MariaSequencerTests
{
    sealed class Memory : IBus
    {
        public byte[] Data { get; } = new byte[65536];
        public Func<long> Clock = () => 0;
        public List<(long Clock, ushort Address)> Reads { get; } = [];
        public byte Read(ushort address) { Reads.Add((Clock(), address)); return Data[address]; }
        public void Write(ushort address, byte value) => Data[address] = value;
    }
    static (Maria, Memory) Setup(bool dli = false)
    {
        var memory = new Memory(); var maria = new Maria { Bus = memory };
        memory.Clock = () => maria.MasterClocks;
        maria.WriteRegister(0, 6); maria.WriteRegister(1, 42); maria.WriteRegister(2, 99);
        maria.WriteRegister(12, 0x18); maria.WriteRegister(28, 0x40);
        memory.Data[0x1800] = dli ? (byte)0x80 : (byte)0;
        memory.Data[0x1801] = 0x19;
        byte[] list = [0, 30, 0x1a, 0, 0, 0]; list.CopyTo(memory.Data, 0x1900);
        memory.Data[0x1a00] = memory.Data[0x1a01] = 0x55;
        return (maria, memory);
    }
    static void Advance(Maria maria, int clocks) { for (int i = 0; i < clocks; i++) maria.Tick(); }
    [Fact] public void DppIsAcquiredAtDmaStartRatherThanDotZero()
    {
        var (maria, memory) = Setup();
        Advance(maria, 16 * 454 + 20);
        memory.Data[0x1811] = 0x19;
        maria.WriteRegister(16, 0x10);
        Advance(maria, 100);
        Assert.Contains(memory.Reads, r => r.Address == 0x1810);
        Assert.DoesNotContain(memory.Reads, r => r.Address == 0x1800);
    }
    [Fact] public void BusReadsAreDistributedAfterDmaStartNotAtLineStart()
    {
        var (maria, memory) = Setup();
        Advance(maria, 16 * 454 + 28);
        Assert.Empty(memory.Reads); Assert.False(maria.DmaBusy);
        Advance(maria, 100);
        Assert.NotEmpty(memory.Reads);
        Assert.All(memory.Reads, read => Assert.True(read.Clock >= 16 * 454 + 28));
        for (int i = 1; i < memory.Reads.Count; i++)
            Assert.True(memory.Reads[i].Clock - memory.Reads[i - 1].Clock >= 2);
        var graphics = memory.Reads.Where(r => r.Address is 0x1a00 or 0x1a01).ToArray();
        Assert.Equal(2, graphics.Length); Assert.Equal(3, graphics[1].Clock - graphics[0].Clock);
    }
    [Fact] public void LaterGraphicFetchSeesNewMemoryRatherThanPrefetchedData()
    {
        var (maria, memory) = Setup();
        Advance(maria, 16 * 454);
        while (!memory.Reads.Any(r => r.Address == 0x1a00)) maria.Tick();
        Assert.DoesNotContain(memory.Reads, r => r.Address == 0x1a01);
        memory.Data[0x1a01] = 0xaa;
        while (maria.Scanline < 17 || maria.Dot < 90) maria.Tick();
        Assert.Equal(42, maria.Framebuffer.Span[320]);
        Assert.Equal(99, maria.Framebuffer.Span[328]);
    }
    [Fact] public void HaltEdgesAlignAcrossEvenAndOddScanlinePhases()
    {
        var (maria, _) = Setup();
        var rising = new List<long>(); var falling = new List<long>();
        for (int i = 0; i < 19 * 454; i++)
        {
            bool before = maria.DmaBusy; maria.Tick();
            if (!before && maria.DmaBusy) rising.Add(maria.MasterClocks - 1);
            if (before && !maria.DmaBusy) falling.Add(maria.MasterClocks - 1);
        }
        Assert.Equal(3, rising.Count); Assert.Equal(3, falling.Count);
        Assert.All(rising.Concat(falling), clock => Assert.Equal(0, clock & 3));
    }
    [Fact] public void DliOccursAfterDllFetchWhileCpuIsHalted()
    {
        var (maria, memory) = Setup(dli: true);
        Advance(maria, 16 * 454 + 34); Assert.False(maria.Nmi);
        maria.Tick(); Assert.False(maria.Nmi); // DLL acquisition alone must not signal NMI.
        while (!maria.Nmi && maria.Scanline == 16) maria.Tick();
        Assert.True(maria.Nmi); Assert.True(maria.DmaBusy);
        Assert.Contains(memory.Reads, r => r.Address == 0x1802);
        Advance(maria, 8); Assert.False(maria.Nmi); Assert.True(maria.DmaBusy);
    }
    [Fact] public void ResetOrRemovingBusCancelsPendingFetches()
    {
        var (maria, memory) = Setup(); Advance(maria, 16 * 454 + 35);
        int reads = memory.Reads.Count; maria.Bus = null; Advance(maria, 100);
        Assert.Equal(reads, memory.Reads.Count); Assert.False(maria.DmaBusy);
        maria.Reset(); Assert.Equal(0, maria.MasterClocks); Assert.False(maria.Nmi);
    }
    sealed class CpuBus(Board board, Sally cpu) : IBus
    {
        public int Accesses;
        public byte Read(ushort address) { Assert.False(cpu.HaltInput()); Accesses++; return board.Read(address); }
        public void Write(ushort address, byte value) { Assert.False(cpu.HaltInput()); Accesses++; board.Write(address, value); }
    }
    [Fact] public void BoardCpuDoesNotAccessBusDuringMariaDmaAndResumesAfterward()
    {
        var host = new DevelopmentComputer(); host.Insert(DevelopmentRoms.DirectObjects());
        var maria = Assert.IsType<Maria>(host.Board.MariaSocket.Chip);
        var cpu = Assert.IsType<Sally>(host.Board.CpuSocket.Chip);
        var bus = new CpuBus(host.Board, cpu); cpu.Bus = bus;
        while (!maria.DmaBusy) host.Board.Tick();
        int before = bus.Accesses;
        while (maria.DmaBusy)
        {
            host.Board.Tick();
            if (maria.DmaBusy) Assert.Equal(before, bus.Accesses);
        }
        // The slot that releases HALT at PHI1 may resume the CPU at its PHI2 edge.
        Assert.True(bus.Accesses > before);
        for (int i = 0; i < 10; i++) host.Board.Tick();
        Assert.True(bus.Accesses > before);
    }
}
