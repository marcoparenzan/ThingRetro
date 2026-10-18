namespace ThingRetroEmu.Tests;

// The composite video signal: structure, encoding of a picture and decoding by a monitor
[TestClass]
public sealed class CompositeTests
{
    // Colour bars: white, yellow, cyan, green, magenta, red, blue, black, at 75 %
    static readonly uint[] Bars =
    [
        Rgb(191, 191, 191), Rgb(191, 191, 0), Rgb(0, 191, 191), Rgb(0, 191, 0), Rgb(191, 0, 191), Rgb(191, 0, 0), Rgb(0, 0, 191), Rgb(0, 0, 0),
    ];

    static uint Rgb(int r, int g, int b) => 0xFF000000u | (uint)r << 16 | (uint)g << 8 | (uint)b;

    sealed unsafe class BarsSource(int width, int height, double fps) : IVideoSource
    {
        public VideoMode Mode => new(width, height, fps);

        public void Render(byte* pixels)
        {
            uint* p = (uint*)pixels;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    p[y * width + x] = Bars[x * 8 / width];
        }
    }

    sealed unsafe class Capture : IMonitor
    {
        public VideoMode Mode;
        public uint[] Pixels = [];
        System.Runtime.InteropServices.GCHandle handle;
        public int Frames;

        public byte* BeginFrame(VideoMode mode)
        {
            Mode = mode;
            Pixels = new uint[mode.PixelCount];
            handle = System.Runtime.InteropServices.GCHandle.Alloc(Pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
            return (byte*)handle.AddrOfPinnedObject();
        }

        public void EndFrame()
        {
            handle.Free();
            Frames++;
        }
    }

    static CompositeFrame Encode(VideoStandard standard, int width = 320, int height = 270)
    {
        var frame = new CompositeFrame(standard);
        new RgbToCompositeSource(new BarsSource(width, height, standard.FieldHz), standard).RenderComposite(frame);
        return frame;
    }

    static (int R, int G, int B) Pixel(Capture capture, int x, int y)
    {
        uint p = capture.Pixels[y * capture.Mode.Width + x];
        return ((int)(p >> 16 & 0xFF), (int)(p >> 8 & 0xFF), (int)(p & 0xFF));
    }

