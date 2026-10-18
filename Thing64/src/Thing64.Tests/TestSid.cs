using static Thing64.Tests.TestHelpers;

namespace Thing64.Tests;

// The SID: oscillators, waveforms and envelopes read through OSC3/ENV3 as C64 programs do,
// then the whole audio chain up to a speaker
[TestClass]
public sealed class SidTests
{
    const long CYCLES_PER_SECOND = 985248;

    // Voice 3 registers ($D40E-$D414), read in $D41B (OSC3) and $D41C (ENV3)
    const int V3_FREQ_LO = 0x0E, V3_FREQ_HI = 0x0F, V3_PW_LO = 0x10, V3_PW_HI = 0x11, V3_CONTROL = 0x12, V3_AD = 0x13, V3_SR = 0x14;
    const int OSC3 = 0x1B, ENV3 = 0x1C;

    static void Tick(MOS6581 sid, int cycles)
    {
        for (int i = 0; i < cycles; i++) sid.Tick();
    }

    static void Frequency(MOS6581 sid, int voice, int value)
    {
        sid.Write(voice * 7, (byte)value);
        sid.Write(voice * 7 + 1, (byte)(value >> 8));
    }

    #region Oscillators and waveforms

    [TestMethod]
    public void Sawtooth_ShouldFollowTheAccumulator()
    {
        var sid = new MOS6581();
        Frequency(sid, 2, 0x1000);
        sid.Write(V3_CONTROL, 0x20);

        Tick(sid, 256);   // accumulator = 256 x $1000 = $100000: the sawtooth is $100

        Assert.AreEqual(0x10, (int)sid.Read(OSC3));
    }

    [TestMethod]
    public void Triangle_ShouldRiseThenFall()
    {
        var sid = new MOS6581();
        Frequency(sid, 2, 0x4000);
        sid.Write(V3_CONTROL, 0x10);

        Tick(sid, 256);   // a quarter of the period: halfway up
        Assert.AreEqual(0x80, (int)sid.Read(OSC3));
        Tick(sid, 512);   // three quarters: halfway down
        Assert.AreEqual(0x7F, (int)sid.Read(OSC3));
    }

    [TestMethod]
    public void Pulse_ShouldCompareTheAccumulatorWithThePulseWidth()
    {
        var sid = new MOS6581();
        Frequency(sid, 2, 0x4000);
        sid.Write(V3_PW_LO, 0x00);
        sid.Write(V3_PW_HI, 0x0C);   // 75%: low for the first three quarters of the period
        sid.Write(V3_CONTROL, 0x40);

        Tick(sid, 700);
        Assert.AreEqual(0x00, (int)sid.Read(OSC3));
        Tick(sid, 100);
        Assert.AreEqual(0xFF, (int)sid.Read(OSC3));
    }

    [TestMethod]
    public void TestBit_ShouldHoldTheOscillatorAtZero()
    {
        var sid = new MOS6581();
        Frequency(sid, 2, 0x1000);
        sid.Write(V3_CONTROL, 0x20);
        Tick(sid, 1000);

        sid.Write(V3_CONTROL, 0x28);
        Tick(sid, 1000);

        Assert.AreEqual(0, (int)sid.Read(OSC3));
    }

    [TestMethod]
    public void Noise_ShouldBeAPseudoRandomSequence()
    {
        // The same shift register always gives the same sequence: two equal SIDs, two equal sequences
        static List<byte> Sequence()
        {
            var sid = new MOS6581();
            Frequency(sid, 2, 0x8000);
            sid.Write(V3_CONTROL, 0x80);
            var values = new List<byte>();
            for (int i = 0; i < 64; i++)
            {
                Tick(sid, 64);
                values.Add(sid.Read(OSC3));
            }
            return values;
        }

        var first = Sequence();
        CollectionAssert.AreEqual(first, Sequence());
        Assert.IsTrue(first.Distinct().Count() > 32, "the noise does not change enough");
    }

    [TestMethod]
    public void Sync_ShouldRestartTheOscillatorWithTheSource()
    {
        // Voice 3 is synced by voice 2, which restarts every 512 cycles: voice 3 never goes past $20000
        var sid = new MOS6581();
        Frequency(sid, 1, 0x8000);
        Frequency(sid, 2, 0x0100);
        sid.Write(V3_CONTROL, 0x22);

        int highest = 0;
        for (int i = 0; i < 100; i++)
        {
            Tick(sid, 37);
            highest = Math.Max(highest, sid.Read(OSC3));
        }

        Assert.IsTrue(highest <= 0x02, $"OSC3 = {highest:X2}");
    }

