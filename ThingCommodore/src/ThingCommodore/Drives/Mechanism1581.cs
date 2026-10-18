namespace ThingCommodore.Drives;

// The mechanics of the 1581: a 3.5" drive with a spindle motor at 300 rpm, a stepper that moves the head from cylinder to cylinder
// (80 of them, one head on each side), the sensors the controller reads (track 0, index, write protect, disk in) and the disk, a .d81
// image. The WD1770 asks the steps and the side; the DOS switches the motor on and off through the 8520
public sealed class Mechanism1581 : IFloppyDrive
{
    public const int CYLINDERS = 82;                     // the head can go two cylinders past the last one
    const int INDEX_PERIOD = 400_000;                    // 200 ms at 2 MHz
    const int INDEX_PULSE = 8_000;                       // 4 ms

    D81Image? disk;
    int cylinder;
    int turn;

    // The disk in the drive (null = no disk). Changing it sets the disk change latch, which a step of the head clears
    public D81Image? Disk
    {
        get => disk;
        set
        {
            disk = value;
            DiskChanged = true;
        }
    }

    public bool MotorOn { get; set; }
    public int Side { get; set; }
    public bool DiskChanged { get; private set; } = true;
    public bool Led { get; set; }

    // The drive is ready when a disk is in and the motor spins it (there is no spin-up delay)
    public bool Ready => disk is not null && MotorOn;
    public bool Track0 => cylinder == 0;
    public bool WriteProtected => disk?.WriteProtected == true;
    public bool Index => Ready && turn < INDEX_PULSE;
    public int Cylinder => cylinder;
    IDiskMedia? IFloppyDrive.Media => disk;
    public IDiskMedia? Media => disk;

    public void Step(bool towardsCentre)
    {
        cylinder = Math.Clamp(cylinder + (towardsCentre ? 1 : -1), 0, CYLINDERS - 1);
        DiskChanged = false;
    }

    // One cycle of the 2 MHz clock: the disk turns
    public void Tick()
    {
        if (Ready && ++turn >= INDEX_PERIOD) turn = 0;
    }

    public void Reset()
    {
        cylinder = 0;
        MotorOn = false;
        Side = 0;
        turn = 0;
    }

    // The sector level: a .d81 image keeps what was written, nothing is pending
    public void Flush() { }
}
