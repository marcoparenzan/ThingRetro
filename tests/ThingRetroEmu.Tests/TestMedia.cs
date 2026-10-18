namespace ThingRetroEmu.Tests;

// The file formats (.prg, .crt) and the video output: a monitor that follows the size of the picture
[TestClass]
public sealed class MediaAndVideoTests
{
    [TestMethod]
    public void Prg_ShouldGiveTheRangeOfTheProgram()
    {
        var prg = PrgFile.Build(0x0801, [1, 2, 3, 4]);

        var (start, end) = PrgFile.GetRange(prg);

        Assert.AreEqual((ushort)0x0801, start);
        Assert.AreEqual(0x0805, end);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, PrgFile.Data(prg).ToArray());
    }

    [TestMethod]
    public void Prg_WithoutData_ShouldBeRefused()
    {
        Assert.ThrowsExactly<ArgumentException>(() => PrgFile.GetRange([0x01, 0x08]));
    }

    [TestMethod]
    public void Prg_PastTheEndOfMemory_ShouldBeRefused()
    {
        Assert.ThrowsExactly<ArgumentException>(() => PrgFile.GetRange([0xFF, 0xFF, 1, 2, 3]));
    }

    [TestMethod]
    public void Crt_ShouldRoundTrip()
    {
        var rom = new byte[0x2000];
        Array.Fill(rom, (byte)0xAB);
        var image = new CrtImage("test cart", 19, exromHigh: false, gameHigh: true,
            [new CrtImage.Chip(0, 0, 0x8000, rom), new CrtImage.Chip(0, 1, 0x8000, rom)]);

        var again = CrtImage.Parse(image.ToBytes());

        Assert.AreEqual("TEST CART", again.Name);
        Assert.AreEqual(19, again.HardwareType);
        Assert.IsFalse(again.ExromHigh);
        Assert.IsTrue(again.GameHigh);
        Assert.AreEqual(2, again.Chips.Count);
        Assert.AreEqual(1, again.Chips[1].Bank);
        Assert.AreEqual(0x8000, again.Chips[1].LoadAddress);
        CollectionAssert.AreEqual(rom, again.Chips[0].Data);
    }

    [TestMethod]
    public void Crt_WithoutTheHeader_ShouldBeRefused()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => CrtImage.Parse(new byte[100]));
    }

    // ---- video -----------------------------------------------------------------------------------------------------------

    sealed unsafe class Source(VideoMode mode) : IVideoSource
    {
        public VideoMode Mode { get; set; } = mode;

        public void Render(byte* pixels)
        {
            for (int i = 0; i < Mode.PixelCount; i++) ((uint*)pixels)[i] = (uint)i | 0xFF000000;
        }
    }

    sealed unsafe class Recorder : IMonitor
    {
        public readonly List<VideoMode> Modes = [];
        public uint[] Last = [];
        System.Runtime.InteropServices.GCHandle handle;

        public byte* BeginFrame(VideoMode mode)
        {
            Modes.Add(mode);
            Last = new uint[mode.PixelCount];
            handle = System.Runtime.InteropServices.GCHandle.Alloc(Last, System.Runtime.InteropServices.GCHandleType.Pinned);
            return (byte*)handle.AddrOfPinnedObject();
        }

        public void EndFrame() => handle.Free();
    }

    [TestMethod]
    public void Present_ShouldGiveTheMonitorABufferOfTheModeAndFillIt()
    {
        var port = new VideoPort();
        var monitor = new Recorder();
        var source = new Source(new VideoMode(160, 192, 59.94, 2));
        using var cable = new MonitorCable(port, monitor);

        port.Present(source);

        Assert.AreEqual(1, monitor.Modes.Count);
        Assert.AreEqual(160, monitor.Modes[0].Width);
        Assert.AreEqual(2.0, monitor.Modes[0].PixelAspect);
        Assert.AreEqual(160 * 192, monitor.Last.Length);
        Assert.AreEqual(0xFF000000u | (160 * 192 - 1), monitor.Last[^1]);
    }

    [TestMethod]
    public void Present_WhenTheSourceChangesTheMode_ShouldFollow()
    {
        var port = new VideoPort();
        var monitor = new Recorder();
        var source = new Source(new VideoMode(403, 284));
        using var cable = new MonitorCable(port, monitor);

        port.Present(source);
        source.Mode = new VideoMode(806, 568);
        port.Present(source);

        Assert.AreEqual(806 * 568, monitor.Last.Length);
        Assert.AreEqual(2, monitor.Modes.Count);
    }

    [TestMethod]
    public void Present_WithoutAMonitor_ShouldDoNothing()
    {
        var port = new VideoPort();

        port.Present(new Source(new VideoMode(10, 10)));

        Assert.IsFalse(port.IsConnected);
    }

    [TestMethod]
    public void Cable_ShouldRefuseASecondMonitorAndUnplugOnDispose()
    {
        var port = new VideoPort();
        var first = new Recorder();
        var cable = new MonitorCable(port, first);

        Assert.ThrowsExactly<InvalidOperationException>(() => new MonitorCable(port, new Recorder()));
        cable.Dispose();

        Assert.IsFalse(port.IsConnected);
        using var again = new MonitorCable(port, new Recorder());
        Assert.IsTrue(port.IsConnected);
    }

    [TestMethod]
    public void VideoMode_ShouldKnowHowItLooks()
    {
        var mode = new VideoMode(160, 192, 60, 2);

        Assert.AreEqual(160 * 192, mode.PixelCount);
        Assert.AreEqual(160 * 192 * 4, mode.ByteCount);
        Assert.AreEqual(320.0 / 192, mode.DisplayAspect, 1e-9);
    }

    // ---- audio -----------------------------------------------------------------------------------------------------------

    sealed class Speaker(int rate) : ISpeaker
    {
        public readonly List<short> Samples = [];
        public int SampleRate => rate;
        public void Play(ReadOnlySpan<short> samples) => Samples.AddRange(samples.ToArray());
    }

    [TestMethod]
    public void AudioPort_ShouldResampleToTheSpeakerInBlocksOf10Ms()
    {
        var port = new AudioPort(1_000_000);
        var speaker = new Speaker(44_100);
        using var cable = new AudioCable(port, speaker);

        for (int i = 0; i < 100_000; i++) port.Sample(i % 2 == 0 ? 0.5 : -0.5);   // 100 ms

        Assert.IsTrue(speaker.Samples.Count is > 4000 and <= 4410, $"{speaker.Samples.Count} samples");
    }
}
