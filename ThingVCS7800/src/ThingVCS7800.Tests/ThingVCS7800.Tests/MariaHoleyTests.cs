using ThingVCS;
using ThingRetroEmu.Chips;
namespace ThingVCS7800.Tests;
public class MariaHoleyTests
{
    sealed class Memory : IBus
    {
        public byte[] Data = new byte[65536];
        public List<ushort> Reads = [];
        public byte Read(ushort address) { Reads.Add(address); return Data[address]; }
        public void Write(ushort address, byte value) => Data[address] = value;
    }
    static (Maria, Memory) Setup(byte flags, ushort graphics, bool indirect = false, bool pair = false, byte width = 31)
    {
        var memory = new Memory(); var chip = new Maria { Bus = memory };
        chip.WriteRegister(0, 6); chip.WriteRegister(1, 42);
        chip.WriteRegister(12, 0x18); chip.WriteRegister(16, 0);
        chip.WriteRegister(28, (byte)(pair ? 0x50 : 0x40));
        memory.Data[0x1800] = flags; memory.Data[0x1801] = 0x19;
        if (indirect)
        {
            chip.WriteRegister(20, (byte)(graphics >> 8));
            byte[] list = [0,0x60,0x1a,width,0,0,0]; list.CopyTo(memory.Data, 0x1900);
            memory.Data[0x1a00] = (byte)graphics;
        }
        else
        {
            byte[] list = [(byte)graphics,width,(byte)(graphics >> 8),0,0,0]; list.CopyTo(memory.Data, 0x1900);
        }
        memory.Data[graphics] = 0x55; return (chip, memory);
    }
    static void Line(Maria chip) { for (int i = 0; i < 17 * 454 + 90; i++) chip.Tick(); }
    [Theory]
    [InlineData(0,0x8800,false)][InlineData(0x20,0x8800,true)][InlineData(0x20,0x9000,false)]
    [InlineData(0x40,0x9000,true)][InlineData(0x40,0x8800,false)][InlineData(0x60,0x9800,true)]
    [InlineData(0x20,0xc800,true)][InlineData(0x40,0xd000,true)][InlineData(0x60,0x8000,false)]
    public void DllFlagsSelectAddressMasks(int flags, int address, bool suppressed)
    {
        var (chip, memory) = Setup((byte)flags, (ushort)address); Line(chip);
        Assert.Equal(suppressed ? 6 : 42, chip.Framebuffer.Span[320 + 0]);
        Assert.Equal(suppressed ? 2 : 0, chip.SuppressedGraphicsFetches);
        Assert.Equal(!suppressed, memory.Reads.Contains((ushort)address)); Assert.Equal(0, chip.UnsupportedLines);
    }
    [Fact] public void DirectFetchCanCrossIntoHoleyRegion()
    {
        var (chip, memory) = Setup(0x20, 0x87ff, width: 30); memory.Data[0x8800] = 0xff;
        Line(chip); Assert.Contains((ushort)0x87ff, memory.Reads); Assert.DoesNotContain((ushort)0x8800, memory.Reads);
        Assert.Equal(42, chip.Framebuffer.Span[320 + 6]); Assert.Equal(6, chip.Framebuffer.Span[320 + 8]);
    }
    [Fact] public void IndirectStillReadsMapButSuppressesCwidthPair()
    {
        var (chip, memory) = Setup(0x20, 0x8800, indirect: true, pair: true); Line(chip);
        Assert.Contains((ushort)0x1a00, memory.Reads);
        Assert.DoesNotContain((ushort)0x8800, memory.Reads); Assert.DoesNotContain((ushort)0x8801, memory.Reads);
        Assert.Equal(4, chip.SuppressedGraphicsFetches); Assert.Equal(6, chip.Framebuffer.Span[320 + 0]);
    }
    [Fact] public void SuppressedIndirectCharactersStillConsumeDistributedMapReads()
    {
        var (chip, memory) = Setup(0x20, 0x8800, indirect: true, width: 0);
        // Inspect the DMA result before the following scanline replaces its diagnostics.
        for (int i = 0; i < 16 * 454 + 300; i++) chip.Tick();
        Assert.Equal(32, chip.SuppressedGraphicsFetches);
        // Startup 16 + header 10 + 32 map reads * 3 + terminator 2 + shutdown 6 = 130;
        // bus release rounds to the next four-master-clock phase.
        Assert.Equal(124, chip.LastDmaClocks); // No successor/shutdown on bootstrap line.
        Assert.Equal(0, chip.TruncatedLines);
        Assert.Contains((ushort)0x1a1f, memory.Reads);
        Assert.DoesNotContain((ushort)0x8800, memory.Reads);
    }
    [Fact] public void ResetClearsSuppressionCountersAndZoneFlags()
    {
        var (chip, _) = Setup(0x40, 0x9000); Line(chip); Assert.Equal(2, chip.SuppressedGraphicsFetches);
        chip.Reset(); Assert.Equal(0, chip.SuppressedGraphicsFetches);
    }
}
