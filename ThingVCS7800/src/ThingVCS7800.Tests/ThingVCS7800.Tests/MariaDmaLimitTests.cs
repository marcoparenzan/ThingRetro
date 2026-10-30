using ThingVCS;
using ThingRetroEmu.Chips;
namespace ThingVCS7800.Tests;
public class MariaDmaLimitTests
{
    sealed class Memory : IBus
    {
        public byte[] Data = new byte[65536];
        public List<ushort> Reads = [];
        public byte Read(ushort address) { Reads.Add(address); return Data[address]; }
        public void Write(ushort address, byte value) => Data[address] = value;
    }
    static (Maria, Memory) Setup(bool indirect = false, bool pair = false)
    {
        var memory = new Memory(); var chip = new Maria { Bus = memory };
        chip.WriteRegister(0, 6); chip.WriteRegister(1, 42);
        chip.WriteRegister(12, 0x18); chip.WriteRegister(16, 0); chip.WriteRegister(20, 0x30);
        chip.WriteRegister(28, (byte)(pair ? 0x50 : 0x40));
        memory.Data[0x1801] = 0x19;
        int pointer = 0x1900;
        for (int obj = 0; obj < 20; obj++)
        {
            int address = 0x2000 + obj * 256;
            byte[] header = indirect ? [(byte)address,0x60,(byte)(address >> 8),1,0] : [(byte)address,1,(byte)(address >> 8),0];
            header.CopyTo(memory.Data, pointer); pointer += header.Length;
            for (int i = 0; i < 31; i++) memory.Data[address + i] = 0x55;
        }
        memory.Data[0x3055] = memory.Data[0x3056] = 0x55;
        return (chip, memory);
    }
    static void StartLine(Maria chip) { for (int i = 0; i < 16 * 454 + 453; i++) chip.Tick(); }
    [Fact] public void OverloadedListStopsBeforeNextObjectGraphics()
    {
        var (chip, memory) = Setup(); StartLine(chip);
        Assert.Equal(1, chip.TruncatedLines); Assert.InRange(chip.LastDmaClocks, 400, 426);
        Assert.True(chip.WaitSync);
        Assert.Contains((ushort)0x231e, memory.Reads); Assert.DoesNotContain((ushort)0x2400, memory.Reads);
        Assert.InRange(memory.Reads.Count, 1, 150);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void IndirectBudgetBoundsMapAndCharacterReads(bool pair)
    {
        var (chip, memory) = Setup(true, pair); StartLine(chip);
        Assert.Equal(1, chip.TruncatedLines); Assert.True(chip.WaitSync);
        Assert.InRange(memory.Reads.Count, 1, 160);
        Assert.DoesNotContain((ushort)0x2300, memory.Reads);
    }
    [Fact] public void SaturatedWaitReleasesAtBoundaryAndResetClearsIt()
    {
        var (chip, _) = Setup(); StartLine(chip);
        Assert.True(chip.WaitSync); chip.Tick(); Assert.False(chip.WaitSync);
        chip.Reset(); Assert.False(chip.DmaBusy); Assert.Equal(0, chip.TruncatedLines);
    }
}
