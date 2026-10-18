namespace ThingRetroEmu.Video;

/// <summary>
/// Makes the composite signal: the structure of the lines (sync, burst, blanking) that is the same for every machine, and the modulation of a picture
/// into the signal. Colour is modulated on a subcarrier sampled four times a cycle, so the carrier is a sequence of the patterns cos = 1 0 -1 0 and sin = 0 1 0 -1;
/// the phase is counted from the start of the line. PAL adds the V component with the sign swapped on every other line
/// </summary>
public static class CompositeEncoder
{
    static readonly float[] Cos = [1, 0, -1, 0];
    static readonly float[] Sin = [0, 1, 0, -1];

    /// <summary>The lowest voltage a picture may reach: a saturated blue dips below the blanking level, but must stay clear of the sync tip (-0.3 V).</summary>
    public const float PictureFloor = -0.25f;

    /// <summary>The sign of V on a line: +1 on all NTSC lines, alternating on PAL ones.</summary>
    public static int VSign(VideoStandard standard, int line) => standard.IsPal && (line & 1) == 1 ? -1 : 1;

    /// <summary>
    /// Fills a field with its structure and a black picture: horizontal sync on every line, the long sync pulses of the vertical sync at the start of the field,
    /// the colour burst on the lines after it. The burst is minus U (and, in PAL, plus or minus V as the line goes): the reference a monitor locks its colours to
    /// </summary>
    public static void Structure(CompositeFrame frame)
    {
        var s = frame.Standard;
        int samplesPerLine = s.SamplesPerLine, sync = s.SyncSamples;
        float peak = CompositeLevels.BurstPeak;
        float a = s.IsPal ? peak / MathF.Sqrt(2) : peak;
        for (int line = 0; line < s.LinesPerField; line++)
        {
            var span = frame.Line(line);
            span.Clear();
            if (line < s.VerticalSyncLines)
            {
                span[..(samplesPerLine - sync)].Fill(CompositeLevels.Sync);     // broad pulse: low almost the whole line
                continue;
            }
            span[..sync].Fill(CompositeLevels.Sync);
            if (line < s.VerticalSyncLines + 3) continue;
            int sign = VSign(s, line);
            float u = -a;
            float v = s.IsPal ? sign * a : 0;
            for (int i = s.BurstStart; i < s.BurstStart + s.BurstLength; i++)
                span[i] = u * Sin[i & 3] + v * Cos[i & 3];
        }
    }

    /// <summary>
    /// Modulates a line of the picture: <paramref name="row"/> is 32-bit BGRA pixels, stretched over the picture part of the line. The luminance is
    /// 0.7 V for white, the chroma is U and V on the subcarrier
    /// </summary>
    public static void EncodeLine(CompositeFrame frame, int line, ReadOnlySpan<uint> row)
    {
        var s = frame.Standard;
        var span = frame.Line(line);
        int width = row.Length;
        if (width == 0) return;
        int sign = VSign(s, line);

        // Y, U and V of each pixel, in volts
        Span<float> y = width <= 2048 ? stackalloc float[width] : new float[width];
        Span<float> u = width <= 2048 ? stackalloc float[width] : new float[width];
        Span<float> v = width <= 2048 ? stackalloc float[width] : new float[width];
        for (int x = 0; x < width; x++)
        {
            uint p = row[x];
            float r = (p >> 16 & 0xFF) / 255f, g = (p >> 8 & 0xFF) / 255f, b = (p & 0xFF) / 255f;
            float luma = 0.299f * r + 0.587f * g + 0.114f * b;
            y[x] = CompositeLevels.White * luma;
            u[x] = CompositeLevels.White * CompositeLevels.UWeight * (b - luma);
            v[x] = CompositeLevels.White * CompositeLevels.VWeight * (r - luma);
        }

        int start = s.ActiveStart, length = s.ActiveSamples;
        for (int i = 0; i < length; i++)
        {
            int x = Math.Min(width - 1, (int)((long)i * width / length));
            int index = start + i;
            float sample = y[x] + u[x] * Sin[index & 3] + sign * v[x] * Cos[index & 3];
            span[index] = Math.Max(sample, PictureFloor);
        }
    }
}

/// <summary>
/// The video encoder of a machine whose chip draws a picture of dots: the picture it renders (<see cref="IVideoSource"/>) becomes a composite signal. It stands for
/// the encoder on the real board; a chip that makes its own signal implements <see cref="ICompositeSource"/> instead
/// </summary>
public sealed class RgbToCompositeSource(IVideoSource source, VideoStandard standard) : ICompositeSource
{
    uint[] picture = [];

    public VideoStandard Standard => standard;

    public unsafe void RenderComposite(CompositeFrame frame)
    {
        var mode = source.Mode;
        if (picture.Length != mode.PixelCount) picture = new uint[mode.PixelCount];
        fixed (uint* pixels = picture) source.Render((byte*)pixels);

        CompositeEncoder.Structure(frame);
        int activeLines = standard.ActiveLines;
        int shift = (mode.Height - activeLines) / 2;                 // a taller picture is cropped, a shorter one is centered
        for (int k = 0; k < activeLines; k++)
        {
            int sourceRow = k + shift;
            if (sourceRow < 0 || sourceRow >= mode.Height) continue;
            CompositeEncoder.EncodeLine(frame, standard.FirstActiveLine + k, picture.AsSpan(sourceRow * mode.Width, mode.Width));
        }
    }
}
