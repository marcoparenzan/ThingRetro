using ThingVCS;
namespace ThingVCS7800.Tests;
public class PokeyPeripheralTests
{
    static void Tick(Pokey chip,int n) { for(int i=0;i<n;i++) chip.Tick(); }
    [Theory][InlineData(3,114)][InlineData(7,1)]
    public void PotgoConvertsAtSelectedRateAndLatchesThresholds(int skctl,int divisor)
    {
        int threshold=3; var chip=new Pokey { PotThreshold = i => i==0 ? threshold : 228 };
        chip.Write(15,(byte)skctl); chip.Write(11,0); threshold=10;
        Tick(chip,2*divisor); Assert.Equal(2,chip.Read(0)); Assert.Equal(255,chip.Read(8));
        Tick(chip,divisor); Assert.Equal(3,chip.Read(0)); Assert.Equal(254,chip.Read(8));
        Tick(chip,225*divisor); Assert.Equal(0,chip.Read(8)); Assert.Equal(228,chip.Read(1));
    }
    [Fact] public void ZeroPotCompletesImmediatelyAndResetDisablesConversion()
    {
        var chip=new Pokey { PotThreshold = _ => 0 }; chip.Write(15,7); chip.Write(11,0);
        Assert.Equal(0,chip.Read(8)); Assert.Equal(0,chip.Read(0));
        chip.Write(15,0); chip.Write(11,0); Tick(chip,100); Assert.Equal(228,chip.Read(0));
    }
    [Theory][InlineData(2)][InlineData(3)]
    public void KeyboardScansDebouncesAndEncodesModifiers(int skctl)
    {
        bool pressed=true; var chip=new Pokey { KeyContact = code => code==12 && pressed, ShiftKey=()=>true, ControlKey=()=>true };
        chip.Write(15,(byte)skctl); chip.Write(14,0x40); Tick(chip,16000);
        Assert.Equal(0xcc,chip.Read(9)); Assert.True(chip.Irq); Assert.Equal(0,chip.Read(15)&12);
        chip.Write(14,0); Assert.False(chip.Irq);
        pressed=false; Tick(chip,114); Assert.Equal(4,chip.Read(15)&4);
    }
    static void Receive(Pokey chip,byte value,bool stop=true)
    {
        chip.ClockSerialInput(false);
        for(int i=0;i<8;i++) chip.ClockSerialInput((value & (1<<i))!=0);
        chip.ClockSerialInput(stop);
    }
    [Fact] public void SerialInputTracksFramingOverrunAndSkrest()
    {
        var chip=new Pokey(); chip.Write(15,3); chip.Write(14,0x20);
        Receive(chip,0xa5); Assert.Equal(0xa5,chip.Read(13)); Assert.True(chip.Irq);
        Receive(chip,0x12,false); Assert.Equal(0x12,chip.Read(13)); Assert.Equal(0,chip.Read(15)&0xa0);
        chip.Write(10,0); Assert.Equal(0xa0,chip.Read(15)&0xa0); Assert.True(chip.Irq);
        chip.Write(14,0); Assert.False(chip.Irq);
    }
    [Fact] public void SerialOutputEmitsStartDataStopAndPreservesCompleteStatus()
    {
        var chip=new Pokey(); chip.Write(15,3); chip.Write(14,0x18); chip.Write(13,0xa5);
        var bits=new List<bool>(); for(int i=0;i<10;i++) { chip.ClockSerialOutput(); bits.Add(chip.SerialOutputHigh); }
        Assert.False(bits[0]); Assert.True(bits[9]);
        for(int i=0;i<8;i++) Assert.Equal((0xa5 & (1<<i))!=0,bits[i+1]);
        Assert.Equal(0,chip.Read(14)&0x18); Assert.True(chip.Irq);
        chip.Write(14,0); Assert.False(chip.Irq); Assert.Equal(0,chip.Read(14)&8);
        chip.Write(15,0x83); Assert.False(chip.SerialOutputHigh);
        chip.Write(15,0); Assert.True(chip.SerialOutputHigh);
    }
    [Fact] public void StimerDelayedResetDoesNotAcknowledgePendingTimerIrq()
    {
        var chip=new Pokey(); chip.Write(15,3); chip.Write(14,1); Tick(chip,28); Assert.True(chip.Irq);
        chip.Write(9,0); Tick(chip,4); Assert.True(chip.Irq);
        chip.Write(14,0); Assert.False(chip.Irq);
    }
    [Theory][InlineData(7159092)][InlineData(7093788)]
    public void CartridgeOscillatorUsesRationalMasterClockRatio(int master)
    {
        var cart=new PokeyCartridge(new byte[16384]); var chip=Assert.IsType<Pokey>(cart.PokeySocket.Chip);
        for(int i=0;i<20000;i++) cart.TickMasterClock(master);
        long expected=20000L*cart.OscillatorHz/master;
        Assert.Equal(expected,cart.OscillatorTicks); Assert.Equal(expected,chip.Clocks);
        cart.Reset(); Assert.Equal(0,cart.OscillatorTicks); Assert.Equal(0,chip.Clocks);
    }
}
