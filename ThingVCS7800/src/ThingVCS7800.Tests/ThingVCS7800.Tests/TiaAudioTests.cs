using ThingVCS;
using ThingVCS7800.A7800;
namespace ThingVCS7800.Tests;
public class TiaAudioTests
{
    static void Clock(TiaAudio audio, int ticks = 1)
    { for (int i = 0; i < ticks * TiaAudio.MasterClocksPerAudioClock; i++) audio.Tick(); }
    [Fact] public void ResetAndVolumeMasksAndIndependentChannels()
    {
        var chip = new TiaAudio(); Assert.Equal(0, chip.AudioLevel);
        chip.WriteRegister(0x19, 0xff); Assert.Equal(.5, chip.AudioLevel);
        chip.WriteRegister(0x1a, 15); Assert.Equal(1, chip.AudioLevel);
        chip.WriteRegister(0x15, 0xf4); chip.WriteRegister(0x17, 0xff);
        Assert.Equal(4, chip.RegisterValue(0x15)); Assert.Equal(31, chip.RegisterValue(0x17));
        chip.Reset(); Assert.Equal(0, chip.AudioLevel); Assert.Equal(0, chip.AudioClocks);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(7)][InlineData(31)]
    public void PureToneTogglesAtFrequencyPlusOne(int frequency)
    {
        var chip = new TiaAudio(); chip.WriteRegister(0x15, 4); chip.WriteRegister(0x17, (byte)frequency); chip.WriteRegister(0x19, 15);
        Assert.Equal(0, chip.AudioLevel);
        Clock(chip, frequency + 1); Assert.Equal(.5, chip.AudioLevel);
        Clock(chip, frequency + 1); Assert.Equal(0, chip.AudioLevel);
    }
    [Fact] public void DivideByThreeToneAndVolumeChangesWithoutPhaseReset()
    {
        var chip = new TiaAudio(); chip.WriteRegister(0x15, 12); chip.WriteRegister(0x19, 15);
        Clock(chip, 2); Assert.Equal(0, chip.AudioLevel);
        Clock(chip); Assert.Equal(.5, chip.AudioLevel);
        chip.WriteRegister(0x19, 3); Assert.Equal(.1, chip.AudioLevel);
        Clock(chip, 3); Assert.Equal(0, chip.AudioLevel);
    }
    [Theory][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(6)][InlineData(7)]
    [InlineData(8)][InlineData(9)][InlineData(10)][InlineData(15)]
    public void NoiseAndGatedModesAreDeterministicBoundedAndNotSilent(int mode)
    {
        var first = new TiaAudio(); var second = new TiaAudio();
        foreach (var chip in new[] { first, second }) { chip.WriteRegister(0x15, (byte)mode); chip.WriteRegister(0x19, 15); }
        var levels = new HashSet<double>();
        for (int i = 0; i < 2048; i++)
        {
            Clock(first); Clock(second); Assert.Equal(first.AudioLevel, second.AudioLevel);
            Assert.InRange(first.AudioLevel, 0, .5); levels.Add(first.AudioLevel);
        }
        Assert.Contains(.5, levels); Assert.Contains(0, levels);
        first.Reset(); Assert.Equal(0, first.AudioLevel);
    }
    [Fact] public void RegisterReadsRemainOpenBusAndAudioWritesDoNotChangeFireInputs()
    {
        var chip = new TiaInputs { Fire1High = () => false };
        chip.WriteRegister(0x19, 15); Assert.Equal(.5, chip.AudioLevel);
        Assert.Equal(0x55, chip.ReadRegister(0x19, 0x55));
        Assert.Equal(0x7f, chip.ReadRegister(12, 0xff));
        chip.Reset(); Assert.Equal(0, chip.AudioLevel);
    }
    sealed class Speaker : ThingRetroEmu.Audio.ISpeaker
    {
        public int SampleRate => 44100;
        public List<short> Samples = [];
        public void Play(ReadOnlySpan<short> samples) { foreach (short sample in samples) Samples.Add(sample); }
    }
    [Fact] public void CommonAudioPortReceivesTiaWithoutPokeyCartridge()
    {
        var host = new DevelopmentComputer(); var speaker = new Speaker(); host.Board.Audio.Plug(speaker);
        host.Board.Write(0x15, 4); host.Board.Write(0x17, 7); host.Board.Write(0x19, 15);
        for (int i = 0; i < 40000; i++) host.Board.Tick();
        Assert.NotEmpty(speaker.Samples); Assert.Contains(speaker.Samples, sample => sample != 0);
        host.Board.Audio.Unplug(speaker);
        host.Board.TiaInputSocket.Remove(); var silent = new Speaker(); host.Board.Audio.Plug(silent);
        for (int i = 0; i < 40000; i++) host.Board.Tick();
        Assert.NotEmpty(silent.Samples); Assert.All(silent.Samples, sample => Assert.Equal(0, sample));
    }
    [Fact] public void BoardMirrorsAudioRegistersAndClocksWhileCpuHalted()
    {
        var host = new DevelopmentComputer(); var tia = Assert.IsType<TiaInputs>(host.Board.TiaInputSocket.Chip);
        host.Board.Write(0x315, 4); host.Board.Write(0x119, 15);
        host.Board.Write(0x24, 0); // WSYNC stalls CPU, not the sound generator.
        for (int i = 0; i < 200; i++) host.Board.Tick();
        Assert.Equal(host.Board.MasterClocks / TiaAudio.MasterClocksPerAudioClock, tia.Audio.AudioClocks);
        Assert.True(tia.Audio.AudioClocks > 0); Assert.Equal(15, tia.Audio.RegisterValue(0x19));
        host.Board.TiaInputSocket.Remove();
        for (int i = 0; i < 200; i++) host.Board.Tick();
        host.Reset(); Assert.Equal(0, host.Board.MasterClocks);
    }
}
