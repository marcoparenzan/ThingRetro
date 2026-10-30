using ThingVCS;
using ThingRetroEmu.Chips;
using ThingVCS7800.A7800;
namespace ThingVCS7800.Tests;
public class MariaIndirectTests
{
    sealed class Memory : IBus
    {
        public byte[] Bytes = new byte[65536];
        public List<ushort> Reads = [];
        public byte Read(ushort address) { Reads.Add(address); return Bytes[address]; }
        public void Write(ushort address, byte value) => Bytes[address] = value;
    }
    static (Maria, Memory) Setup(byte descriptor = 0x60, byte width = 0x3f)
    {
        var memory = new Memory(); var chip = new Maria { Bus = memory };
        chip.WriteRegister(0, 6); chip.WriteRegister(5, 0x3a); chip.WriteRegister(6, 0x7a); chip.WriteRegister(7, 0xba);
        chip.WriteRegister(12, 0x18); chip.WriteRegister(16, 0); chip.WriteRegister(20, 0x1b); chip.WriteRegister(28, 0x40);
        memory.Bytes[0x1801] = 0x19;
        byte[] list = [0,descriptor,0x1a,width,0,0,0]; list.CopyTo(memory.Bytes, 0x1900);
        memory.Bytes[0x1a00] = 2; memory.Bytes[0x1b02] = 0x55; memory.Bytes[0x1b03] = 0xaa;
        return (chip, memory);
    }
    static void Line(Maria chip) { for (int i = 0; i < 17 * 454 + 100; i++) chip.Tick(); }
    [Fact] public void IndirectUsesMapByteAndCharbase()
    {
        var (chip, memory) = Setup(); Line(chip);
        Assert.Equal(0x3a, chip.Framebuffer.Span[320 + 0]); Assert.Contains((ushort)0x1b02, memory.Reads);
        Assert.Equal(0, chip.UnsupportedLines);
    }
    [Fact] public void CwidthFetchesTwoBytesAndAdvancesPosition()
    {
        var (chip, memory) = Setup(); chip.WriteRegister(28, 0x50); Line(chip);
        Assert.Equal(0x3a, chip.Framebuffer.Span[320 + 6]); Assert.Equal(0x7a, chip.Framebuffer.Span[320 + 8]);
        Assert.Contains((ushort)0x1b03, memory.Reads);
    }
    [Fact] public void ExtendedDirectUsesFiveByteHeaderAndIndependentWidth()
    {
        var (chip, memory) = Setup(0x40); memory.Bytes[0x1a00] = 0xff; Line(chip);
        Assert.Equal(0xba, chip.Framebuffer.Span[320 + 0]); Assert.Equal(6, chip.Framebuffer.Span[320 + 8]);
        Assert.DoesNotContain((ushort)0x1b02, memory.Reads);
    }
    [Fact] public void ZoneOffsetAppliesToCharacterDataNotMap()
    {
        var (chip, memory) = Setup(); memory.Bytes[0x1800] = 1; memory.Bytes[0x1c02] = 0xff; Line(chip);
        Assert.Equal(0xba, chip.Framebuffer.Span[320 + 0]); Assert.Contains((ushort)0x1a00, memory.Reads);
        Assert.Contains((ushort)0x1c02, memory.Reads);
    }
    [Fact] public void ExtendedWidthZeroMeans32MapBytes()
    {
        var (chip, memory) = Setup(0x60, 0x20); Line(chip);
        Assert.Contains((ushort)0x1a1f, memory.Reads); Assert.DoesNotContain((ushort)0x1a20, memory.Reads);
    }
    [Fact] public void ExtendedWriteModeAcceptsTransparentData()
    {
        var (chip, _) = Setup(0xc0); Line(chip);
        Assert.Equal(0, chip.UnsupportedLines); Assert.Equal(6, chip.Framebuffer.Span[320 + 0]);
    }
    [Fact] public void CpuDemoFetchesIndirectGraphicsFromRom()
    {
        var host = new DevelopmentComputer(); host.Insert(DevelopmentRoms.IndirectObjects());
        for (int i = 0; i < 30000; i++) host.Board.Tick();
        var chip = Assert.IsType<Maria>(host.Board.MariaSocket.Chip);
        Assert.Equal(0x3a, chip.Framebuffer.Span[320 + 80]); Assert.Equal(0x7a, chip.Framebuffer.Span[320 + 88]);
        Assert.Equal(0xba, chip.Framebuffer.Span[320 + 96]); Assert.Equal(0, chip.UnsupportedLines);
    }
}
