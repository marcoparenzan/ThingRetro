using ThingVCS;
using ThingRetroEmu.Chips;
using ThingVCS7800.A7800;
namespace ThingVCS7800.Tests;
public class Maria320Tests
{
    sealed class Memory : IBus
    {
        public byte[] Data = new byte[65536];
        public byte Read(ushort address) => Data[address];
        public void Write(ushort address, byte value) => Data[address] = value;
    }
    static (Maria, Memory) Setup(int mode, bool writeMode, byte data, byte paletteWidth = 31)
    {
        var memory = new Memory(); var chip = new Maria { Bus = memory };
        chip.WriteRegister(0, 90);
        for (int i = 1; i < 32; i++) if ((i & 3) != 0) chip.WriteRegister(i, (byte)i);
        chip.WriteRegister(12, 0x18); chip.WriteRegister(16, 0); chip.WriteRegister(28, (byte)(0x40 | mode));
        memory.Data[0x1801] = 0x19;
        byte[] list = [0,(byte)(writeMode ? 0xc0 : 0x40),0x1a,paletteWidth,0,0,0]; list.CopyTo(memory.Data, 0x1900);
        memory.Data[0x1a00] = data; return (chip, memory);
    }
    static void Line(Maria chip) { for (int i = 0; i < 17 * 454 + 90; i++) chip.Tick(); }
    [Theory]
    [InlineData(3,false,0x55,0x3f,90,6,90,6)] // 320A
    [InlineData(2,true,0xe7,0x1f,2,3,3,1)] // 320B
    [InlineData(3,true,0xe7,0x1f,6,6,14,90)] // 320C
    [InlineData(2,false,0x55,0x3f,90,3,90,3)] // 320D
    [InlineData(2,true,0xe7,0xbf,18,19,19,17)]
    [InlineData(3,true,0xe7,0xbf,22,22,30,90)]
    public void ReadAndWriteModesSelectCorrectHalfCellPalette(int mode, bool writeMode, int data, int palette, int a, int b, int c, int d)
    {
        var (chip, _) = Setup(mode, writeMode, (byte)data, (byte)palette); Line(chip);
        Assert.Equal(new byte[] {(byte)a,(byte)b,(byte)c,(byte)d}, chip.Framebuffer.Span[320..(320 + 4)].ToArray());
        Assert.Equal(0, chip.UnsupportedLines);
    }
    [Fact] public void ReadModeCanChangeAfterGraphicsWereFetched()
    {
        var (chip, _) = Setup(0, false, 0x55, 0x3f);
        for (int i = 0; i < 16 * 454 + 1; i++) chip.Tick();
        chip.WriteRegister(28, 0x43);
        for (int i = 0; i < 454 + 89; i++) chip.Tick();
        Assert.Equal(90, chip.Framebuffer.Span[320 + 0]); Assert.Equal(6, chip.Framebuffer.Span[320 + 1]);
    }
    [Fact] public void CpuDemoUses320OutputNot160Duplication()
    {
        var host = new DevelopmentComputer(); host.Insert(DevelopmentRoms.WriteModeObjects(3));
        for (int i = 0; i < 30000; i++) host.Board.Tick();
        var chip = Assert.IsType<Maria>(host.Board.MariaSocket.Chip);
        Assert.Equal(6, chip.Framebuffer.Span[320 + 80]); Assert.Equal(0x6a, chip.Framebuffer.Span[320 + 81]);
        Assert.Equal(0xea, chip.Framebuffer.Span[320 + 88]); Assert.Equal(0xea, chip.Framebuffer.Span[320 + 89]);
    }
    [Fact] public void DemoRejectsInvalidMode() => Assert.Throws<ArgumentOutOfRangeException>(() => DevelopmentRoms.WriteModeObjects(4));
}
