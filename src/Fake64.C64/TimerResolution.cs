using System.Runtime.InteropServices;

namespace Fake64.C64;

// Su Windows Thread.Sleep(1) dura fino a ~15,6 ms con la risoluzione predefinita del timer di sistema:
// timeBeginPeriod(1) la porta a 1 ms finché l'emulazione è attiva, così i frame non arrivano a raffiche
sealed class TimerResolution : IDisposable
{
    readonly uint milliseconds;
    readonly bool active;

    public TimerResolution(uint milliseconds)
    {
        this.milliseconds = milliseconds;
        active = OperatingSystem.IsWindows() && timeBeginPeriod(milliseconds) == 0;
    }

    public void Dispose()
    {
        if (active) timeEndPeriod(milliseconds);
    }

    [DllImport("winmm.dll")]
    static extern uint timeBeginPeriod(uint uPeriod);

    [DllImport("winmm.dll")]
    static extern uint timeEndPeriod(uint uPeriod);
}
