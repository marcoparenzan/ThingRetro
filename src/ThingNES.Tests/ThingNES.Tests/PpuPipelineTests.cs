using ThingNES;

namespace ThingNES.Tests;

public class PpuPipelineTests
{
    static Ppu Create()
    {
        var image = new byte[16400]; "NES\u001a"u8.CopyTo(image); image[4] = 1;
        var p = new Ppu(Cartridge.Load(image)); Array.Fill(p.Oam, (byte)255); return p;
    }
    static void At(Ppu p, int line, int dot) { while (p.Scanline != line || p.Dot != dot) p.Tick(); }
    static void Address(Ppu p, ushort value) { p.ReadRegister(2); p.WriteRegister(6, (byte)(value >> 8)); p.WriteRegister(6, (byte)value); }
    [Fact] public void ScrollRegistersPackCoarseFineAndSharedToggle()
    {
        var p = Create(); p.WriteRegister(0, 3); p.WriteRegister(5, 0xab);
        Assert.Equal(3, p.FineX); Assert.True(p.WriteToggle); Assert.Equal(21, p.TemporaryAddress & 31);
        p.WriteRegister(5, 0xcd); Assert.False(p.WriteToggle);
        Assert.Equal(25, (p.TemporaryAddress >> 5) & 31); Assert.Equal(5, (p.TemporaryAddress >> 12) & 7);
        Assert.Equal(3, (p.TemporaryAddress >> 10) & 3);
        p.WriteRegister(5, 0); p.ReadRegister(2); Assert.False(p.WriteToggle);
    }
    [Fact] public void CoarseXWrapsAndChangesHorizontalNametableAtDotEight()
    {
        var p = Create(); Address(p, 31); p.WriteRegister(1, 8); At(p, 0, 8);
        Assert.Equal(31, p.VramAddress); p.Tick(); Assert.Equal(0x400, p.VramAddress);
    }
    [Fact] public void HorizontalTransferWaitsUntilDot257()
    {
        var p = Create(); p.WriteRegister(1, 8); At(p, 0, 256);
        p.WriteRegister(0, 1); p.WriteRegister(5, 40); p.WriteRegister(5, 0);
        p.Tick(); Assert.NotEqual(0x405, p.VramAddress & 0x41f);
        p.Tick(); Assert.Equal(0x405, p.VramAddress & 0x41f);
    }
    [Theory] [InlineData(239, 0x800)] [InlineData(255, 0)]
    public void VerticalWrapDistinguishesCoarseRows29And31(byte scroll, int expected)
    {
        var p = Create(); p.WriteRegister(5, 0); p.WriteRegister(5, scroll); p.WriteRegister(1, 8);
        while (p.Frames == 0) p.Tick(); At(p, 0, 256); p.Tick();
        Assert.Equal(expected, p.VramAddress & 0x7be0);
    }
    [Fact] public void VerticalTransferOccursOnPreRenderNotVisibleLines()
    {
        var p = Create(); p.WriteRegister(5, 0); p.WriteRegister(5, 0x5b); p.WriteRegister(1, 8);
        At(p, 261, 280); p.Tick(); Assert.Equal(p.TemporaryAddress & 0x7be0, p.VramAddress & 0x7be0);
    }
    [Fact] public void PpuDataIncrementsBothAxesDuringRenderingButLinearWhenDisabled()
    {
        var p = Create(); Address(p, 31); p.WriteRegister(1, 8); p.ReadRegister(7); Assert.Equal(0x1400, p.VramAddress);
        p.WriteRegister(1, 0); Address(p, 31); p.ReadRegister(7); Assert.Equal(32, p.VramAddress);
        p.WriteRegister(0, 4); p.ReadRegister(7); Assert.Equal(64, p.VramAddress);
    }
    [Fact] public void PatternPixelsHaveCorrectBitOrderAndTileBoundary()
    {
        var p = Create(); p.WriteMemory(0x2000, 1); p.WriteMemory(16, 128);
        p.WriteMemory(0x3f00, 3); p.WriteMemory(0x3f01, 17); p.WriteRegister(1, 10);
        while (p.Frames == 0) p.Tick(); At(p, 0, 10);
        Assert.Equal(17, p.Framebuffer.Span[0]);
        for (int x = 1; x < 9; x++) Assert.Equal(3, p.Framebuffer.Span[x]);
    }
    [Fact] public void PrefetchedPatternsSurviveChrChangesUntilFollowingTile()
    {
        var p = Create(); for (int i = 0; i < 32; i++) p.WriteMemory(0x2000 + i, 1);
        for (int i = 0; i < 8; i++) p.WriteMemory(16 + i, 255);
        p.WriteMemory(0x3f00, 3); p.WriteMemory(0x3f01, 17); p.WriteRegister(1, 10);
        while (p.Frames == 0) p.Tick();
        for (int i = 0; i < 8; i++) p.WriteMemory(16 + i, 0);
        At(p, 0, 18);
        for (int x = 0; x < 16; x++) Assert.Equal(17, p.Framebuffer.Span[x]);
        Assert.Equal(3, p.Framebuffer.Span[16]);
    }
}
