namespace Thing65.Mos;

// MOS 6581 SID: three voices (oscillator, waveforms, envelope), a multimode filter and the master volume.
// Everything advances one step at every φ2 cycle (985248 Hz on the PAL C64). AUDIO OUT is the instantaneous
// level of the output: the board samples it at every cycle and sends it to the audio socket (see Board.AudioOut).
// Oscillators and envelopes follow the digital model of reSID; filter and mixer, which are analog on the chip,
// are approximated (filter curve from the datasheet, combined waveforms as AND)
public partial class MOS6581 : I6581Pinout
{
    const int CLOCK_HZ = 985248;

    // Filter and volume registers
    const int FC_LO = 0x15;         // bits 0-2 of the cutoff frequency (11 bits)
    const int FC_HI = 0x16;         // bit 3-10
    const int RES_FILT = 0x17;      // bits 4-7 resonance, bits 0-2 voices into the filter, bit 3 external input
    const int MODE_VOL = 0x18;      // bit 4 low-pass, 5 band-pass, 6 high-pass, 7 voice 3 off; bits 0-3 volume

    readonly Voice[] voices = [new(), new(), new()];

    int cutoff;                     // 0-2047
    int resonance;                  // 0-15
    int filterRouting;              // bits 0-2: voices going through the filter
    byte modeVolume;
    byte busValue;                  // last value written: write-only registers read back the bus

    // State-variable filter (high-, band-, low-pass) updated at every cycle
    double w0, inverseQ;
    double highPass, bandPass, lowPass;

    // The 6581 mixer has a DC component: the volume scales it together with the voices, so writing $D418 moves
    // the output even with no voice playing. That is how games play 4-bit samples ("digis")
    const double MIXER_DC = 0.5;

    // Voice scale: a full-amplitude waveform with envelope 255 is about 1
    const double VOICE_SCALE = 1.0 / (0x800 * 255);

    public MOS6581()
    {
        voices[0].SyncSource = voices[2];
        voices[1].SyncSource = voices[0];
        voices[2].SyncSource = voices[1];
        Reset();
    }

    public void Reset()
    {
        foreach (var voice in voices) voice.Reset();
        cutoff = resonance = filterRouting = 0;
        modeVolume = 0;
        busValue = 0;
        highPass = bandPass = lowPass = 0;
        UpdateFilter();
        AudioOutput = 0;
        potCycle = 0;
        potX = potY = 0xFF;
    }

    // AUDIO OUT (pin 27): output level at the last cycle, about -1 to 1 for each voice at full volume
    public double AudioOutput { get; private set; }

    // POT X / POT Y: the SID discharges and recharges a capacitor through the external resistance and counts
    // the cycles; the result (0-255) is available in $19 / $1A and is updated every 512 cycles
    public Func<byte> PotXInput { get; set; } = () => 0xFF;
    public Func<byte> PotYInput { get; set; } = () => 0xFF;
    const int POT_MEASUREMENT_CYCLES = 512;
    byte potX = 0xFF, potY = 0xFF;
    int potCycle;

    // φ2: one cycle of oscillators, envelopes, filter and mixer
    public void Tick()
    {
        foreach (var voice in voices) voice.ClockOscillator();
        foreach (var voice in voices) voice.ApplySync();
        foreach (var voice in voices) voice.ClockEnvelope();

        double filterInput = 0, direct = 0;
        for (int v = 0; v < 3; v++)
        {
            double output = voices[v].Output() * VOICE_SCALE;
            if ((filterRouting & (1 << v)) != 0) filterInput += output;
            else if (v != 2 || (modeVolume & 0x80) == 0) direct += output; // "3 OFF" silences voice 3 when it is not filtered
        }

        AudioOutput = (direct + Filter(filterInput) + MIXER_DC) * (modeVolume & 0x0F) / 15.0;

        if (++potCycle == POT_MEASUREMENT_CYCLES)
        {
            potCycle = 0;
            potX = PotXInput();
            potY = PotYInput();
        }
    }

    // One step of the state-variable filter (as in reSID). The outputs are inverted, as on the chip
    double Filter(double input)
    {
        bandPass -= w0 * highPass;
        lowPass -= w0 * bandPass;
        highPass = bandPass * inverseQ - lowPass - input;

        double output = 0;
        if ((modeVolume & 0x10) != 0) output += lowPass;
        if ((modeVolume & 0x20) != 0) output += bandPass;
        if ((modeVolume & 0x40) != 0) output += highPass;
        return output;
    }

    // Cutoff frequency from the datasheet: about 30 Hz + 5.8 Hz per step (30 Hz - 12 kHz). On the 6581 the real
    // curve varies a lot from chip to chip and is not linear
    void UpdateFilter()
    {
        double hertz = 30 + 5.8 * cutoff;
        w0 = 2 * Math.PI * hertz / CLOCK_HZ;
        inverseQ = 1 / (0.707 + resonance / 15.0);
    }

    // A0-A4: the 32 registers repeat across the whole area selected by /CS. Only $19-$1C can be read
    public byte Read(int register) => (register & 0x1F) switch
    {
        0x19 => potX,
        0x1A => potY,
        0x1B => (byte)(voices[2].Waveform() >> 4),   // OSC3: the high 8 bits of voice 3's waveform
        0x1C => (byte)voices[2].Envelope,             // ENV3: voice 3's envelope
        _ => busValue,
    };

    public void Write(int register, byte value)
    {
        int address = register & 0x1F;
        busValue = value;

        if (address < 0x15)
        {
            var voice = voices[address / 7];
            switch (address % 7)
            {
                case 0: voice.Frequency = (voice.Frequency & 0xFF00) | value; break;
                case 1: voice.Frequency = (voice.Frequency & 0x00FF) | value << 8; break;
                case 2: voice.PulseWidth = (voice.PulseWidth & 0xF00) | value; break;
                case 3: voice.PulseWidth = (voice.PulseWidth & 0x0FF) | (value & 0x0F) << 8; break;
                case 4: voice.WriteControl(value); break;
                case 5: voice.Attack = value >> 4; voice.Decay = value & 0x0F; break;
                case 6: voice.Sustain = value >> 4; voice.Release = value & 0x0F; break;
            }
            return;
        }

        switch (address)
        {
            case FC_LO: cutoff = (cutoff & 0x7F8) | (value & 0x07); UpdateFilter(); break;
            case FC_HI: cutoff = (cutoff & 0x007) | value << 3; UpdateFilter(); break;
            case RES_FILT: resonance = value >> 4; filterRouting = value & 0x07; UpdateFilter(); break;
            case MODE_VOL: modeVolume = value; break;
        }
    }
}
