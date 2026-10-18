namespace ThingRetroEmu.Video;

/// <summary>
/// The video output of a machine (the monitor socket): at the end of a frame the video source sends its picture to the connected
/// monitor, if there is one. The connection is made with a <see cref="MonitorCable"/>
/// </summary>
public sealed class VideoPort
{
    volatile IMonitor? monitor;

    public bool IsConnected => monitor is not null;

    public void Plug(IMonitor screen)
    {
        if (monitor is not null && !ReferenceEquals(monitor, screen))
            throw new InvalidOperationException("A monitor is already connected to the video port: unplug it before connecting another one");
        monitor = screen;
    }

    public void Unplug(IMonitor screen)
    {
        if (ReferenceEquals(monitor, screen)) monitor = null;
    }

    /// <summary>Called by the emulation thread at the end of every frame.</summary>
    public unsafe void Present(IVideoSource source)
    {
        var screen = monitor;
        if (screen is null) return;
        var pixels = screen.BeginFrame(source.Mode);
        try
        {
            source.Render(pixels);
        }
        finally
        {
            screen.EndFrame();
        }
    }
}
