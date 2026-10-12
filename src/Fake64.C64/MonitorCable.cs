namespace Fake64.C64;

// Il cavo del monitor: collega l'uscita video di una Board a uno schermo.
// Finché il cavo esiste lo schermo riceve un frame ogni 1/50 di secondo; Dispose lo scollega
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
