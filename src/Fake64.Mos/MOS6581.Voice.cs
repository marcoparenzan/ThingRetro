namespace Fake64.Mos;

public partial class MOS6581
{
    // Bit del registro di controllo di una voce ($04, $0B, $12)
    const byte GATE = 0x01;
    const byte SYNC = 0x02;
    const byte RING_MOD = 0x04;
    const byte TEST = 0x08;
    const byte TRIANGLE = 0x10;
    const byte SAWTOOTH = 0x20;
    const byte PULSE = 0x40;
    const byte NOISE = 0x80;

    // Cicli fra un passo e l'altro del contatore d'inviluppo per i 16 valori di attacco, decadimento e rilascio
    // (dalla misura del chip: attacco 0 = 9 cicli x 255 passi = 2 ms, attacco 15 = 31251 x 255 = 8 s)
    static readonly int[] RatePeriods = [9, 32, 63, 95, 149, 220, 267, 313, 392, 977, 1954, 3126, 3907, 11720, 19532, 31251];

    enum EnvelopePhase { Attack, DecaySustain, Release }

    // Una voce: oscillatore a 24 bit, generatori di forma d'onda (12 bit) e generatore d'inviluppo (8 bit).
    // L'uscita è il prodotto dei due, come nel moltiplicatore analogico del chip
    sealed class Voice
    {
        public Voice SyncSource = null!;   // la voce precedente: 3 per la 1, 1 per la 2, 2 per la 3

        // Registri
        public int Frequency;              // $00-$01: incremento dell'accumulatore a ogni ciclo
        public int PulseWidth;             // $02-$03: 12 bit
        public byte Control;               // $04
        public int Attack, Decay;          // $05
        public int Sustain, Release;       // $06

        // Oscillatore: la frequenza è Frequency x clock / 2^24 (sul C64 PAL Frequency = Hz x 17,03)
        public int Accumulator;
        public bool MsbRising;             // il bit 23 è passato da 0 a 1 in questo ciclo: sincronizza la voce successiva
        int noise = 0x7FFFF8;              // registro a scorrimento a 23 bit del rumore

        // Inviluppo
        public int Envelope;               // 0-255, leggibile per la voce 3 in $1C
        EnvelopePhase phase = EnvelopePhase.Release;
        int rateCounter;                   // contatore a 15 bit confrontato con il periodo della fase
        int exponentialCounter;
        int exponentialPeriod = 1;
        bool holdZero = true;              // a zero il contatore si ferma fino al prossimo attacco

        public void Reset()
        {
            Frequency = PulseWidth = Attack = Decay = Sustain = Release = 0;
            Control = 0;
            Accumulator = 0;
            MsbRising = false;
            noise = 0x7FFFF8;
            Envelope = 0;
            phase = EnvelopePhase.Release;
            rateCounter = exponentialCounter = 0;
            exponentialPeriod = 1;
            holdZero = true;
        }

        public void WriteControl(byte value)
        {
            bool gateWas = (Control & GATE) != 0, gate = (value & GATE) != 0;
            if (!gateWas && gate)
            {
                // Fronte di salita del GATE: attacco, dal livello in cui si trova l'inviluppo
                phase = EnvelopePhase.Attack;
                holdZero = false;
            }
            else if (gateWas && !gate)
            {
                phase = EnvelopePhase.Release;
            }

            // TEST tiene fermo l'oscillatore a zero e riempie di 1 il registro del rumore
            if ((value & TEST) != 0)
            {
                Accumulator = 0;
                noise = 0x7FFFF8;
            }
            Control = value;
        }

        public void ClockOscillator()
        {
            if ((Control & TEST) != 0)
            {
                MsbRising = false;
                return;
            }

            int previous = Accumulator;
            Accumulator = (Accumulator + Frequency) & 0xFFFFFF;
            MsbRising = (previous & 0x800000) == 0 && (Accumulator & 0x800000) != 0;

            // Il rumore avanza quando il bit 19 dell'accumulatore passa da 0 a 1: segue la frequenza della voce
            if ((previous & 0x080000) == 0 && (Accumulator & 0x080000) != 0)
            {
                int feedback = ((noise >> 22) ^ (noise >> 17)) & 1;
                noise = ((noise << 1) | feedback) & 0x7FFFFF;
            }
        }

