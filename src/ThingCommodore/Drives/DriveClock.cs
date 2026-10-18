namespace ThingCommodore.Drives;

// The drives run at 1 MHz and the computer at 985,248 Hz (PAL): this turns the cycles of the computer into cycles of the
// drive, one more every 64 or so. Call OnComputerCycle at every cycle of the computer
public sealed class DriveClock
{
    public const long DRIVE_HZ = 1_000_000;

    readonly Action driveCycle;
    readonly long computerHz;
    long credit;

    public DriveClock(long computerHz, Action driveCycle)
    {
        this.computerHz = computerHz;
        this.driveCycle = driveCycle;
    }

    public void OnComputerCycle()
    {
        credit += DRIVE_HZ;
        while (credit >= computerHz)
        {
            credit -= computerHz;
            driveCycle();
        }
    }
}
