using System.Runtime.InteropServices;
using ThingCommodore.Graphics;
using ThingRetroEmu.Video;

namespace ThingOS64.Tests;

// A monitor that keeps the last frame, to look at the screen as the VIC-II draws it (sprites included)
sealed unsafe class CaptureMonitor : IMonitor, IDisposable
{
    byte* buffer;
    int capacity;

    public RgbImage? Frame { get; private set; }
    VideoMode mode;

    public byte* BeginFrame(VideoMode frameMode)
    {
        if (frameMode.ByteCount > capacity)
        {
            if (buffer is not null) NativeMemory.Free(buffer);
            buffer = (byte*)NativeMemory.Alloc((nuint)frameMode.ByteCount);
            capacity = frameMode.ByteCount;
        }
        mode = frameMode;
        return buffer;
    }

    public void EndFrame()
    {
        var image = new RgbImage(mode.Width, mode.Height);
        var pixels = new ReadOnlySpan<uint>(buffer, mode.PixelCount);    // BGRA in memory = ARGB as a little-endian uint
        pixels.CopyTo(image.Pixels);
        Frame = image;
    }

    public void Dispose()
    {
        if (buffer is not null) NativeMemory.Free(buffer);
        buffer = null;
    }
}
