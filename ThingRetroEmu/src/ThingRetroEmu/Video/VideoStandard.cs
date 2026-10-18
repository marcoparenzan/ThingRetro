namespace ThingRetroEmu.Video;

/// <summary>
/// A television standard: how a composite video signal is laid out in time. The line is a sync pulse, a back porch with the colour burst, the
/// picture and a front porch; a field starts with a few lines of vertical sync. Signal levels are in volts (<see cref="CompositeLevels"/>), and the signal is
/// sampled at four times the colour subcarrier, which makes the colour demodulation exact with four samples per cycle
/// </summary>
/// <param name="Name">PAL or NTSC.</param>
/// <param name="IsPal">PAL swaps the sign of the V colour component at every line; NTSC does not.</param>
/// <param name="SubcarrierHz">The colour subcarrier: 4,433,618.75 Hz for PAL, 3,579,545.45 Hz for NTSC.</param>
/// <param name="LineMicroseconds">The duration of a line: 64 µs (PAL), 63.556 µs (NTSC).</param>
/// <param name="LinesPerField">312 lines (PAL, 50 fields a second) or 262 (NTSC, 59.9 fields a second): no interlace.</param>
/// <param name="VerticalSyncLines">The lines at the start of a field that carry the vertical sync.</param>
/// <param name="FirstActiveLine">The first line of the picture, counted from the first line of the vertical sync.</param>
/// <param name="ActiveLines">The lines of the picture.</param>
/// <param name="SyncMicroseconds">The horizontal sync pulse.</param>
/// <param name="BurstStartMicroseconds">Where the colour burst starts, from the start of the sync pulse.</param>
/// <param name="BurstCycles">The cycles of the burst.</param>
/// <param name="ActiveStartMicroseconds">Where the picture starts on a line, from the start of the sync pulse.</param>
/// <param name="ActiveMicroseconds">The duration of the picture on a line.</param>
public sealed record VideoStandard(
    string Name, bool IsPal, double SubcarrierHz, double LineMicroseconds, int LinesPerField, int VerticalSyncLines, int FirstActiveLine, int ActiveLines,
    double SyncMicroseconds, double BurstStartMicroseconds, int BurstCycles, double ActiveStartMicroseconds, double ActiveMicroseconds)
{
    public static VideoStandard Pal { get; } = new("PAL", true, 4_433_618.75, 64.0, 312, 3, 22, 288, 4.7, 5.6, 10, 10.5, 51.8);

    public static VideoStandard Ntsc { get; } = new("NTSC", false, 3_579_545.4545, 63.5556, 262, 3, 20, 240, 4.7, 5.3, 9, 9.4, 52.6);

    /// <summary>Samples a second: four per cycle of the colour subcarrier.</summary>
    public double SampleRate => 4 * SubcarrierHz;

    public int SamplesPerLine => (int)Math.Round(LineMicroseconds * 1e-6 * SampleRate);

    public double FieldHz => 1_000_000.0 / (LineMicroseconds * LinesPerField);

    /// <summary>A duration in samples.</summary>
    public int Samples(double microseconds) => (int)Math.Round(microseconds * 1e-6 * SampleRate);

    public int SyncSamples => Samples(SyncMicroseconds);
    public int BurstStart => Samples(BurstStartMicroseconds);
    public int BurstLength => BurstCycles * 4;
    public int ActiveStart => Samples(ActiveStartMicroseconds);
    public int ActiveSamples => Samples(ActiveMicroseconds);

    /// <summary>The standard that fits a picture refreshed at this rate: NTSC from 55 frames a second up, PAL below.</summary>
    public static VideoStandard For(double framesPerSecond) => framesPerSecond >= 55 ? Ntsc : Pal;
}

/// <summary>The levels of a composite signal, in volts: the sync tip is below the black.</summary>
public static class CompositeLevels
{
    public const float Sync = -0.3f;
    public const float Blank = 0f;
    public const float White = 0.7f;

    /// <summary>Peak amplitude of the colour burst.</summary>
    public const float BurstPeak = 0.15f;

    /// <summary>Weights of the colour difference signals: U = 0.493 (B - Y), V = 0.877 (R - Y).</summary>
    public const float UWeight = 0.493f;
    public const float VWeight = 0.877f;
}
