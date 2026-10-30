using ThingVCS;
namespace ThingVCS7800.Tests;
public class PokeyExtendedTests
{
    static void Tick(Pokey chip, int clocks) { for (int i = 0; i < clocks; i++) chip.Tick(); }
    [Theory][InlineData(0,28)][InlineData(1,114)][InlineData(0x40,4)][InlineData(0x50,7)]
    public void ClockAndJoinedPeriodsFollowAudctl(int control, int expected)
    {
        var chip = new Pokey(); chip.Write(8, (byte)control);
        int channel = (control & 0x10) != 0 ? 1 : 0;
        Assert.Equal(expected, chip.ChannelPeriod(channel));
    }
    [Fact] public void JoinedFrequencyUsesBothBytesAndSuppressesLowTone()
    {
        var chip = new Pokey(); chip.Write(15,3); chip.Write(0, 0x34); chip.Write(2,0x12); chip.Write(8,0x50);
        Assert.Equal(0x1234 + 7, chip.ChannelPeriod(1));
        chip.Write(1,0xaf); Tick(chip,1000); Assert.Equal(0,chip.AudioLevel);
        chip.Write(3,0xaf); Tick(chip,chip.ChannelPeriod(1)); Assert.InRange(chip.AudioLevel,0,.25);
    }
    [Theory][InlineData(0x80)][InlineData(0xc0)][InlineData(0)]
    public void NoiseIsDeterministicAndHasBothLevels(int control)
    {
        var first = new Pokey(); var second = new Pokey();
        foreach (var chip in new[] { first, second }) { chip.Write(15,3); chip.Write(8,0x40); chip.Write(1,(byte)(control|15)); }
        HashSet<double> values = [];
        for (int i=0;i<4096;i++) { first.Tick(); second.Tick(); Assert.Equal(first.AudioLevel,second.AudioLevel); values.Add(first.AudioLevel); }
        Assert.Contains(0,values); Assert.Contains(.25,values);
    }
    [Fact] public void RandomUsesPolynomialAndStopsInSkctlReset()
    {
        var chip = new Pokey(); chip.Write(15,3); HashSet<byte> values = [];
        for (int i=0;i<600;i++) { chip.Tick(); values.Add(chip.Read(10)); }
        Assert.True(values.Count > 100);
        chip.Write(15,0); Tick(chip,100); Assert.Equal(255,chip.Read(10));
        chip.Reset(); Assert.False(chip.Irq); Assert.Equal(0,chip.Clocks);
    }
    [Fact] public void TimerFlagsAreActiveLowAndReadDoesNotAcknowledge()
    {
        var chip = new Pokey(); chip.Write(15,3); chip.Write(14,7); chip.Write(9,0);
        Tick(chip,31); Assert.False(chip.Irq); chip.Tick(); Assert.True(chip.Irq);
        Assert.Equal(0xf8,chip.Read(14)); Assert.Equal(0xf8,chip.Read(14));
        chip.Write(14,0); Assert.False(chip.Irq); Assert.Equal(255,chip.Read(14));
    }
    [Fact] public void FilterClockLatchesAndSuppressesMatchingOutput()
    {
        var chip = new Pokey(); chip.Write(15,3); chip.Write(1,0xaf); chip.Write(8,0x44); // CH1 fast, CH3 base filter clock.
        Tick(chip,4); Assert.Equal(.25,chip.AudioLevel);
        Tick(chip,24); Assert.Equal(0,chip.AudioLevel);
    }
    [Theory][InlineData(1,15)][InlineData(9,31)][InlineData(8,511)]
    public void TiaNoiseRepeatsAtPolynomialPeriod(int mode,int period)
    {
        var audio=new TiaAudio(); audio.WriteRegister(0x15,(byte)mode); audio.WriteRegister(0x19,15);
        List<double> values=[];
        for(int i=0;i<period*2;i++) { for(int c=0;c<TiaAudio.MasterClocksPerAudioClock;c++) audio.Tick(); values.Add(audio.AudioLevel); }
        Assert.Equal(values.Take(period),values.Skip(period)); Assert.Contains(0,values); Assert.Contains(.5,values);
    }
    [Fact] public void TiaLoweringFrequencyWaitsForFiveBitWrap()
    {
        var audio=new TiaAudio(); audio.WriteRegister(0x15,4); audio.WriteRegister(0x17,31); audio.WriteRegister(0x19,15);
        for(int i=0;i<10*228;i++) audio.Tick(); audio.WriteRegister(0x17,0);
        for(int i=0;i<22*228;i++) audio.Tick(); Assert.Equal(0,audio.AudioLevel);
        for(int i=0;i<228;i++) audio.Tick(); Assert.Equal(.5,audio.AudioLevel);
        audio.WriteRegister(0x15,11); Assert.Equal(.5,audio.AudioLevel);
    }
}
