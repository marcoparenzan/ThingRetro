using ThingNES;

namespace ThingNES.Tests;

public class FrontendTests
{
    static NesMachine Machine()
    {
        var image = new byte[16400]; "NES\u001a"u8.CopyTo(image); image[4] = 1;
        return new NesMachine(Cartridge.Load(image));
    }
    [Fact] public void RunFrameStopsAtNextBoundaryEvenFromMidFrame()
    {
        var m = Machine(); m.RunCycles(100);
        long start = m.CpuCycles; int elapsed = m.RunFrame();
        Assert.Equal(1, m.Ppu.Frames); Assert.Equal(elapsed, m.CpuCycles - start);
        Assert.InRange(m.Ppu.Dot, 0, 2); Assert.Equal(0, m.Ppu.Scanline); Assert.InRange(elapsed, 29679, 29682);
        m.RunFrame(); Assert.Equal(2, m.Ppu.Frames);
    }
    [Fact] public void ArgbConversionUsesSharedPaletteAndOpaqueAlpha()
    {
        var pixels = new byte[256 * 240]; pixels[1] = 0xff; var argb = new int[pixels.Length];
        FrameImage.CopyArgb(pixels, argb);
        Assert.Equal(unchecked((int)0xff666666), argb[0]); Assert.Equal(unchecked((int)0xff000000), argb[1]);
        Assert.Throws<ArgumentException>(() => FrameImage.CopyArgb(new byte[1], new int[1]));
        Assert.Throws<ArgumentException>(() => FrameImage.CopyArgb(pixels, new int[1]));
    }
}
