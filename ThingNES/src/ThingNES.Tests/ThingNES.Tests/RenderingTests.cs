using ThingNES;

namespace ThingNES.Tests;

public class RenderingTests
{
    static Ppu Create(byte mirroring = 0)
    {
        var rom = new byte[16 + 16384]; "NES\u001a"u8.CopyTo(rom); rom[4] = 1; rom[6] = mirroring;
        var p = new Ppu(Cartridge.Load(rom)); Array.Fill(p.Oam, (byte)255);
        p.WriteMemory(0x3f00, 3); p.WriteMemory(0x3f01, 17); p.WriteMemory(0x3f05, 22); p.WriteMemory(0x3f11, 33);
        for (int row = 0; row < 8; row++) p.WriteMemory(16 + row, 255); // tile 1: solid color 1
        return p;
    }
    static void PixelThrough(Ppu p, int x, int y)
    {
        // Allow a pre-render line to transfer t into v and prime the two-tile pipeline.
        while (p.Frames == 0) p.Tick();
        int target = y * 341 + x + 2;
        for (int i = 0; i < target; i++) p.Tick();
    }
    static void Sprite(Ppu p, int i, byte x, byte y, byte tile = 1, byte attr = 0)
    {
        p.Oam[i * 4] = y; p.Oam[i * 4 + 1] = tile; p.Oam[i * 4 + 2] = attr; p.Oam[i * 4 + 3] = x;
    }
    [Fact] public void BackgroundUsesTileAttributeAndLeftClip()
    {
        var p = Create(); p.WriteMemory(0x2000, 1); p.WriteMemory(0x23c0, 1); p.WriteRegister(1, 8);
        PixelThrough(p, 7, 0); Assert.Equal(3, p.Framebuffer.Span[0]);
        var visible = Create(); visible.WriteMemory(0x2000, 1); visible.WriteMemory(0x23c0, 1); visible.WriteRegister(1, 10);
        PixelThrough(visible, 7, 0); Assert.Equal(22, visible.Framebuffer.Span[0]);
    }
    [Fact] public void ScrollCrossesNametable()
    {
        var p = Create(1); p.WriteMemory(0x2400, 1);
        p.WriteRegister(5, 255); p.WriteRegister(5, 0); p.WriteRegister(1, 10);
        PixelThrough(p, 1, 0);
        Assert.Equal(3, p.Framebuffer.Span[0]); Assert.Equal(17, p.Framebuffer.Span[1]);
    }
    [Theory] [InlineData(0, 33)] [InlineData(32, 17)]
    public void SpritePriorityAndZeroHit(byte attr, byte expected)
    {
        var p = Create(); p.WriteMemory(0x2021, 1); Sprite(p, 0, 8, 7, attr: attr); p.WriteRegister(1, 30);
        PixelThrough(p, 8, 8);
        Assert.Equal(expected, p.Framebuffer.Span[8 * 256 + 8]); Assert.Equal(64, p.ReadRegister(2) & 64);
    }
    [Fact] public void SpriteFlipSelectsOppositePixel()
    {
        var p = Create(); for (int row = 0; row < 8; row++) p.WriteMemory(16 + row, 0);
        p.WriteMemory(16 + 7, 1); Sprite(p, 0, 8, 0, attr: 0xc0); p.WriteRegister(1, 20);
        PixelThrough(p, 8, 1); Assert.Equal(33, p.Framebuffer.Span[256 + 8]);
    }
    [Fact] public void EightBySixteenUsesTileBankAndBottomHalf()
    {
        var p = Create(); p.WriteRegister(0, 32);
        p.WriteMemory(0x1030, 128); Sprite(p, 0, 8, 0, tile: 3); p.WriteRegister(1, 20);
        PixelThrough(p, 8, 9); Assert.Equal(33, p.Framebuffer.Span[9 * 256 + 8]);
    }
    [Fact] public void NinthSpriteIsDroppedAndOverflowSet()
    {
        var p = Create(); for (int i = 0; i < 8; i++) Sprite(p, i, 40, 0, tile: 0);
        Sprite(p, 8, 8, 0); p.WriteRegister(1, 20);
        PixelThrough(p, 8, 1); Assert.Equal(3, p.Framebuffer.Span[256 + 8]); Assert.Equal(32, p.ReadRegister(2) & 32);
    }
    [Fact] public void GrayscaleMasksPalette()
    {
        var p = Create(); p.WriteRegister(1, 1); PixelThrough(p, 0, 0); Assert.Equal(0, p.Framebuffer.Span[0]);
    }
    [Theory] [InlineData(0, 89342)] [InlineData(8, 89341)]
    public void OddFrameSkipsDotOnlyWhenRendering(byte mask, int expected)
    {
        var p = Create(); p.WriteRegister(1, mask);
        while (p.Frames == 0) p.Tick();
        int ticks = 0; while (p.Frames == 1) { p.Tick(); ticks++; }
        Assert.Equal(expected, ticks);
    }
    [Fact] public void PpmHasCorrectHeaderAndRgbPayload()
    {
        using var stream = new MemoryStream(); FrameImage.WritePpm(stream, new byte[256 * 240]);
        var bytes = stream.ToArray(); var header = "P6\n256 240\n255\n"u8;
        Assert.True(bytes.AsSpan(0, header.Length).SequenceEqual(header));
        Assert.Equal(header.Length + 256 * 240 * 3, bytes.Length); Assert.Equal(0x66, bytes[header.Length]);
        Assert.Throws<ArgumentException>(() => FrameImage.WritePpm(stream, new byte[1]));
    }
}