        // SYNC: quando l'oscillatore sorgente ricomincia il ciclo, questo riparte da zero
        public void ApplySync()
        {
            if ((Control & SYNC) != 0 && SyncSource.MsbRising) Accumulator = 0;
        }

        // Uscita a 12 bit del generatore di forme d'onda. Più forme insieme danno l'AND dei bit
        // (un'approssimazione: sul chip vero le combinazioni sono più deboli e meno regolari)
        public int Waveform()
        {
            if ((Control & 0xF0) == 0) return 0;

            int output = 0xFFF;
            if ((Control & TRIANGLE) != 0) output &= Triangle();
            if ((Control & SAWTOOTH) != 0) output &= Accumulator >> 12;
            if ((Control & PULSE) != 0) output &= (Control & TEST) != 0 || (Accumulator >> 12) >= PulseWidth ? 0xFFF : 0;
            if ((Control & NOISE) != 0) output &= Noise();
            return output;
        }

        // Il triangolo è il dente di sega ripiegato con il bit 23; con RING_MOD il bit 23 è messo in XOR con quello
        // della voce sorgente, e il triangolo diventa la modulazione ad anello delle due frequenze
        int Triangle()
        {
            int msb = ((Control & RING_MOD) != 0 ? Accumulator ^ SyncSource.Accumulator : Accumulator) & 0x800000;
            return ((msb != 0 ? ~Accumulator : Accumulator) >> 11) & 0xFFF;
        }

        // Otto bit sparsi del registro a scorrimento diventano gli 8 bit alti dell'uscita
        int Noise() =>
            ((noise & 0x100000) >> 9) | ((noise & 0x040000) >> 8) | ((noise & 0x004000) >> 5) | ((noise & 0x000800) >> 3) |
            ((noise & 0x000200) >> 2) | ((noise & 0x000020) << 1) | ((noise & 0x000004) << 3) | ((noise & 0x000001) << 4);

        // Un ciclo del generatore d'inviluppo. Il contatore di cicli è a 15 bit e si confronta per uguaglianza
        // con il periodo: se il periodo scende sotto il valore già raggiunto, il contatore deve fare tutto il giro
        // (fino a 32768 cicli) prima del passo successivo. È il famoso "ADSR bug" del SID
        public void ClockEnvelope()
        {
            int period = phase switch
            {
                EnvelopePhase.Attack => RatePeriods[Attack],
                EnvelopePhase.DecaySustain => RatePeriods[Decay],
                _ => RatePeriods[Release],
            };

            rateCounter = (rateCounter + 1) & 0x7FFF;
            if (rateCounter != period) return;
            rateCounter = 0;

            if (phase == EnvelopePhase.Attack)
            {
                // Attacco lineare fino a 255, poi il decadimento
                exponentialCounter = 0;
                Envelope = (Envelope + 1) & 0xFF;
                if (Envelope == 0xFF) phase = EnvelopePhase.DecaySustain;
            }
            else
            {
                // Decadimento e rilascio sono "esponenziali": il passo si allunga man mano che il livello scende
                if (++exponentialCounter != exponentialPeriod) return;
                exponentialCounter = 0;
                if (holdZero) return;

                if (phase == EnvelopePhase.Release || Envelope != Sustain * 0x11)
                    Envelope = (Envelope - 1) & 0xFF;
            }

            exponentialPeriod = Envelope switch
            {
                0xFF => 1,
                0x5D => 2,
                0x36 => 4,
                0x1A => 8,
                0x0E => 16,
                0x06 => 30,
                0x00 => 1,
                _ => exponentialPeriod,
            };
            if (Envelope == 0) holdZero = true;
        }

        // Uscita della voce, prima del filtro. Sul 6581 lo zero della forma d'onda non è a metà scala ($800) ma
        // più in basso: per questo una voce con l'inviluppo che sale o scende produce anche uno spostamento continuo
        public int Output() => (Waveform() - 0x380) * Envelope;
    }
}
