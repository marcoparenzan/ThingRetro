using static Fake64.Tests.TestHelpers;

namespace Fake64.Tests;

// Il SID: oscillatori, forme d'onda e inviluppi letti attraverso OSC3/ENV3 come fanno i programmi del C64,
// poi la catena audio completa fino a un altoparlante
[TestClass]
public sealed class SidTests
{
    const long CYCLES_PER_SECOND = 985248;

    // Registri della voce 3 ($D40E-$D414), letti in $D41B (OSC3) e $D41C (ENV3)
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

    #region Oscillatori e forme d'onda

    [TestMethod]
    public void Sawtooth_ShouldFollowTheAccumulator()
    {
        var sid = new MOS6581();
        Frequency(sid, 2, 0x1000);
        sid.Write(V3_CONTROL, 0x20);

        Tick(sid, 256);   // accumulatore = 256 x $1000 = $100000: il dente di sega vale $100

        Assert.AreEqual(0x10, (int)sid.Read(OSC3));
    }

    [TestMethod]
    public void Triangle_ShouldRiseThenFall()
    {
        var sid = new MOS6581();
        Frequency(sid, 2, 0x4000);
        sid.Write(V3_CONTROL, 0x10);

        Tick(sid, 256);   // un quarto di periodo: a metà della salita
        Assert.AreEqual(0x80, (int)sid.Read(OSC3));
        Tick(sid, 512);   // tre quarti: a metà della discesa
        Assert.AreEqual(0x7F, (int)sid.Read(OSC3));
    }

    [TestMethod]
    public void Pulse_ShouldCompareTheAccumulatorWithThePulseWidth()
    {
        var sid = new MOS6581();
        Frequency(sid, 2, 0x4000);
        sid.Write(V3_PW_LO, 0x00);
        sid.Write(V3_PW_HI, 0x0C);   // 75%: basso per i primi tre quarti del periodo
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
        // Lo stesso registro a scorrimento dà sempre la stessa sequenza: due SID uguali, due sequenze uguali
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
        Assert.IsTrue(first.Distinct().Count() > 32, "il rumore non cambia abbastanza");
    }

    [TestMethod]
    public void Sync_ShouldRestartTheOscillatorWithTheSource()
    {
        // La voce 3 è sincronizzata dalla voce 2, che ricomincia ogni 512 cicli: la voce 3 non supera $20000
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
        // Voce 3 ferma (frequenza 0): il suo triangolo con la modulazione ad anello segue solo il bit 23 della voce 2
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

    #region Inviluppo

    [TestMethod]
    public void Attack_ShouldReachTheTopIn255Steps()
    {
        // Attacco 0: un passo ogni 9 cicli, 255 passi = 2295 cicli (circa 2 ms)
        var sid = new MOS6581();
        sid.Write(V3_AD, 0x00);
        sid.Write(V3_SR, 0xF0);
        sid.Write(V3_CONTROL, 0x01);

        Tick(sid, 2294);
        Assert.AreEqual(254, (int)sid.Read(ENV3));
        Tick(sid, 1);
        Assert.AreEqual(255, (int)sid.Read(ENV3));
        Tick(sid, 10000);
        Assert.AreEqual(255, (int)sid.Read(ENV3)); // sostegno 15 = $FF
    }

    [TestMethod]
    public void Decay_ShouldStopAtTheSustainLevel()
    {
        var sid = new MOS6581();
        sid.Write(V3_AD, 0x00);
        sid.Write(V3_SR, 0x80);   // sostegno 8 = $88
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

        // Da $88 a 0 con rilascio 0: 637 passi (più lunghi man mano che il livello scende) di 9 cicli
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
        // Attacco 15 (periodo 31251), poi dopo 1000 cicli attacco 0 (periodo 9): il contatore è già a 1000,
        // quindi deve arrivare a 32767, ripartire da 0 e raggiungere 9 prima del primo passo
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

    #region Uscita audio

    // Un altoparlante che registra quello che riceve
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

    // Voce 1: frequenza in Hz (registro = Hz x 2^24 / clock), forma d'onda, attacco immediato, sostegno massimo
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
        PlayVoice1(board, 440, 0x10);   // la a 440 Hz, triangolo

        board.RunCycles(CYCLES_PER_SECOND);

        // Mezzo secondo, lasciando assestare il passa alto d'uscita: 220 passaggi per lo zero in salita
        var half = speaker.Samples.Skip(22050).ToArray();
        int crossings = Enumerable.Range(1, half.Length - 1).Count(i => half[i - 1] < 0 && half[i] >= 0);
        Assert.IsTrue(crossings is >= 218 and <= 222, $"passaggi per lo zero: {crossings}");
        Assert.IsTrue(half.Max() > 5000, "volume troppo basso");
    }

    [TestMethod]
    public void Silence_WithoutGateOrVolume()
    {
        var (board, speaker) = Connected();
        board.Poke(0xD418, 0x0F);
        int value = 7493;
        board.Poke(0xD400, (byte)value, (byte)(value >> 8));
        board.Poke(0xD404, 0x10);       // triangolo, senza GATE: inviluppo a zero

        board.RunCycles(CYCLES_PER_SECOND);

        Assert.IsTrue(speaker.Samples.Skip(22050).All(s => Math.Abs((int)s) < 50));
    }

    [TestMethod]
    public void VolumeWrites_ShouldPlayDigis()
    {
        // Senza voci attive: scrivere il volume sposta l'uscita del mixer (la sua componente continua).
        // Alternando 0 e 15 a 1 kHz si ottiene un'onda quadra, come fanno i giochi con i campioni a 4 bit
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
            board.Poke(0xD415, 0x00, 0x00);                  // frequenza di taglio minima: circa 30 Hz
            board.Poke(0xD417, (byte)(filtered ? 0x01 : 0x00)); // voce 1 nel filtro
            board.Poke(0xD418, 0x1F);                        // passa basso, volume 15
            PlayVoice1(board, 4000, 0x20);                   // dente di sega a 4 kHz
            board.RunCycles(CYCLES_PER_SECOND / 2);
            return Rms(speaker.Samples.Skip(11025));
        }

        double direct = Level(false), filtered = Level(true);
        Assert.IsTrue(filtered < direct / 10, $"diretto {direct:F0}, filtrato {filtered:F0}");
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

        Assert.AreEqual(9 * 441, count);   // arrivano solo blocchi completi da 10 ms
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