    // ---- structure ----------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void Standards_ShouldHaveTheRealTiming()
    {
        Assert.AreEqual(1135, VideoStandard.Pal.SamplesPerLine);
        Assert.AreEqual(910, VideoStandard.Ntsc.SamplesPerLine);
        Assert.AreEqual(50.0, VideoStandard.Pal.FieldHz, 0.2);
        Assert.AreEqual(59.9, VideoStandard.Ntsc.FieldHz, 0.2);
        Assert.AreEqual(VideoStandard.Pal, VideoStandard.For(50.12));
        Assert.AreEqual(VideoStandard.Ntsc, VideoStandard.For(59.94));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Structure_ShouldHaveSyncBurstAndVerticalSync(bool pal)
    {
        var standard = pal ? VideoStandard.Pal : VideoStandard.Ntsc;
        var frame = new CompositeFrame(standard);

        CompositeEncoder.Structure(frame);

        var line = frame.Line(standard.VerticalSyncLines + 5);
        Assert.AreEqual(CompositeLevels.Sync, line[0]);
        Assert.AreEqual(CompositeLevels.Sync, line[standard.SyncSamples - 1]);
        Assert.AreEqual(CompositeLevels.Blank, line[standard.SyncSamples + 1]);
        float burstPeak = 0;
        for (int i = standard.BurstStart; i < standard.BurstStart + standard.BurstLength; i++) burstPeak = Math.Max(burstPeak, Math.Abs(line[i]));
        // at four samples a cycle the samples of a PAL burst (45 or 135 degrees) are never at its peak: 0.15 / sqrt 2
        Assert.AreEqual(pal ? CompositeLevels.BurstPeak / MathF.Sqrt(2) : CompositeLevels.BurstPeak, burstPeak, 0.01f, "the burst");
        int low = 0;
        foreach (float v in frame.Line(0)) if (v < -0.2f) low++;
        Assert.IsTrue(low > standard.SamplesPerLine * 8 / 10, "the vertical sync is low for most of the line");
        Assert.AreEqual(0, frame.Line(1)[standard.SamplesPerLine - 1], "and goes back to blanking at the end");
    }

    [TestMethod]
    public void Structure_PalBurst_ShouldSwingFromLineToLine()
    {
        var frame = new CompositeFrame(VideoStandard.Pal);
        CompositeEncoder.Structure(frame);
        int sample = VideoStandard.Pal.BurstStart + 5;

        float even = frame.Line(40)[sample], odd = frame.Line(41)[sample];

        Assert.AreNotEqual(even, odd, 0.01f);
    }

    // ---- decoding -----------------------------------------------------------------------------------------------------------

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Decode_ColourBars_ShouldComeBackWithTheirColours(bool pal)
    {
        var standard = pal ? VideoStandard.Pal : VideoStandard.Ntsc;
        var capture = new Capture();
        var tuner = new CompositeTuner(capture);

        tuner.ShowComposite(Encode(standard));

        Assert.AreEqual(standard.ActiveLines, capture.Mode.Height);
        int row = capture.Mode.Height / 2;
        for (int bar = 0; bar < 8; bar++)
        {
            int x = (int)((bar + 0.5) * capture.Mode.Width / 8);
            var (r, g, b) = Pixel(capture, x, row);
            uint expected = Bars[bar];
            int er = (int)(expected >> 16 & 0xFF), eg = (int)(expected >> 8 & 0xFF), eb = (int)(expected & 0xFF);
            Assert.IsTrue(Math.Abs(r - er) < 40 && Math.Abs(g - eg) < 40 && Math.Abs(b - eb) < 40,
                $"bar {bar}: expected ({er},{eg},{eb}) got ({r},{g},{b})");
        }
    }

    [TestMethod]
    public void Decode_Grey_ShouldHaveNoColour()
    {
        var standard = VideoStandard.Pal;
        var frame = new CompositeFrame(standard);
        var grey = Enumerable.Repeat(Rgb(128, 128, 128), 100).ToArray();
        CompositeEncoder.Structure(frame);
        for (int line = standard.FirstActiveLine; line < standard.FirstActiveLine + standard.ActiveLines; line++)
            CompositeEncoder.EncodeLine(frame, line, grey);
        var capture = new Capture();

        new CompositeTuner(capture).ShowComposite(frame);

        var (r, g, b) = Pixel(capture, capture.Mode.Width / 2, capture.Mode.Height / 2);
        Assert.IsTrue(Math.Abs(r - g) < 10 && Math.Abs(g - b) < 10, $"({r},{g},{b})");
        Assert.IsTrue(r is > 100 and < 160, $"grey level {r}");
    }

    [TestMethod]
    public void Decode_LumaRamp_ShouldGrowLeftToRight()
    {
        var standard = VideoStandard.Ntsc;
        var frame = new CompositeFrame(standard);
        var ramp = Enumerable.Range(0, 256).Select(i => Rgb(i, i, i)).ToArray();
        CompositeEncoder.Structure(frame);
        for (int line = standard.FirstActiveLine; line < standard.FirstActiveLine + standard.ActiveLines; line++)
            CompositeEncoder.EncodeLine(frame, line, ramp);
        var capture = new Capture();

        new CompositeTuner(capture).ShowComposite(frame);

        int row = capture.Mode.Height / 2, width = capture.Mode.Width;
        int last = -1;
        for (int x = width / 8; x < width * 7 / 8; x += width / 8)
        {
            int level = Pixel(capture, x, row).G;
            Assert.IsTrue(level > last, $"x={x}: {level} after {last}");
            last = level;
        }
    }

    [TestMethod]
    public void Decode_WithTheBurstMissing_ShouldShowBlackAndWhite()
    {
        var standard = VideoStandard.Pal;
        var frame = Encode(standard);
        for (int line = 0; line < standard.LinesPerField; line++)
            for (int i = standard.BurstStart; i < standard.BurstStart + standard.BurstLength; i++) frame.Line(line)[i] = 0;
        var capture = new Capture();

        new CompositeTuner(capture).ShowComposite(frame);

        int row = capture.Mode.Height / 2;
        for (int bar = 0; bar < 8; bar++)
        {
            var (r, g, b) = Pixel(capture, (int)((bar + 0.5) * capture.Mode.Width / 8), row);
            Assert.IsTrue(Math.Abs(r - g) < 6 && Math.Abs(g - b) < 6, $"bar {bar}: ({r},{g},{b})");
        }
    }

    [TestMethod]
    public void Decode_ThePhaseOfTheSubcarrier_ShouldBeLockedOnTheBurst()
    {
        // The whole line, burst and picture, is delayed by one sample: the subcarrier phase turns by 90 degrees, and the colours must not
        var standard = VideoStandard.Ntsc;
        var frame = Encode(standard);
        var shifted = new CompositeFrame(standard);
        for (int line = 0; line < standard.LinesPerField; line++)
        {
            var from = frame.Line(line);
            var to = shifted.Line(line);
            for (int i = 1; i < from.Length; i++) to[i] = from[i - 1];
            to[0] = CompositeLevels.Blank;
        }
        var capture = new Capture();

        new CompositeTuner(capture).ShowComposite(shifted);

        int row = capture.Mode.Height / 2;
        var (r, g, b) = Pixel(capture, (int)(1.5 * capture.Mode.Width / 8), row);   // yellow
        Assert.IsTrue(r > 150 && g > 150 && b < 60, $"yellow came out ({r},{g},{b})");
    }

    // ---- the port -------------------------------------------------------------------------------------------------------------

    sealed class CompositeRecorder : ICompositeMonitor
    {
        public readonly List<VideoStandard> Standards = [];
        public void ShowComposite(CompositeFrame frame) => Standards.Add(frame.Standard);
    }

    [TestMethod]
    public void Port_WithACompositeMonitor_ShouldEncodeTheSourceWithTheStandardOfItsRate()
    {
        var port = new VideoPort();
        var recorder = new CompositeRecorder();
        using var cable = new MonitorCable(port, recorder);

        port.Present(new BarsSource(200, 200, 50.12));
        port.Present(new BarsSource(200, 200, 59.94));

        CollectionAssert.AreEqual(new[] { VideoStandard.Pal, VideoStandard.Ntsc }, recorder.Standards);
    }

    [TestMethod]
    public void Port_CompositeCableAndTuner_ShouldDeliverTheDecodedPictureToAScreen()
    {
        var port = new VideoPort();
        var screen = new Capture();
        using var cable = new MonitorCable(port, new CompositeTuner(screen));

        port.Present(new BarsSource(403, 284, 50.12));
        port.Present(new BarsSource(403, 284, 50.12));

        Assert.AreEqual(2, screen.Frames);
        Assert.AreEqual(VideoStandard.Pal.ActiveLines, screen.Mode.Height);
        var (r, g, b) = Pixel(screen, (int)(0.5 * screen.Mode.Width / 8), screen.Mode.Height / 2);   // the white bar
        Assert.IsTrue(r > 150 && g > 150 && b > 150, $"({r},{g},{b})");
    }

    [TestMethod]
    public void Port_ShouldRefuseTwoMonitors()
    {
        var port = new VideoPort();
        using var first = new MonitorCable(port, new CompositeRecorder());

        Assert.ThrowsExactly<InvalidOperationException>(() => new MonitorCable(port, new Capture()));
    }
}
