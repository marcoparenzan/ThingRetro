using ThingVCS;
using ThingRetroEmu.Chips;
using ThingVCS7800.A7800;
namespace ThingVCS7800.Tests;
public class MariaWriteModeTests
{
    sealed class Memory : IBus
    {
        public byte[] Data = new byte[65536];
        public byte Read(ushort address) => Data[address];
        public void Write(ushort address, byte value) => Data[address] = value;
    }
    static (Maria, Memory) Setup(byte data, byte paletteWidth = 31)
    {
        var mem = new Memory(); var chip = new Maria { Bus = mem };
        chip.WriteRegister(0, 90);
        for (int i = 1; i < 32; i++) if ((i & 3) != 0) chip.WriteRegister(i, (byte)i);
        chip.WriteRegister(12, 0x18); chip.WriteRegister(16, 0); chip.WriteRegister(28, 0x40);
        mem.Data[0x1801] = 0x19;
        byte[] list = [0,0xc0,0x1a,paletteWidth,0,0,0]; list.CopyTo(mem.Data, 0x1900);
        mem.Data[0x1a00] = data; return (chip, mem);
    }
    static void Line(Maria chip) { for (int i = 0; i < 17 * 454 + 90; i++) chip.Tick(); }
    [Theory][InlineData(0xe7,7,14)][InlineData(0xff,15,15)][InlineData(0,90,90)][InlineData(0x1b,90,13)]
    public void WriteModeCombinesBitPairsAndOutputsTwoCells(int data, int first, int second)
    {
        var (chip, _) = Setup((byte)data); Line(chip);
        Assert.Equal(new byte[] {(byte)first,(byte)first,(byte)second,(byte)second,90,90}, chip.Framebuffer.Span[320..(320 + 6)].ToArray());
    }
    [Fact] public void OnlyHighPaletteBitParticipatesIn160B()
    {
        var (chip, _) = Setup(0xe7, 0xbf); Line(chip);
        Assert.Equal(23, chip.Framebuffer.Span[320 + 0]); Assert.Equal(30, chip.Framebuffer.Span[320 + 2]);
    }
    [Fact] public void NormalHeadersRetainExtendedWriteMode()
    {
        var (chip, mem) = Setup(0xe7);
        byte[] next = [1,31,0x1a,2,0,0]; next.CopyTo(mem.Data, 0x1905); mem.Data[0x1a01] = 0xff;
        Line(chip); Assert.Equal(15, chip.Framebuffer.Span[320 + 4]); Assert.Equal(15, chip.Framebuffer.Span[320 + 6]);
        Assert.Equal(90, chip.Framebuffer.Span[320 + 8]);
    }
    [Fact] public void LaterExtendedHeaderCanReturnTo160A()
    {
        var (chip, mem) = Setup(0xe7);
        byte[] next = [1,0x40,0x1a,31,2,0,0]; next.CopyTo(mem.Data, 0x1905); mem.Data[0x1a01] = 0x55;
        Line(chip); Assert.Equal(1, chip.Framebuffer.Span[320 + 4]); Assert.Equal(1, chip.Framebuffer.Span[320 + 10]);
    }
    [Fact] public void CpuWriteModeDemoUsesPaletteAndGraphics()
    {
        var host = new DevelopmentComputer(); host.Insert(DevelopmentRoms.WriteModeObjects());
        for (int i = 0; i < 30000; i++) host.Board.Tick();
        var chip = Assert.IsType<Maria>(host.Board.MariaSocket.Chip);
        Assert.Equal(0x5a, chip.Framebuffer.Span[320 + 80]); Assert.Equal(0xfa, chip.Framebuffer.Span[320 + 88]);
        Assert.Equal(0, chip.UnsupportedLines);
    }
}
