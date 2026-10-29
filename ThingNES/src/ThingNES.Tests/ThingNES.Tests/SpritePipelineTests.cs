using ThingNES;

namespace ThingNES.Tests;

public class SpritePipelineTests
{
    static Ppu Create()
    {
        var rom = new byte[16400]; "NES\u001a"u8.CopyTo(rom); rom[4] = 1;
        var p = new Ppu(Cartridge.Load(rom)); Array.Fill(p.Oam, (byte)255);
        p.Oam[0] = 0; p.Oam[1] = 1; p.Oam[2] = 0; p.Oam[3] = 8;
        for (int y = 0; y < 8; y++) p.WriteMemory(16 + y, 255);
        p.WriteMemory(0x3f00, 3); p.WriteMemory(0x3f11, 33); p.WriteRegister(1, 20);
        return p;
    }
    static void At(Ppu p, int line, int dot) { while (p.Scanline != line || p.Dot != dot) p.Tick(); }
    [Fact] public void SecondaryOamClearsOnEvenDotsAndCopiesOneBytePerTwoCycles()
    {
        var p = Create(); p.Tick(); p.Tick(); p.Tick(); Assert.Equal(255, p.SecondaryOam.Span[0]);
        At(p, 0, 65); p.Tick(); Assert.Equal(0, p.SelectedSpriteCount);
        p.Tick(); Assert.Equal(0, p.SecondaryOam.Span[0]);
        At(p, 0, 73); Assert.Equal(1, p.SelectedSpriteCount);
        Assert.Equal(new byte[] {0,1,0,8}, p.SecondaryOam.Span[..4].ToArray());
    }
    [Fact] public void FetchesFeedFollowingLineNotCurrentLine()
    {
        var p = Create(); At(p, 0, 320); Assert.Equal(0, p.ActiveSpriteCount);
        At(p, 1, 10); Assert.Equal(1, p.ActiveSpriteCount);
        Assert.Equal(3, p.Framebuffer.Span[8]); Assert.Equal(33, p.Framebuffer.Span[256 + 8]);
    }
    [Fact] public void ChrMutationAfterFetchDoesNotChangeLatchedPattern()
    {
        var p = Create(); At(p, 0, 264);
        for (int y = 0; y < 8; y++) p.WriteMemory(16 + y, 0);
        At(p, 1, 10); Assert.Equal(33, p.Framebuffer.Span[256 + 8]);
        At(p, 2, 10); Assert.Equal(3, p.Framebuffer.Span[512 + 8]);
    }
    [Fact] public void OamMutationAfterEvaluationDoesNotChangeLatchedPosition()
    {
        var p = Create(); At(p, 0, 257); p.Oam[3] = 40;
        At(p, 1, 42); Assert.Equal(33, p.Framebuffer.Span[256 + 8]); Assert.Equal(3, p.Framebuffer.Span[256 + 40]);
        At(p, 2, 42); Assert.Equal(33, p.Framebuffer.Span[512 + 40]);
    }
    [Fact] public void LowAndHighPatternBytesAreFetchedSeparately()
    {
        var p = Create(); p.WriteMemory(0x3f13, 44); At(p, 0, 262);
        p.WriteMemory(16, 0); p.WriteMemory(24, 255);
        At(p, 1, 10); Assert.Equal(44, p.Framebuffer.Span[256 + 8]);
    }
    [Fact] public void SelectionPreservesPrimaryOamPriority()
    {
        var p = Create(); p.Oam[4] = 0; p.Oam[5] = 1; p.Oam[6] = 1; p.Oam[7] = 8;
        p.WriteMemory(0x3f15, 55); At(p, 1, 10);
        Assert.Equal(2, p.ActiveSpriteCount); Assert.Equal(33, p.Framebuffer.Span[256 + 8]);
    }
    [Fact] public void PreRenderDoesNotWrapSprite255OntoLineZero()
    {
        var p = Create(); p.Oam[0] = 255;
        while (p.Frames == 0) p.Tick(); At(p, 0, 10);
        Assert.Equal(0, p.ActiveSpriteCount); Assert.Equal(3, p.Framebuffer.Span[8]);
    }
}
