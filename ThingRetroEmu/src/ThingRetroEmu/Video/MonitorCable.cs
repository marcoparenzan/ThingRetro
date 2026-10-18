namespace ThingRetroEmu.Video;

// The monitor cable: connects the video output of a Board to a monitor.
// While the cable exists the monitor receives a frame at the end of every frame of the source; Dispose unplugs it
public sealed class MonitorCable : IDisposable
{
    readonly VideoPort port;

    public MonitorCable(VideoPort port, IMonitor monitor)
    {
        this.port = port;
        Monitor = monitor;
        port.Plug(monitor);
    }

    public IMonitor Monitor { get; }

    public void Dispose() => port.Unplug(Monitor);
}
