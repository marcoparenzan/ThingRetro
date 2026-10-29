using ThingNES;

namespace ThingNES.Tests;

public class ApuTests
{
    static void Tick(Apu a, int count) { for (int i = 0; i < count; i++) a.Tick(); }
    static List<short> Samples(Apu a) { var samples = new List<short>(); a.SampleProduced += samples.Add; return samples; }
    static void Pulse(Apu a, int channel = 0, byte envelope = 0x9f, byte length = 0x08)
    {
        a.Write(0x4015, 3); ushort b = (ushort)(0x4000 + channel * 4);
        a.Write(b, envelope); a.Write((ushort)(b + 2), 80); a.Write((ushort)(b + 3), length);
    }
    [Fact] public void SampleRateIsStableAndDisabledApuSilent()
    {
        var a = new Apu(); var samples = Samples(a); Tick(a, Apu.CpuFrequency);
        Assert.Equal(Apu.SampleRate, samples.Count); Assert.All(samples, s => Assert.Equal(0, s));
    }
    [Theory] [InlineData(0)] [InlineData(1)]
    public void BothPulseChannelsOscillateAndDisableImmediately(int channel)
    {
        var a = new Apu(); var samples = Samples(a); Pulse(a, channel); Tick(a, 5000);
        Assert.Contains(samples, s => s > 0); Assert.Contains(samples, s => s == 0);
        a.Write(0x4015, 0); samples.Clear(); Tick(a, 1000); Assert.All(samples, s => Assert.Equal(0, s));
    }
    [Fact] public void LengthCounterExpiresAndHaltKeepsItAlive()
    {
        var a = new Apu(); Pulse(a, length: 0x18); // length table index 3: two half frames
        Assert.Equal(1, a.ReadStatus() & 1); Tick(a, 14913); Assert.Equal(1, a.ReadStatus() & 1);
        Tick(a, 14916); Assert.Equal(0, a.ReadStatus() & 1);
        var halt = new Apu(); Pulse(halt, envelope: 0xbf, length: 0x18); Tick(halt, 100000); Assert.Equal(1, halt.ReadStatus() & 1);
    }
    [Fact] public void EnvelopeDecaysAfterRestart()
    {
        var a = new Apu(); var samples = Samples(a); Pulse(a, envelope: 0x80);
        Tick(a, 7457); samples.Clear(); Tick(a, 7000); short loud = samples.Max();
        samples.Clear(); Tick(a, 160000); samples.Clear(); Tick(a, 7000);
        Assert.True(loud > 0); Assert.All(samples, s => Assert.Equal(0, s));
    }
    [Fact] public void TriangleRequiresLinearCounterAndProducesWave()
    {
        var a = new Apu(); var samples = Samples(a); a.Write(0x4015, 4); a.Write(0x4008, 0x81);
        a.Write(0x400a, 60); a.Write(0x400b, 8); Tick(a, 7457); samples.Clear(); Tick(a, 5000);
        Assert.True(samples.Distinct().Count() > 5); Assert.Equal(4, a.ReadStatus() & 4);
    }
    [Theory] [InlineData(0)] [InlineData(128)]
    public void NoiseLfsrProducesVariation(byte mode)
    {
        var a = new Apu(); var samples = Samples(a); a.Write(0x4015, 8); a.Write(0x400c, 0x3f);
        a.Write(0x400e, mode); a.Write(0x400f, 8); Tick(a, 8000);
        Assert.Contains(samples, s => s > 0); Assert.Contains(samples, s => s == 0);
        Assert.Equal(8, a.ReadStatus() & 8);
    }
    [Fact] public void FrameIrqClearsOnReadAndCanBeInhibited()
    {
        var a = new Apu(); Tick(a, 29829); Assert.True(a.Irq);
        Assert.Equal(64, a.ReadStatus() & 64); Assert.False(a.Irq);
        a.Write(0x4017, 64); Tick(a, 60000); Assert.False(a.Irq);
        a.Write(0x4017, 128); Tick(a, 80000); Assert.False(a.Irq);
    }
    [Fact] public void FiveStepWriteImmediatelyClocksLength()
    {
        var a = new Apu(); Pulse(a, length: 0x18); a.Write(0x4017, 128); a.Write(0x4017, 128);
        Assert.Equal(0, a.ReadStatus() & 1);
    }
    [Fact] public void SweepOverflowMutesPulse()
    {
        var a = new Apu(); var samples = Samples(a); Pulse(a);
        a.Write(0x4001, 0x81); a.Write(0x4002, 255); a.Write(0x4003, 0x0f); Tick(a, 8000);
        Assert.All(samples, s => Assert.Equal(0, s));
    }
    [Fact] public void BoardRoutesRegistersAndApuRunsDuringDma()
    {
        var rom = new byte[16400]; "NES\u001a"u8.CopyTo(rom); rom[4] = 1;
        var m = new NesMachine(Cartridge.Load(rom)); int count = 0; m.Apu.SampleProduced += _ => count++;
        m.Write(0x4015, 1); m.Write(0x4003, 8); Assert.Equal(1, m.Read(0x4015) & 1);
        m.Write(0x4014, 0); m.RunCycles(513); Assert.Equal(513L * Apu.SampleRate / Apu.CpuFrequency, count);
        m.Write(0x4017, 64); Assert.False(m.Apu.Irq);
    }
    [Fact] public void WaveWriterFinalizesSizesAndLeavesStreamOpen()
    {
        using var stream = new MemoryStream();
        var wave = new WaveWriter(stream); wave.WriteSample(-123); wave.WriteSample(456); wave.Dispose(); wave.Dispose();
        var data = stream.ToArray(); Assert.Equal(48, data.Length);
        Assert.True(data.AsSpan(0, 4).SequenceEqual("RIFF"u8)); Assert.Equal(40u, BitConverter.ToUInt32(data, 4));
        Assert.Equal(4u, BitConverter.ToUInt32(data, 40)); Assert.Equal(-123, BitConverter.ToInt16(data, 44));
        Assert.True(stream.CanWrite); Assert.Throws<ObjectDisposedException>(() => wave.WriteSample(1));
    }
}