    [TestMethod]
    public void RingModulation_ShouldFlipTheTriangleWithTheSource()
    {
        // Voice 3 still (frequency 0): its triangle with ring modulation follows only bit 23 of voice 2
        var sid = new MOS6581();
        Frequency(sid, 1, 0x8000);
        sid.Write(V3_CONTROL, 0x14);

        var values = new HashSet<byte>();
        for (int i = 0; i < 40; i++)
        {
            Tick(sid, 61);
            values.Add(sid.Read(OSC3));
        }

        CollectionAssert.AreEquivalent(new byte[] { 0x00, 0xFF }, values.ToArray());
    }

    #endregion

    #region Envelope

    [TestMethod]
    public void Attack_ShouldReachTheTopIn255Steps()
    {
        // Attack 0: one step every 9 cycles, 255 steps = 2295 cycles (about 2 ms)
        var sid = new MOS6581();
        sid.Write(V3_AD, 0x00);
        sid.Write(V3_SR, 0xF0);
        sid.Write(V3_CONTROL, 0x01);

        Tick(sid, 2294);
        Assert.AreEqual(254, (int)sid.Read(ENV3));
        Tick(sid, 1);
        Assert.AreEqual(255, (int)sid.Read(ENV3));
        Tick(sid, 10000);
        Assert.AreEqual(255, (int)sid.Read(ENV3)); // sustain 15 = $FF
    }

    [TestMethod]
    public void Decay_ShouldStopAtTheSustainLevel()
    {
        var sid = new MOS6581();
        sid.Write(V3_AD, 0x00);
        sid.Write(V3_SR, 0x80);   // sustain 8 = $88
        sid.Write(V3_CONTROL, 0x01);

        Tick(sid, 20000);

        Assert.AreEqual(0x88, (int)sid.Read(ENV3));
    }

    [TestMethod]
    public void Release_ShouldReachZeroAndStayThere()
    {
        var sid = new MOS6581();
        sid.Write(V3_AD, 0x00);
        sid.Write(V3_SR, 0x80);
        sid.Write(V3_CONTROL, 0x01);
        Tick(sid, 20000);

        // From $88 to 0 with release 0: 637 steps of 9 cycles (longer as the level drops)
        sid.Write(V3_CONTROL, 0x00);
        Tick(sid, 5700);
        Assert.AreNotEqual(0, (int)sid.Read(ENV3));
        Tick(sid, 100);
        Assert.AreEqual(0, (int)sid.Read(ENV3));
        Tick(sid, 50000);
        Assert.AreEqual(0, (int)sid.Read(ENV3));
    }

    [TestMethod]
    public void AdsrBug_ShortenedPeriodShouldWaitForTheCounterToWrap()
    {
        // Attack 15 (period 31251), then after 1000 cycles attack 0 (period 9): the counter is already at 1000,
        // so it has to reach 32767, wrap to 0 and get to 9 before the first step
        var sid = new MOS6581();
        sid.Write(V3_AD, 0xF0);
        sid.Write(V3_CONTROL, 0x01);
        Tick(sid, 1000);

        sid.Write(V3_AD, 0x00);
        Tick(sid, 31000);
        Assert.AreEqual(0, (int)sid.Read(ENV3));
        Tick(sid, 1000);
        Assert.IsTrue(sid.Read(ENV3) > 0);
    }

    #endregion

    #region Audio output

    // A speaker that records what it receives
    sealed class Recorder : ISpeaker
    {
        public readonly List<short> Samples = [];
        public int Blocks;
        public int SampleRate => 44100;

        public void Play(ReadOnlySpan<short> samples)
        {
            Samples.AddRange(samples.ToArray());
            Blocks++;
        }
    }

    static (Board Board, Recorder Speaker) Connected()
    {
        var board = NewBoard();
        board.ParkCpu();
        var speaker = new Recorder();
        _ = new AudioCable(board.AudioOut, speaker);
        return (board, speaker);
    }

    // Voice 1: frequency in Hz (register = Hz x 2^24 / clock), waveform, immediate attack, full sustain
    static void PlayVoice1(Board board, double hertz, byte waveform)
    {
        int value = (int)Math.Round(hertz * (1 << 24) / CYCLES_PER_SECOND);
        board.Poke(0xD400, (byte)value, (byte)(value >> 8));
        board.Poke(0xD405, 0x00, 0xF0);
        board.Poke(0xD404, (byte)(waveform | 0x01));
    }

