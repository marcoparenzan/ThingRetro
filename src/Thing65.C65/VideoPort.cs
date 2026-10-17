using Thing65.Mos;

namespace Thing65.C65;

// Video output of the Board (the monitor socket): at the end of a frame the VIC-II sends the picture
// to the connected monitor, if any. The connection is made with a MonitorCable
public sealed class VideoPort
{
    volatile IScreen? monitor;

    internal VideoPort()
    {
    }

    public bool IsConnected => monitor is not null;

    internal void Plug(IScreen screen)
    {
        if (monitor is not null && !ReferenceEquals(monitor, screen))
            throw new InvalidOperationException("A monitor is already connected to the video port: unplug it before connecting another one");
        monitor = screen;
    }

    internal void Unplug(IScreen screen)
    {
        if (ReferenceEquals(monitor, screen)) monitor = null;
    }

    // Called by the emulation thread at the end of every frame
    internal unsafe void Present(I6569Pinout vicii)
    {
        var screen = monitor;
        if (screen is null) return;
        var pixels = screen.BeginRender();
        try
        {
            vicii.Render(pixels);
        }
        finally
        {
            screen.EndRender();
        }
    }
}
