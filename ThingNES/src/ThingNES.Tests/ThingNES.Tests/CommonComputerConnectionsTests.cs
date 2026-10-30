using ThingRetroEmu.Audio;
using ThingRetroEmu.Video;
namespace ThingNES.Tests;
public class CommonComputerConnectionsTests
{
    static NesMachine Machine()
    {
        var image=new byte[16400]; "NES\u001a"u8.CopyTo(image);image[4]=1;
        image[16]=0x4c;image[17]=0;image[18]=0xc0;image[16+0x3ffd]=0xc0;
        return new NesMachine(Cartridge.Load(image));
    }
    sealed class Speaker : ISpeaker
    {
        public int SampleRate=>44100;
        public List<short> Samples=[];
        public void Play(ReadOnlySpan<short> samples){foreach(short sample in samples)Samples.Add(sample);}
    }
    [Fact] public void FramesReachCommonMonitorThroughCable()
    {
        var machine=Machine();using var monitor=new BufferedMonitor();using var cable=new MonitorCable(machine.Video,monitor);
        machine.RunFrame();machine.RunFrame();
        Assert.Equal(256,monitor.Width);Assert.Equal(240,monitor.Height);Assert.Equal(256*240*4,monitor.Rgba().Length);
        Assert.Equal(VideoSystem.Ntsc,machine.Video.DetectedSystem);
        cable.Dispose();Assert.False(machine.Video.IsConnected);
    }
    [Fact] public void AudioSocketFeedsCommonSpeakerWithoutChangingApuSampleEvents()
    {
        var machine=Machine();var speaker=new Speaker();using var cable=new AudioCable(machine.Audio,speaker);
        int rawCount=0;machine.Apu.SampleProduced+=_=>rawCount++;
        machine.Write(0x4011,64);machine.RunCycles(80000);
        Assert.True(rawCount>0);Assert.NotEmpty(speaker.Samples);Assert.Contains(speaker.Samples,s=>s!=0);
        Assert.Equal(44100,machine.Audio.ClockHz);
        cable.Dispose();Assert.False(machine.Audio.IsConnected);
    }
    [Fact] public void RecorderAndSpeakerCanShareTheNesAudioOutput()
    {
        var machine=Machine();var speaker=new Speaker();var splitter=new AudioSplitter();
        using var stream=new MemoryStream();using var recorder=new WaveRecorder(stream,leaveOpen:true);
        splitter.Connect(speaker);splitter.Connect(recorder);using var cable=new AudioCable(machine.Audio,splitter);
        machine.RunCycles(80000);Assert.Equal(speaker.Samples.Count,recorder.SampleCount);Assert.True(recorder.SampleCount>0);
        splitter.Disconnect(recorder);recorder.Dispose();Assert.Equal(44+recorder.SampleCount*2,stream.Length);
    }
}
