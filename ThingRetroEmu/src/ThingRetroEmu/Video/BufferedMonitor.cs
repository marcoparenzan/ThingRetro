using System.Runtime.InteropServices;
namespace ThingRetroEmu.Video;

/// <summary>Single-owner buffered RGB monitor for headless/web consumers, connected by the common cable.</summary>
public sealed class BufferedMonitor : IMonitor, IDisposable
{
    byte[] buffer = [];
    GCHandle pinned;
    bool disposed;
    public int Width { get; private set; }
    public int Height { get; private set; }
    public unsafe byte* BeginFrame(VideoMode mode)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (buffer.Length != mode.ByteCount)
        {
            if (pinned.IsAllocated) pinned.Free();
            buffer = new byte[mode.ByteCount]; pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        }
        Width = mode.Width; Height = mode.Height;
        return (byte*)pinned.AddrOfPinnedObject();
    }
    public void EndFrame() { }
    public byte[] Rgba()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var copy = buffer.ToArray();
        for (int i=0;i<copy.Length;i+=4) { (copy[i],copy[i+2])=(copy[i+2],copy[i]); copy[i+3]=255; }
        return copy;
    }
    public void Dispose() { if (disposed) return; disposed=true; if (pinned.IsAllocated) pinned.Free(); buffer=[]; }
}
