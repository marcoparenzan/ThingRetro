namespace ThingRetroEmu.Video;

/// <summary>
/// One field of a composite video signal: the voltage of the wire, sample after sample, line after line, sync and burst and blanking included.
/// This is what comes out of the video connector of a machine, and what a monitor receives
/// </summary>
public sealed class CompositeFrame
{
    public CompositeFrame(VideoStandard standard)
    {
        Standard = standard;
        Samples = new float[standard.SamplesPerLine * standard.LinesPerField];
    }

    public VideoStandard Standard { get; }
    public float[] Samples { get; }
    public int SamplesPerLine => Standard.SamplesPerLine;
    public int Lines => Standard.LinesPerField;

    public Span<float> Line(int line) => Samples.AsSpan(line * SamplesPerLine, SamplesPerLine);
}

/// <summary>A machine that makes a composite signal itself (a TIA, a VIC-II with its encoder): it fills the lines of a field.</summary>
public interface ICompositeSource
{
    VideoStandard Standard { get; }

    /// <summary>Writes the whole field: sync, burst, blanking and picture. <see cref="CompositeEncoder.Structure"/> does the part that is the same for every machine.</summary>
    void RenderComposite(CompositeFrame frame);
}

/// <summary>A monitor with a composite input: it receives the signal and decodes it. Called by the emulation thread at the end of every field.</summary>
public interface ICompositeMonitor
{
    void ShowComposite(CompositeFrame frame);
}
