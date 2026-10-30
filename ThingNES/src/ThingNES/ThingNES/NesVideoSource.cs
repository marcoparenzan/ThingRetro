using ThingRetroEmu.Video;
namespace ThingNES;

/// <summary>Adapter to the same monitor/cable contracts used by Thing64. Composite remains RGB-encoded.</summary>
public sealed class NesVideoSource(Ppu ppu) : IVideoSource
{
    readonly int[] colors = new int[256 * 240];
    public VideoMode Mode => new(256, 240, 1789773.0 / 29780.5, 8.0 / 7);
    public unsafe void Render(byte* pixels)
    {
        FrameImage.CopyArgb(ppu.Framebuffer.Span, colors);
        colors.AsSpan().CopyTo(new Span<int>(pixels, colors.Length));
    }
}
