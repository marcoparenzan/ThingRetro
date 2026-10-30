using ThingRetroEmu.Chips;
using ThingVCS;
using ThingVCS7800.A7800;
namespace ThingVCS7800.Tests;
public class MariaDisplayListTests
{
    sealed class Memory : IBus
    {
        public byte[] Data { get; } = new byte[65536];
        public byte Read(ushort address) => Data[address];
        public void Write(ushort address, byte value) => Data[address] = value;
    }
    static (Maria, Memory) Setup()
    {
        var memory = new Memory(); var chip = new Maria { Bus = memory };
        chip.WriteRegister(0, 6); chip.WriteRegister(5, 0x3a); chip.WriteRegister(6, 0x7a); chip.WriteRegister(7, 0xba);
        chip.WriteRegister(12, 0x18); chip.WriteRegister(16, 0); chip.WriteRegister(28, 0x40);
        memory.Data[0x1801] = 0x19; memory.Data[0x1802] = 0;
        byte[] list = [0,0x3f,0x1a,2,0,0]; list.CopyTo(memory.Data, 0x1900);
        memory.Data[0x1a00] = 0x1b;
        return (chip, memory);
    }
    static void Advance(Maria chip, int count) { for (int i = 0; i < count; i++) chip.Tick(); }
    [Fact] public void DirectByteExpandsToDoubleWidthPalettePixels()
    {
        var (chip, _) = Setup(); Advance(chip, 17 * 454 + 80);
        Assert.Equal(new byte[] {6,6,6,6,6,6,0x3a,0x3a,0x7a,0x7a,0xba,0xba}, chip.Framebuffer.Span[320..(320 + 12)].ToArray());
    }
    [Fact] public void ZoneOffsetDecrementsByGraphicsPageThenAdvancesDll()
    {
        var (chip, memory) = Setup(); memory.Data[0x1800] = 1; memory.Data[0x1b00] = 0xff;
        Advance(chip, 19 * 454 + 80);
        Assert.Equal(0xba, chip.Framebuffer.Span[320 + 4]);
        Assert.Equal(0xba, chip.Framebuffer.Span[640 + 4]); // Initial offset held on bootstrap.
        Assert.Equal(6, chip.Framebuffer.Span[960 + 4]);
        Assert.Equal(0x3a, chip.Framebuffer.Span[960 + 6]);
    }
    [Fact] public void GraphicsAreLatchedButPaletteChangesAtOutputTime()
    {
        var (chip, memory) = Setup(); Advance(chip, 16 * 454 + 90);
        memory.Data[0x1a00] = 0xff; chip.WriteRegister(5, 0x99);
        Advance(chip, 454 + 79); Assert.Equal(0x99, chip.Framebuffer.Span[320 + 6]); Assert.Equal(6, chip.Framebuffer.Span[320 + 4]);
    }
    [Theory][InlineData(0x42)][InlineData(0x43)]
    public void ReadModes320AreAccepted(int control)
    {
        var (chip, _) = Setup(); chip.WriteRegister(28, (byte)control);
        Advance(chip, 17 * 454 + 80); Assert.Equal(0, chip.UnsupportedLines); Assert.NotEqual(0x3a, chip.Framebuffer.Span[320 + 6]);
    }
    [Fact] public void CpuTestRomUsesDllAndGraphicsFromCartridge()
    {
        var host = new DevelopmentComputer(); host.Insert(DevelopmentRoms.DirectObjects());
        for (int i = 0; i < 30000; i++) host.Board.Tick();
        var chip = Assert.IsType<Maria>(host.Board.MariaSocket.Chip);
        Assert.Equal(0x3a, chip.Framebuffer.Span[320 + 80]); Assert.Equal(0x7a, chip.Framebuffer.Span[320 + 88]);
        Assert.True(chip.GraphicsReads > 0); Assert.Equal(0, chip.UnsupportedLines);
    }
}
