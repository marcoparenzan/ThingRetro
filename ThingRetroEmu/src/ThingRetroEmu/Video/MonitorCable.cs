namespace ThingRetroEmu.Video;

// The monitor cable: connects the video output of a machine to a monitor, by RGB or by composite.
// While the cable exists the monitor receives a picture at the end of every frame of the source; Dispose unplugs it
public sealed class MonitorCable : IDisposable
{
    readonly VideoPort port;
    readonly IMonitor? monitor;
    readonly ICompositeMonitor? compositeMonitor;

    public MonitorCable(VideoPort port, IMonitor monitor)
    {
        this.port = port;
        this.monitor = monitor;
        port.Plug(monitor);
    }

    /// <summary>The composite cable: the picture goes out as a signal and the monitor decodes it.</summary>
    public MonitorCable(VideoPort port, ICompositeMonitor monitor)
    {
        this.port = port;
        compositeMonitor = monitor;
        port.Plug(monitor);
    }

    public IMonitor? Monitor => monitor;
    public ICompositeMonitor? CompositeMonitor => compositeMonitor;

    public void Dispose()
    {
        if (monitor is not null) port.Unplug(monitor);
        if (compositeMonitor is not null) port.Unplug(compositeMonitor);
    }
}
