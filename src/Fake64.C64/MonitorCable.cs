namespace Fake64.C64;

// The monitor cable: connects the video output of a Board to a screen.
// While the cable exists the screen receives a frame every 1/50 of a second; Dispose unplugs it
public sealed class MonitorCable : IDisposable
{
    readonly VideoPort port;

    public MonitorCable(VideoPort port, IScreen screen)
    {
        this.port = port;
        Screen = screen;
        port.Plug(screen);
    }

    public IScreen Screen { get; }

    public void Dispose() => port.Unplug(Screen);
}
