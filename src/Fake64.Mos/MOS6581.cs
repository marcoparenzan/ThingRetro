namespace Fake64.Mos;

// MOS 6581 SID: tre voci (oscillatore, forme d'onda, inviluppo), un filtro multimodo e il volume generale.
// Tutto avanza di un passo a ogni ciclo di φ2 (985248 Hz sul C64 PAL). AUDIO OUT è il livello istantaneo
// dell'uscita: la scheda lo campiona a ogni ciclo e lo manda alla presa audio (vedi Board.AudioOut).
// Il modello è digitale come quello di reSID per oscillatori e inviluppi; filtro e mixer, che sul chip sono
// analogici, sono approssimati (curva del filtro dal datasheet, combinazioni di forme d'onda come AND)
public partial class MOS6581 : I6581Pinout
{
    const int CLOCK_HZ = 985248;

    // Registri del filtro e del volume
    const int FC_LO = 0x15;         // bit 0-2 della frequenza di taglio (11 bit)
    const int FC_HI = 0x16;         // bit 3-10
    const int RES_FILT = 0x17;      // bit 4-7 risonanza, bit 0-2 voci nel filtro, bit 3 ingresso esterno
    const int MODE_VOL = 0x18;      // bit 4 passa basso, 5 passa banda, 6 passa alto, 7 voce 3 spenta; bit 0-3 volume

    readonly Voice[] voices = [new(), new(), new()];

    int cutoff;                     // 0-2047
    int resonance;                  // 0-15
    int filterRouting;              // bit 0-2: voci che passano dal filtro
    byte modeVolume;
    byte busValue;                  // ultimo valore scritto: i registri di sola scrittura rileggono il bus

    // Filtro a variabili di stato (passa alto, banda, basso) aggiornato a ogni ciclo
    double w0, inverseQ;
    double highPass, bandPass, lowPass;

    // Il mixer del 6581 ha una componente continua: il volume la scala insieme alle voci, quindi anche senza
    // voci attive scrivere $D418 sposta l'uscita. È così che i giochi suonano campioni ("digi") a 4 bit
    const double MIXER_DC = 0.5;

    // Scala delle voci: una forma d'onda a piena ampiezza e inviluppo 255 vale circa 1
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

    // AUDIO OUT (pin 27): livello dell'uscita all'ultimo ciclo, circa fra -1 e 1 per ogni voce a pieno volume
    public double AudioOutput { get; private set; }

    // POT X / POT Y: il SID scarica e ricarica un condensatore attraverso la resistenza esterna e conta
    // i cicli; il risultato (0-255) è disponibile in $19 / $1A e si aggiorna ogni 512 cicli
    public Func<byte> PotXInput { get; set; } = () => 0xFF;
    public Func<byte> PotYInput { get; set; } = () => 0xFF;
    const int POT_MEASUREMENT_CYCLES = 512;
    byte potX = 0xFF, potY = 0xFF;
    int potCycle;

    // φ2: un ciclo di oscillatori, inviluppi, filtro e mixer
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
            else if (v != 2 || (modeVolume & 0x80) == 0) direct += output; // "3 OFF" spegne la voce 3 se non è filtrata
        }

        AudioOutput = (direct + Filter(filterInput) + MIXER_DC) * (modeVolume & 0x0F) / 15.0;

        if (++potCycle == POT_MEASUREMENT_CYCLES)
        {
            potCycle = 0;
            potX = PotXInput();
            potY = PotYInput();
        }
    }

    // Un passo del filtro a variabili di stato (come in reSID). Le uscite sono invertite, come sul chip
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

    // Frequenza di taglio dal datasheet: circa 30 Hz + 5,8 Hz per passo (30 Hz - 12 kHz). Sul 6581 la curva reale
    // varia molto da un esemplare all'altro e non è lineare
    void UpdateFilter()
    {
        double hertz = 30 + 5.8 * cutoff;
        w0 = 2 * Math.PI * hertz / CLOCK_HZ;
        inverseQ = 1 / (0.707 + resonance / 15.0);
    }

    // A0-A4: i 32 registri si ripetono in tutta l'area selezionata da /CS. Solo $19-$1C si leggono
    public byte Read(int register) => (register & 0x1F) switch
    {
        0x19 => potX,
        0x1A => potY,
        0x1B => (byte)(voices[2].Waveform() >> 4),   // OSC3: gli 8 bit alti della forma d'onda della voce 3
        0x1C => (byte)voices[2].Envelope,             // ENV3: l'inviluppo della voce 3
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
