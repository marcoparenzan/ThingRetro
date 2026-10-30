using ThingVCS;
using ThingRetroEmu.Chips;
namespace ThingVCS7800.Tests;

public class InitialDliTimingTests
{
    sealed class Memory : IBus
    {
        public byte[] Data = new byte[65536];
        public List<(long Clock, ushort Address)> Reads = [];
        public Maria Chip = null!;
        public byte Read(ushort address) { Reads.Add((Chip.MasterClocks, address)); return Data[address]; }
        public void Write(ushort address, byte value) => Data[address] = value;
    }
    static (Maria Chip, Memory Bus) Setup(bool dli)
    {
        var bus = new Memory(); var chip = new Maria { Bus = bus }; bus.Chip = chip;
        chip.WriteRegister(12, 0x18); chip.WriteRegister(28, 0x40);
        bus.Data[0x1800] = dli ? (byte)0x81 : (byte)1;
        bus.Data[0x1801] = 0x19;
        byte[] list = [0, 31, 0x20, 0, 0, 0]; list.CopyTo(bus.Data, 0x1900);
        return (chip, bus);
    }
    static void Advance(Maria chip, int clocks) { for (int i = 0; i < clocks; i++) chip.Tick(); }
    [Fact] public void InitialEdgeFollowsGraphicsAndTerminatorWithSeventeenClockCost()
    {
        var (plain, plainBus) = Setup(false); var (chip, bus) = Setup(true);
        Advance(plain, 16 * 454 + 200); Advance(chip, 16 * 454 + 200);
        Assert.Equal(17, chip.DliOverheadMasterClocks);
        // HALT release is rounded to a CPU boundary; elapsed overhead itself is exactly 17.
        Assert.InRange(chip.LastDmaClocks - plain.LastDmaClocks, 16, 20);
        Assert.Equal(plainBus.Reads.Select(r => r.Address), bus.Reads.Select(r => r.Address));
        Assert.True(chip.LastDliMasterClock > bus.Reads.Single(r => r.Address == 0x2100).Clock);
        Assert.Equal(bus.Reads.Last().Clock, chip.LastDliMasterClock);
        Advance(chip, 454); Assert.Equal(1, chip.DisplayListInterrupts);
    }
    [Fact] public void ResetDuringInitialOverheadCancelsPendingNmiAndHalt()
    {
        var (chip, _) = Setup(true); Advance(chip, 16 * 454);
        while (!chip.Nmi && chip.Scanline == 16) chip.Tick();
        Assert.True(chip.Nmi); Assert.True(chip.DmaBusy);
        chip.Reset(); Advance(chip, 20);
        Assert.False(chip.Nmi); Assert.False(chip.DmaBusy);
        Assert.Equal(0, chip.DisplayListInterrupts); Assert.Equal(0, chip.DliOverheadMasterClocks);
    }
    [Theory][InlineData(true)][InlineData(false)]
    public void HeldNmiIsOneEdgeAndNewEdgeAfterRtiIsServiced(bool haltInitially)
    {
        var bus = new Memory(); byte[] main = [0x4c, 0, 0xc0]; main.CopyTo(bus.Data, 0xc000);
        byte[] handler = [0xee, 0, 0x20, 0x40]; handler.CopyTo(bus.Data, 0xc600);
        bus.Data[0xfffd] = 0xc0; bus.Data[0xfffb] = 0xc6;
        bus.Chip = new Maria(); bool halt = haltInitially, nmi = true;
        var cpu = new Sally { Bus = bus, HaltInput = () => halt, NmiInput = () => nmi }; cpu.Reset();
        for (int i = 0; i < 100; i++) cpu.Tick();
        halt = false; for (int i = 0; i < 500; i++) cpu.Tick();
        Assert.Equal(1, bus.Data[0x2000]);
        nmi = false; for (int i = 0; i < 20; i++) cpu.Tick();
        nmi = true; for (int i = 0; i < 500; i++) cpu.Tick();
        Assert.Equal(2, bus.Data[0x2000]);
    }
}
