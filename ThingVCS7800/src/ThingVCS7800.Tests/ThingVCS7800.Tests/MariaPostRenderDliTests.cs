using ThingVCS;
using ThingRetroEmu.Chips;
namespace ThingVCS7800.Tests;

public class MariaPostRenderDliTests
{
    sealed class Memory : IBus
    {
        public byte[] Data = new byte[65536];
        public List<ushort> Reads = [];
        public byte Read(ushort address) { Reads.Add(address); return Data[address]; }
        public void Write(ushort address, byte value) => Data[address] = value;
    }
    static (Maria chip, Memory memory, List<MariaDmaTrace> traces) Setup(bool nextDli)
    {
        var memory = new Memory(); var chip = new Maria { Bus = memory }; List<MariaDmaTrace> traces = [];
        chip.DmaTrace = traces.Add;
        chip.WriteRegister(12, 0x18); chip.WriteRegister(28, 0x40);
        memory.Data[0x1801] = memory.Data[0x1804] = 0x19;
        memory.Data[0x1803] = nextDli ? (byte)0x80 : (byte)0;
        byte[] list = [0,31,0x20,0,0,0]; list.CopyTo(memory.Data, 0x1900);
        return (chip, memory, traces);
    }
    static void Advance(Maria chip, int clocks) { for (int i = 0; i < clocks; i++) chip.Tick(); }
    [Fact] public void BootstrapDoesNotFetchSuccessorEvenForZeroOffsetZone()
    {
        var (chip, memory, trace) = Setup(true);
        Advance(chip, 16 * 454 + 200);
        Assert.DoesNotContain((ushort)0x1803, memory.Reads);
        Assert.Equal(0, chip.DisplayListInterrupts);
        Assert.Equal(0, chip.DliOverheadMasterClocks);
        Assert.Equal((ushort)0x1800, Assert.Single(trace).Dll);
        Advance(chip, 454);
        Assert.Contains((ushort)0x1803, memory.Reads);
        Assert.Equal(1, chip.DisplayListInterrupts);
    }
    [Fact] public void NextZoneDliAddsSeventeenPostRenderClocksNotGraphicFetches()
    {
        var (plain, plainMemory, plainTrace) = Setup(false);
        var (dli, dliMemory, dliTrace) = Setup(true);
        Advance(plain, 17 * 454 + 200); Advance(dli, 17 * 454 + 200);
        Assert.Equal(17, dli.DliOverheadMasterClocks);
        Assert.Equal(17, dliTrace[1].Budget - plainTrace[1].Budget);
        Assert.Equal(plainMemory.Reads, dliMemory.Reads);
        Assert.Equal(1, dli.DisplayListInterrupts); Assert.Equal(0, plain.DisplayListInterrupts);
        Assert.Equal(0, dli.TruncatedLines); Assert.Equal(0, dli.DliOverheadClampedLines);
    }
    [Fact] public void PrefetchedEntryIsNotReadAgainOrPulsedAtNextLineStartup()
    {
        var (chip, memory, _) = Setup(true);
        Advance(chip, 17 * 454 + 200);
        Assert.Equal(1, memory.Reads.Count(a => a == 0x1803));
        memory.Data[0x1804] = 0x40; // Must not replace the DLL pointer already acquired.
        Advance(chip, 454);
        Assert.Equal(1, memory.Reads.Count(a => a == 0x1803));
        Assert.Equal(1, chip.DisplayListInterrupts);
        Assert.DoesNotContain((ushort)0x4001, memory.Reads);
    }
    [Fact] public void NmiEdgeOccursDuringHaltAndOverheadCountsOnlyElapsedClocks()
    {
        var (chip, _, _) = Setup(true);
        Advance(chip, 17 * 454);
        while (chip.DisplayListInterrupts == 0 && chip.Scanline == 17) chip.Tick();
        Assert.Equal(17, chip.Scanline); Assert.True(chip.Nmi); Assert.True(chip.DmaBusy);
        Assert.Equal(chip.MasterClocks - 1, chip.LastDliMasterClock);
        Assert.Equal(0, chip.DliOverheadMasterClocks);
        Advance(chip, 5); Assert.Equal(5, chip.DliOverheadMasterClocks);
        chip.Bus = null; Advance(chip, 30);
        Assert.Equal(5, chip.DliOverheadMasterClocks); Assert.False(chip.DmaBusy);
        chip.Reset(); Assert.Equal(-1, chip.LastDliMasterClock); Assert.Equal(0, chip.DliOverheadMasterClocks);
    }
    [Fact] public void PostRenderOverheadIsClampedWithoutTruncatingCompletedGraphics()
    {
        var (chip, memory, trace) = Setup(true);
        int pointer = 0x1900;
        for (int obj = 0; obj < 4; obj++)
        {
            byte width = obj == 3 ? (byte)6 : (byte)1; // 31,31,31,26 bytes: 407 clocks including terminator.
            byte[] header = [0,width,(byte)(0x20 + obj),0];
            header.CopyTo(memory.Data, pointer); pointer += 4;
        }
        Advance(chip, 17 * 454 + 453);
        Assert.Equal(1, chip.DliOverheadClampedLines); Assert.Equal(0, chip.TruncatedLines);
        Assert.Equal(1, chip.DisplayListInterrupts); Assert.True(chip.WaitSync);
        chip.Tick(); Assert.False(chip.WaitSync);
        Assert.True(Assert.Single(trace, line => line.Scanline == 17).Terminated);
        Assert.InRange(chip.DliOverheadMasterClocks, 1, 16);
    }
    [Fact] public void FinalVisibleLineDoesNotLoadOrInterruptAnInvisibleZone()
    {
        var (chip, memory, _) = Setup(false);
        // One-line empty zones all pointing at an empty list. Set only the invisible successor's DLI bit.
        for (int zone = 0; zone < 242; zone++) memory.Data[0x1800 + zone * 3 + 1] = 0x1a;
        memory.Data[0x1800 + 242 * 3] = 0x80;
        Advance(chip, 258 * 454);
        Assert.Equal(0, chip.DisplayListInterrupts);
        Assert.DoesNotContain((ushort)(0x1800 + 242 * 3), memory.Reads);
    }
}
