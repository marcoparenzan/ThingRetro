namespace ThingCommodore.Drives;

// The drives run at 1 MHz and the computer at 985,248 Hz (PAL): this turns the cycles of the computer into cycles of the
// drive, one more every 64 or so. Call OnComputerCycle at every cycle of the computer
public sealed class DriveClock
{
    public const long DRIVE_HZ = 1_000_000;

    readonly Action driveCycle;
    readonly long computerHz;
    readonly long driveHz;
    long credit;

    // driveHz: the clock of the drive; 1 MHz for the 1541, 2 MHz for the 1581
    public DriveClock(long computerHz, Action driveCycle, long driveHz = DRIVE_HZ)
    {
        this.computerHz = computerHz;
        this.driveCycle = driveCycle;
        this.driveHz = driveHz;
    }

    public void OnComputerCycle()
    {
        credit += driveHz;
        while (credit >= computerHz)
        {
            credit -= computerHz;
            driveCycle();
        }
    }
}
