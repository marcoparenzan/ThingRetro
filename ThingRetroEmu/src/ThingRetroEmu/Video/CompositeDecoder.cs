namespace ThingRetroEmu.Video;

/// <summary>
/// What a monitor does with the signal. It finds the horizontal sync of every line and the vertical sync of the field, measures the phase of the colour burst
/// to lock the colour on it (and, in PAL, to know which way V goes on that line), separates the luminance from the chroma and brings the picture to RGB.
/// The separation is the simplest one: a box filter over one cycle of the subcarrier takes the colour out of the luminance, and the demodulation is a product with the
/// carrier, which at four samples a cycle needs no multiplication tables
/// </summary>
public sealed class CompositeDecoder
{
    const float SyncThreshold = -0.15f;
    static readonly float[] Cos = [1, 0, -1, 0];
    static readonly float[] Sin = [0, 1, 0, -1];

    // The five taps (-2..+2) of the filters for each phase of the subcarrier at the pixel: the box filter over one cycle for the luminance, and the same weights
    // times the carrier for the two components of the chroma (the 2 / 4 of the demodulation included)
    static readonly float[] CosTaps = new float[20], SinTaps = new float[20];

    static CompositeDecoder()
    {
        float[] weight = [0.5f, 1f, 1f, 1f, 0.5f];
        for (int m = 0; m < 4; m++)
            for (int k = 0; k < 5; k++)
            {
                int phase = (m + k - 2 + 4) & 3;
                CosTaps[m * 5 + k] = 0.5f * weight[k] * Cos[phase];
                SinTaps[m * 5 + k] = 0.5f * weight[k] * Sin[phase];
            }
    }

    readonly VideoStandard standard;
    readonly int width;
    readonly int[] centers;

    /// <param name="width">Dots across the decoded picture; by default one for every two samples of the picture part of the line.</param>
    public CompositeDecoder(VideoStandard standard, int width = 0)
    {
        this.standard = standard;
        this.width = width > 0 ? width : standard.ActiveSamples / 2;
        centers = new int[this.width];
        for (int x = 0; x < this.width; x++) centers[x] = standard.ActiveStart + (int)((x + 0.5) * standard.ActiveSamples / this.width);
    }

    /// <summary>The picture it makes: the active lines, at the shape of a television (4:3).</summary>
    public VideoMode Mode => new(width, standard.ActiveLines, standard.FieldHz, 4.0 / 3 * standard.ActiveLines / width);

