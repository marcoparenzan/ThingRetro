using ThingRetroEmu.Audio;
using ThingVCS;
using ThingVCS7800.A7800;
namespace ThingVCS7800.Tests;
public class AudioSplitterTests
{
    sealed class Speaker(int rate = 44100) : ISpeaker
    {
        public int SampleRate => rate;
        public List<short> Samples = [];
        public void Play(ReadOnlySpan<short> samples) { foreach (short sample in samples) Samples.Add(sample); }
    }
    [Fact] public void DeliversSameSamplesToEachDestinationOnlyOnce()
    {
        var splitter = new AudioSplitter(); var first = new Speaker(); var second = new Speaker();
        splitter.Connect(first); splitter.Connect(first); splitter.Connect(second);
        splitter.Play([1, -2, 3]); Assert.Equal(new short[] { 1, -2, 3 }, first.Samples); Assert.Equal(first.Samples, second.Samples);
        splitter.Disconnect(second); splitter.Disconnect(second); splitter.Play([4]);
        Assert.Equal(4, first.Samples.Count); Assert.Equal(3, second.Samples.Count);
    }
    [Fact] public void RejectsRateMismatchAndFeedbackLoops()
    {
        var splitter = new AudioSplitter();
        Assert.Throws<ArgumentException>(() => splitter.Connect(new Speaker(22050)));
        Assert.Throws<ArgumentException>(() => splitter.Connect(splitter));
        Assert.Throws<ArgumentException>(() => splitter.Connect(new AudioSplitter()));
        Assert.Throws<ArgumentNullException>(() => splitter.Connect(null!));
    }
    [Fact] public void DisconnectBeforeDisposeAllowsListeningToContinue()
    {
        var splitter = new AudioSplitter(); var speaker = new Speaker(); using var stream = new MemoryStream();
        var recorder = new WaveRecorder(stream, leaveOpen: true);
        splitter.Connect(speaker); splitter.Connect(recorder); splitter.Play([1, 2]);
        splitter.Disconnect(recorder); recorder.Dispose(); splitter.Play([3]);
        Assert.Equal(3, speaker.Samples.Count); Assert.Equal(48, stream.Length);
    }
    [Fact] public void AudioCableRecordsAndPlaysWhileHostPauseAddsNoSamples()
    {
        var host = new DevelopmentComputer(); host.Insert(DevelopmentRoms.TiaSound());
        var splitter = new AudioSplitter(); var speaker = new Speaker();
        using var stream = new MemoryStream(); using var recorder = new WaveRecorder(stream, leaveOpen: true);
        splitter.Connect(speaker); splitter.Connect(recorder);
        using var cable = new AudioCable(host.Board.Audio, splitter);
        host.Advance(TimeSpan.FromMilliseconds(30)); long before = recorder.SampleCount;
        Assert.True(before > 0); Assert.Equal(speaker.Samples.Count, recorder.SampleCount);
        host.Paused = true; host.Advance(TimeSpan.FromSeconds(1)); Assert.Equal(before, recorder.SampleCount);
        host.Paused = false; host.Advance(TimeSpan.FromMilliseconds(30)); Assert.True(recorder.SampleCount > before);
        splitter.Disconnect(recorder); recorder.Dispose(); host.Advance(TimeSpan.FromMilliseconds(30));
        Assert.True(speaker.Samples.Count > recorder.SampleCount);
    }
    [Theory][InlineData(0)][InlineData(-1)]
    public void InvalidRateRejected(int rate) => Assert.Throws<ArgumentOutOfRangeException>(() => new AudioSplitter(rate));
}
