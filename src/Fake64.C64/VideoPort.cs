using Fake64.Mos;

namespace Fake64.C64;

// Uscita video della Board (la presa del monitor): a fine frame il VIC-II invia l'immagine
// al monitor collegato, se c'è. Il collegamento si fa con un MonitorCable
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
            throw new InvalidOperationException("Alla porta video è già collegato un monitor: scollegarlo prima di collegarne un altro");
        monitor = screen;
    }

    internal void Unplug(IScreen screen)
    {
        if (ReferenceEquals(monitor, screen)) monitor = null;
    }

    // Chiamato dal thread di emulazione alla fine di ogni frame
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
