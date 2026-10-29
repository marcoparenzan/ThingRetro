using ThingNES;

namespace ThingNES.Tests;

public class DmcTests
{
    static void Tick(Dmc d, int count) { for (int i = 0; i < count; i++) d.Tick(); }
    [Fact] public void RegisterAddressLengthAndWrapAreCorrect()
    {
        var d = new Dmc(); d.Write(0, 15); d.Write(2, 255); d.Write(3, 4); d.SetEnabled(true);
        var addresses = new List<ushort>();
        for (int i = 0; i < 100000 && (d.Active || addresses.Count == 0); i++)
        {
            if (d.DmaAddress is ushort a) { addresses.Add(a); d.SupplySample(0); }
            d.Tick();
        }
        Assert.Equal(65, addresses.Count); Assert.Equal(0xffc0, addresses[0]);
        Assert.Equal(0xffff, addresses[63]); Assert.Equal(0x8000, addresses[64]); Assert.False(d.Active);
    }
    [Fact] public void OneByteSampleSetsIrqOnFetchAndReadStatusDoesNotClearIt()
    {
        var a = new Apu(); a.Write(0x4010, 128); a.Write(0x4015, 16);
        Assert.Equal(16, a.ReadStatus() & 16); Assert.Equal((ushort)0xc000, a.Dmc.DmaAddress);
        a.Dmc.SupplySample(255); Assert.Equal(128, a.ReadStatus() & 0x90); Assert.True(a.Irq);
        Assert.Equal(128, a.ReadStatus() & 128); a.Write(0x4017, 64); Assert.True(a.Irq);
        a.Write(0x4015, 0); Assert.False(a.Irq);
    }
    [Fact] public void LoopRestartsWithoutIrq()
    {
        var d = new Dmc(); d.Write(0, 0xcf); d.Write(2, 2); d.SetEnabled(true);
        d.SupplySample(255); Assert.True(d.Active); Assert.False(d.Irq);
        Tick(d, 8 * 54); Assert.Equal((ushort)0xc080, d.DmaAddress);
    }
    [Theory] [InlineData(255, 64, 80)] [InlineData(0, 64, 48)] [InlineData(255, 127, 127)] [InlineData(0, 0, 0)]
    public void OutputConsumesLeastSignificantBitsAndSaturates(byte data, byte start, int expected)
    {
        var d = new Dmc(); d.Write(0, 15); d.Write(1, start); d.SetEnabled(true); d.SupplySample(data);
        Tick(d, 16 * 54); Assert.Equal(expected, d.Output);
    }
    [Fact] public void DisabledDacHoldsDirectLoadAndMasksToSevenBits()
    {
        var d = new Dmc(); d.Write(1, 255); d.SetEnabled(false); Tick(d, 10000);
        Assert.Equal(127, d.Output); Assert.Null(d.DmaAddress);
        Assert.Throws<InvalidOperationException>(() => d.SupplySample(0));
    }
    [Theory] [InlineData(0, 428)] [InlineData(13, 84)] [InlineData(15, 54)]
    public void RateControlsBitClock(int rate, int period)
    {
        var d = new Dmc(); d.Write(0, (byte)rate); d.Write(1, 64); d.SetEnabled(true); d.SupplySample(255);
        Tick(d, 8 * period); Assert.Equal(64, d.Output); Tick(d, 1); Assert.Equal(66, d.Output);
    }
    [Fact] public void DisablingIrqViaControlClearsFlag()
    {
        var d = new Dmc(); d.Write(0, 128); d.SetEnabled(true); d.SupplySample(0); Assert.True(d.Irq);
        d.Write(0, 0); Assert.False(d.Irq);
    }
    [Fact] public void BoardFetchStallsCpuFourCyclesButClocksPpuAndApu()
    {
        var rom = new byte[16400]; "NES\u001a"u8.CopyTo(rom); rom[4] = 1; rom[16] = 255;
        var m = new NesMachine(Cartridge.Load(rom)); m.Write(0x4010, 128); m.Write(0x4015, 16);
        m.RunCycles(3); Assert.True(m.DmcDmaActive); Assert.True(m.Apu.Dmc.Active);
        m.Tick(); Assert.False(m.DmcDmaActive); Assert.Equal(4, m.DmcDmaCycles);
        Assert.Equal(12, m.Ppu.Dot); Assert.Equal(128, m.Read(0x4015) & 128);
    }
    [Fact] public void DmcDirectLoadContributesToMixerWithoutPlayback()
    {
        var a = new Apu(); var samples = new List<short>(); a.SampleProduced += samples.Add;
        a.Write(0x4011, 64); for (int i = 0; i < 1000; i++) a.Tick();
        Assert.NotEmpty(samples); Assert.All(samples, s => Assert.True(s > 0));
    }
    [Fact] public void OamTransferSurvivesDmcOverlapInFunctionalArbiter()
    {
        var rom = new byte[16400]; "NES\u001a"u8.CopyTo(rom); rom[4] = 1;
        var m = new NesMachine(Cartridge.Load(rom));
        for (int i = 0; i < 256; i++) m.Write((ushort)(0x200 + i), (byte)i);
        m.Write(0x4014, 2); m.Write(0x4015, 16); m.RunCycles(517);
        Assert.False(m.DmaActive); Assert.Equal(4, m.DmcDmaCycles);
        for (int i = 0; i < 256; i++) Assert.Equal((byte)i, m.Ppu.Oam[i]);
    }
}
