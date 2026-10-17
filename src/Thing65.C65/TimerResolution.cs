using System.Runtime.InteropServices;

namespace Thing65.C65;

// On Windows Thread.Sleep(1) lasts up to ~15.6 ms with the default resolution of the system timer:
// timeBeginPeriod(1) brings it to 1 ms while the emulation runs, so the frames do not arrive in bursts
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