    static double Rms(IEnumerable<short> samples) => Math.Sqrt(samples.Average(s => (double)s * s));

    [TestMethod]
    public void AudioPort_ShouldDeliver44100SamplesPerSecondIn10MsBlocks()
    {
        var (board, speaker) = Connected();

        board.RunCycles(CYCLES_PER_SECOND);

        Assert.AreEqual(44100, speaker.Samples.Count);
        Assert.AreEqual(100, speaker.Blocks);
    }

    [TestMethod]
    public void Tone_ShouldHaveTheProgrammedFrequency()
    {
        var (board, speaker) = Connected();
        board.Poke(0xD418, 0x0F);
        PlayVoice1(board, 440, 0x10);   // A at 440 Hz, triangle

        board.RunCycles(CYCLES_PER_SECOND);

        // Half a second, letting the output high-pass settle: 220 rising zero crossings
        var half = speaker.Samples.Skip(22050).ToArray();
        int crossings = Enumerable.Range(1, half.Length - 1).Count(i => half[i - 1] < 0 && half[i] >= 0);
        Assert.IsTrue(crossings is >= 218 and <= 222, $"zero crossings: {crossings}");
        Assert.IsTrue(half.Max() > 5000, "volume too low");
    }

    [TestMethod]
    public void Silence_WithoutGateOrVolume()
    {
        var (board, speaker) = Connected();
        board.Poke(0xD418, 0x0F);
        int value = 7493;
        board.Poke(0xD400, (byte)value, (byte)(value >> 8));
        board.Poke(0xD404, 0x10);       // triangle, without GATE: envelope at zero

        board.RunCycles(CYCLES_PER_SECOND);

        Assert.IsTrue(speaker.Samples.Skip(22050).All(s => Math.Abs((int)s) < 50));
    }

    [TestMethod]
    public void VolumeWrites_ShouldPlayDigis()
    {
        // With no voice playing: writing the volume moves the mixer output (its DC component).
        // Alternating 0 and 15 at 1 kHz gives a square wave, as games do with 4-bit samples
        var (board, speaker) = Connected();
        for (int i = 0; i < 1000; i++)
        {
            board.Poke(0xD418, (byte)(i % 2 == 0 ? 0x0F : 0x00));
            board.RunCycles(CYCLES_PER_SECOND / 2000);
        }

        Assert.IsTrue(Rms(speaker.Samples.Skip(4410)) > 2000, $"RMS {Rms(speaker.Samples.Skip(4410))}");
    }

    [TestMethod]
    public void LowPassFilter_ShouldAttenuateHighFrequencies()
    {
        double Level(bool filtered)
        {
            var (board, speaker) = Connected();
            board.Poke(0xD415, 0x00, 0x00);                  // lowest cutoff frequency: about 30 Hz
            board.Poke(0xD417, (byte)(filtered ? 0x01 : 0x00)); // voice 1 into the filter
            board.Poke(0xD418, 0x1F);                        // low-pass, volume 15
            PlayVoice1(board, 4000, 0x20);                   // 4 kHz sawtooth
            board.RunCycles(CYCLES_PER_SECOND / 2);
            return Rms(speaker.Samples.Skip(11025));
        }

        double direct = Level(false), filtered = Level(true);
        Assert.IsTrue(filtered < direct / 10, $"direct {direct:F0}, filtered {filtered:F0}");
    }

    [TestMethod]
    public void UnpluggedCable_ShouldStopTheSamples()
    {
        var board = NewBoard();
        var speaker = new Recorder();
        var cable = new AudioCable(board.AudioOut, speaker);
        board.RunCycles(CYCLES_PER_SECOND / 10);
        cable.Dispose();
        int count = speaker.Samples.Count;

        board.RunCycles(CYCLES_PER_SECOND / 10);

        Assert.AreEqual(9 * 441, count);   // only complete 10 ms blocks arrive
        Assert.AreEqual(count, speaker.Samples.Count);
        Assert.IsFalse(board.AudioOut.IsConnected);
        Assert.ThrowsException<InvalidOperationException>(() =>
        {
            _ = new AudioCable(board.AudioOut, new Recorder());
            _ = new AudioCable(board.AudioOut, new Recorder());
        });
    }

    #endregion
}
