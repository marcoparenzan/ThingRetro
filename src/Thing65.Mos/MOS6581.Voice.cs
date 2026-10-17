namespace Thing65.Mos;

public partial class MOS6581
{
    // Bits of a voice's control register ($04, $0B, $12)
    const byte GATE = 0x01;
    const byte SYNC = 0x02;
    const byte RING_MOD = 0x04;
    const byte TEST = 0x08;
    const byte TRIANGLE = 0x10;
    const byte SAWTOOTH = 0x20;
    const byte PULSE = 0x40;
    const byte NOISE = 0x80;

    // Cycles between two steps of the envelope counter for the 16 attack, decay and release values
    // (measured on the chip: attack 0 = 9 cycles x 255 steps = 2 ms, attack 15 = 31251 x 255 = 8 s)
    static readonly int[] RatePeriods = [9, 32, 63, 95, 149, 220, 267, 313, 392, 977, 1954, 3126, 3907, 11720, 19532, 31251];

    enum EnvelopePhase { Attack, DecaySustain, Release }

    // A voice: 24-bit oscillator, waveform generators (12 bits) and envelope generator (8 bits).
    // The output is the product of the two, as in the chip's analog multiplier
    sealed class Voice
    {
        public Voice SyncSource = null!;   // the previous voice: 3 for 1, 1 for 2, 2 for 3

        // Registers
        public int Frequency;              // $00-$01: accumulator increment at every cycle
        public int PulseWidth;             // $02-$03: 12 bit
        public byte Control;               // $04
        public int Attack, Decay;          // $05
        public int Sustain, Release;       // $06

        // Oscillator: the frequency is Frequency x clock / 2^24 (on the PAL C64 Frequency = Hz x 17.03)
        public int Accumulator;
        public bool MsbRising;             // bit 23 went from 0 to 1 in this cycle: syncs the next voice
        int noise = 0x7FFFF8;              // 23-bit noise shift register

        // Envelope
        public int Envelope;               // 0-255, readable for voice 3 in $1C
        EnvelopePhase phase = EnvelopePhase.Release;
        int rateCounter;                   // 15-bit counter compared with the period of the phase
        int exponentialCounter;
        int exponentialPeriod = 1;
        bool holdZero = true;              // at zero the counter stops until the next attack

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
                // Rising edge of GATE: attack, from the level the envelope is at
                phase = EnvelopePhase.Attack;
                holdZero = false;
            }
            else if (gateWas && !gate)
            {
                phase = EnvelopePhase.Release;
            }

            // TEST holds the oscillator at zero and fills the noise register with 1s
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

            // The noise advances when bit 19 of the accumulator goes from 0 to 1: it follows the voice's frequency
            if ((previous & 0x080000) == 0 && (Accumulator & 0x080000) != 0)
            {
                int feedback = ((noise >> 22) ^ (noise >> 17)) & 1;
                noise = ((noise << 1) | feedback) & 0x7FFFFF;
            }
        }

        // SYNC: when the source oscillator starts a new cycle, this one restarts from zero
        public void ApplySync()
        {
            if ((Control & SYNC) != 0 && SyncSource.MsbRising) Accumulator = 0;
        }

        // 12-bit output of the waveform generator. Several waveforms together give the AND of the bits
        // (an approximation: on the real chip the combinations are weaker and less regular)
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

        // The triangle is the sawtooth folded by bit 23; with RING_MOD bit 23 is XORed with the source voice's,
        // and the triangle becomes the ring modulation of the two frequencies
        int Triangle()
        {
            int msb = ((Control & RING_MOD) != 0 ? Accumulator ^ SyncSource.Accumulator : Accumulator) & 0x800000;
            return ((msb != 0 ? ~Accumulator : Accumulator) >> 11) & 0xFFF;
        }

        // Eight scattered bits of the shift register become the high 8 bits of the output
        int Noise() =>
            ((noise & 0x100000) >> 9) | ((noise & 0x040000) >> 8) | ((noise & 0x004000) >> 5) | ((noise & 0x000800) >> 3) |
            ((noise & 0x000200) >> 2) | ((noise & 0x000020) << 1) | ((noise & 0x000004) << 3) | ((noise & 0x000001) << 4);

        // One cycle of the envelope generator. The cycle counter has 15 bits and is compared for equality with
        // the period: if the period drops below the value already reached, the counter has to wrap all the way
        // around (up to 32768 cycles) before the next step. It is the SID's famous "ADSR bug"
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
                // Linear attack up to 255, then the decay
                exponentialCounter = 0;
                Envelope = (Envelope + 1) & 0xFF;
                if (Envelope == 0xFF) phase = EnvelopePhase.DecaySustain;
            }
            else
            {
                // Decay and release are "exponential": the step gets longer as the level drops
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

        // Output of the voice, before the filter. On the 6581 the zero of the waveform is not at mid-scale ($800)
        // but lower: that is why a voice whose envelope rises or falls also produces a DC shift
        public int Output() => (Waveform() - 0x380) * Envelope;
    }
}