    public unsafe void Decode(CompositeFrame frame, byte* pixels)
    {
        uint* output = (uint*)pixels;
        for (int i = 0; i < width * standard.ActiveLines; i++) output[i] = 0xFF000000;

        var data = frame.Samples;
        int samplesPerLine = frame.SamplesPerLine, lines = frame.Lines;
        int fieldStart = FindFieldStart(data, samplesPerLine, lines);
        int previousSign = 0;

        for (int k = 0; k < standard.ActiveLines; k++)
        {
            int line = fieldStart + standard.FirstActiveLine + k;
            if (line >= lines) break;
            int sync = FindSync(data, line * samplesPerLine, samplesPerLine);
            if (sync < 0) { previousSign = 0; continue; }

            // The colour burst: its phase is the reference of this line
            int burstStart = sync + standard.BurstStart + 4, burstLength = standard.BurstLength - 8;
            if (burstStart + burstLength + 2 >= data.Length) break;
            float ic = 0, qs = 0;
            for (int i = burstStart; i < burstStart + burstLength; i++)
            {
                int m = (i - sync) & 3;
                ic += data[i] * Cos[m];
                qs += data[i] * Sin[m];
            }
            float scale = 2f / burstLength;
            ic *= scale;
            qs *= scale;
            float burstMagnitude = MathF.Sqrt(ic * ic + qs * qs);
            bool color = burstMagnitude > 0.02f;
            float burstAngle = MathF.Atan2(-qs, ic);                         // the phasor is ic - j qs
            int sign = 1;
            float rotationCos = 1, rotationSin = 0, gain = 0;
            if (color)
            {
                float nominal;
                if (standard.IsPal)
                {
                    // +V lines have the burst at 45 degrees, -V lines at 135
                    float errorPlus = Wrap(burstAngle - MathF.PI / 4), errorMinus = Wrap(burstAngle - 3 * MathF.PI / 4);
                    int expected = previousSign == 0 ? 0 : -previousSign;
                    if (expected == 0) sign = Math.Abs(errorPlus) <= Math.Abs(errorMinus) ? 1 : -1;
                    else
                    {
                        sign = expected;
                        float expectedError = expected == 1 ? errorPlus : errorMinus, otherError = expected == 1 ? errorMinus : errorPlus;
                        if (Math.Abs(expectedError) > MathF.PI / 3 && Math.Abs(otherError) < MathF.PI / 6) sign = -expected;
                    }
                    nominal = sign == 1 ? MathF.PI / 4 : 3 * MathF.PI / 4;
                }
                else nominal = MathF.PI / 2;                                   // minus U, seen from the (V, -U) axes
                float theta = Wrap(burstAngle - nominal);
                rotationCos = MathF.Cos(theta);
                rotationSin = -MathF.Sin(theta);                               // e^{-j theta}
                gain = CompositeLevels.BurstPeak / burstMagnitude;
            }
            previousSign = sign;

            // The chroma phasor (cx, cy) is brought to R-Y and B-Y by the rotation that locks it on the burst and the gain of the burst: four constants
            float vScale = sign * gain / (CompositeLevels.White * CompositeLevels.VWeight), uScale = gain / (CompositeLevels.White * CompositeLevels.UWeight);
            float a1 = vScale * rotationCos, a2 = vScale * rotationSin, b1 = -uScale * rotationSin, b2 = uScale * rotationCos;
            float invWhite = 1f / CompositeLevels.White;
            fixed (float* origin = data)
            fixed (float* cosTaps = CosTaps, sinTaps = SinTaps)
            {
                uint* row = output + k * width;
                for (int x = 0; x < width; x++)
                {
                    int center = sync + centers[x];
                    if (center - 2 < 0 || center + 2 >= data.Length) continue;
                    float* d = origin + center;
                    float yn = (0.125f * d[-2] + 0.25f * d[-1] + 0.25f * d[0] + 0.25f * d[1] + 0.125f * d[2]) * invWhite;
                    float rMinusY = 0, bMinusY = 0;
                    if (color)
                    {
                        int m = (center - sync) & 3;
                        float* ct = cosTaps + m * 5, st = sinTaps + m * 5;
                        float cx = ct[0] * d[-2] + ct[1] * d[-1] + ct[2] * d[0] + ct[3] * d[1] + ct[4] * d[2];
                        float cy = st[0] * d[-2] + st[1] * d[-1] + st[2] * d[0] + st[3] * d[1] + st[4] * d[2];
                        rMinusY = a1 * cx + a2 * cy;
                        bMinusY = b1 * cx + b2 * cy;
                    }
                    float r = yn + rMinusY, b = yn + bMinusY;
                    float g = yn - 0.50935f * rMinusY - 0.19420f * bMinusY;      // 0.299 / 0.587 and 0.114 / 0.587
                    row[x] = 0xFF000000 | (uint)Byte(r) << 16 | (uint)Byte(g) << 8 | Byte(b);
                }
            }
        }
    }

    static byte Byte(float value) => (byte)Math.Clamp((int)MathF.Round(value * 255), 0, 255);

    static float Wrap(float angle)
    {
        while (angle > MathF.PI) angle -= 2 * MathF.PI;
        while (angle < -MathF.PI) angle += 2 * MathF.PI;
        return angle;
    }

    /// <summary>The first sample of the sync pulse of a line, or -1 if the line has none.</summary>
    static int FindSync(float[] data, int lineStart, int samplesPerLine)
    {
        int end = Math.Min(data.Length, lineStart + samplesPerLine / 2);
        for (int i = lineStart; i < end; i++)
            if (data[i] < SyncThreshold) return i;
        return -1;
    }

    /// <summary>The first line of the vertical sync: two lines in a row that are low for most of their length.</summary>
    static int FindFieldStart(float[] data, int samplesPerLine, int lines)
    {
        bool Broad(int line)
        {
            int low = 0, start = line * samplesPerLine;
            for (int i = start; i < start + samplesPerLine; i++)
                if (data[i] < SyncThreshold) low++;
            return low > samplesPerLine * 4 / 10;
        }
        for (int line = 0; line + 1 < lines; line++)
            if (Broad(line) && Broad(line + 1)) return line;
        return 0;
    }
}

/// <summary>
/// A monitor on the composite input: it decodes the signal and shows the picture on another monitor (<see cref="IMonitor"/>: a window, a canvas). The video cable
/// of a machine with composite output plugs into this, and the screen behind it needs no change
/// </summary>
public sealed class CompositeTuner(IMonitor screen) : ICompositeMonitor
{
    CompositeDecoder? decoder;
    VideoStandard? decoded;

    public unsafe void ShowComposite(CompositeFrame frame)
    {
        if (decoder is null || !ReferenceEquals(decoded, frame.Standard))
        {
            decoder = new CompositeDecoder(frame.Standard);
            decoded = frame.Standard;
        }
        var pixels = screen.BeginFrame(decoder.Mode);
        try
        {
            decoder.Decode(frame, pixels);
        }
        finally
        {
            screen.EndFrame();
        }
    }
}
