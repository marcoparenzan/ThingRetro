namespace ThingNES;

// Wall-clock scheduler with bounded catch-up; drops excess debt rather than monopolizing the UI.
public sealed class FramePacer
{
    public const double FramesPerSecond = Apu.CpuFrequency * 6.0 / 178683; // alternating 89342/89341-dot frames
    double credit;
    public void Reset() => credit = 0;
    public int Advance(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        credit += Math.Min(elapsed.TotalSeconds, 0.25) * FramesPerSecond;
        int due = (int)Math.Floor(credit);
        credit -= due;
        return Math.Min(due, 3);
    }
}
