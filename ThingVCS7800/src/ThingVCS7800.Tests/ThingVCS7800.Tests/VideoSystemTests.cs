using System.Buffers.Binary;
using ThingRetroEmu.Video;
using ThingRetroEmu.Audio;
using ThingVCS;
using ThingVCS7800.A7800;
namespace ThingVCS7800.Tests;

public class VideoSystemTests
{
    static byte[] Image(bool pal)
    {
        var bytes = new byte[16384 + 128]; bytes[0] = 1;
        "ATARI7800"u8.CopyTo(bytes.AsSpan(1));
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(49, 4), 16384);
        bytes[57] = pal ? (byte)1 : (byte)0;
        // Infinite JMP at C000 and reset vector.
        bytes[128] = 0x4c; bytes[130] = 0xc0; bytes[^3] = 0xc0;
        return bytes;
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void AutoUsesCartridgeRegion(bool pal)
    {
        var host = new DevelopmentComputer(); host.InsertImage(Image(pal), true);
        var system = pal ? VideoSystem.Pal : VideoSystem.Ntsc;
        Assert.Equal(system, host.Board.Timing.System);
        Assert.Equal(system, host.Board.Video.DetectedSystem);
        Assert.Equal(system, CartridgeImage.Parse(Image(pal), true).TvSystem);
        var maria = Assert.IsType<Maria>(host.Board.MariaSocket.Chip);
        Assert.Equal(pal ? 312 : 262, maria.Timing.Lines);
        Assert.Equal(pal ? 292 : 242, maria.Mode.Height);
        Assert.Equal(maria.Mode.PixelCount, maria.Framebuffer.Length);
        Assert.Equal(pal ? VideoStandard.Pal : VideoStandard.Ntsc, host.Board.Video.ResolveStandard(maria.Mode.FramesPerSecond));
    }
    [Fact] public void ExplicitOverrideDoesNotChangeMetadataAndAutoRestoresPal()
    {
        var host = new DevelopmentComputer(); host.InsertImage(Image(true), true);
        host.Board.Tick();
        host.Board.Video.SystemSelection = VideoSystem.Ntsc;
        Assert.Equal(VideoSystem.Ntsc, host.Board.Timing.System);
        Assert.Equal(VideoSystem.Pal, host.Board.Video.DetectedSystem);
        Assert.Equal(0, host.Board.Cycles);
        host.Board.Video.SystemSelection = VideoSystem.Auto;
        Assert.Equal(VideoSystem.Pal, host.Board.Timing.System);
        Assert.Equal(1773447, host.Board.Audio.ClockHz);
    }
    [Theory][InlineData(VideoSystem.Pal,312)][InlineData(VideoSystem.Ntsc,262)]
    public void RasterWrapsAtSelectedLineCount(VideoSystem system, int lines)
    {
        var maria = new Maria(); maria.Configure(system);
        for (int i = 0; i < lines * Maria.DotsPerLine; i++) maria.Tick();
        Assert.Equal(1, maria.Frames); Assert.Equal(0, maria.Scanline);
        Assert.Equal(MariaTiming.For(system).FramesPerSecond, maria.Mode.FramesPerSecond);
    }
    [Fact] public void TimingSelectionWorksWithMariaSocketEmpty()
    {
        var host = new DevelopmentComputer(); host.Board.MariaSocket.Remove();
        host.Board.Video.SystemSelection = VideoSystem.Pal;
        Assert.Equal(1773447, host.Board.Audio.ClockHz);
        host.Board.MariaSocket.Insert(new Maria());
        Assert.Equal(VideoSystem.Pal, Assert.IsType<Maria>(host.Board.MariaSocket.Chip).Timing.System);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void PacingUsesSelectedMasterClock(bool pal)
    {
        var host = new DevelopmentComputer(); host.InsertImage(Image(pal), true);
        host.Advance(TimeSpan.FromMilliseconds(10));
        // Fractional credit below one CPU slot is retained for the next Advance call.
        Assert.InRange(host.Board.MasterClocks, host.Board.Timing.MasterClockHz / 100 - 4,
            host.Board.Timing.MasterClockHz / 100 + 6);
    }
    [Fact] public void InvalidLoadPreservesCurrentRegionAndCartridge()
    {
        var host = new DevelopmentComputer(); host.InsertImage(Image(true), true);
        var old = host.Board.CartridgeSocket.Chip;
        var invalid = Image(false); invalid[54] = 0xff;
        Assert.Throws<NotSupportedException>(() => host.InsertImage(invalid, true));
        Assert.Same(old, host.Board.CartridgeSocket.Chip);
        Assert.Equal(VideoSystem.Pal, host.Board.Timing.System);
    }
    [Fact] public void CommonPortFallsBackToRateButCanForceOrDetectSystem()
    {
        var port = new VideoPort();
        Assert.Equal(VideoStandard.Ntsc, port.ResolveStandard(60));
        Assert.Equal(VideoStandard.Pal, port.ResolveStandard(50));
        port.DetectedSystem = VideoSystem.Pal;
        Assert.Equal(VideoStandard.Pal, port.ResolveStandard(60));
        port.SystemSelection = VideoSystem.Ntsc;
        Assert.Equal(VideoStandard.Ntsc, port.ResolveStandard(50));
        // Cable encoding override is independent of console timing.
        port.Standard = VideoStandard.Pal;
        Assert.Equal(VideoStandard.Pal, port.ResolveStandard(60));
        Assert.Equal(VideoSystem.Ntsc, port.EffectiveSystem);
        Assert.Throws<ArgumentOutOfRangeException>(() => port.DetectedSystem = VideoSystem.Auto);
        Assert.Throws<ArgumentOutOfRangeException>(() => port.SystemSelection = (VideoSystem)99);
    }
    sealed class Speaker : ISpeaker
    {
        public int SampleRate => 100;
        public int Samples;
        public void Play(ReadOnlySpan<short> samples) => Samples += samples.Length;
    }
    [Fact] public void AudioClockChangePreservesConnectionAndUpdatesResampling()
    {
        var port = new AudioPort(1000); var speaker = new Speaker(); port.Plug(speaker);
        for (int i = 0; i < 1000; i++) port.Sample(0);
        Assert.Equal(100, speaker.Samples);
        port.SetClockRate(2000);
        for (int i = 0; i < 2000; i++) port.Sample(0);
        Assert.Equal(200, speaker.Samples); Assert.True(port.IsConnected);
        Assert.Throws<ArgumentOutOfRangeException>(() => port.SetClockRate(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => port.SetClockRate(50));
    }
}
