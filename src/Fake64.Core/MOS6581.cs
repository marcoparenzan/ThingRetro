using Fake64;
using System;
using System.Drawing;
using System.Drawing.Imaging;

public class MOS6581
{
    Board board;

    // SID registers (25 bytes total)
    private const int REG_COUNT = 0x1F;

    // Voice registers for 3 voices (0, 1, 2)
    private const int VOICE_OFFSET = 7;

    // Voice register offsets
    private const int FREQ_LO = 0;        // Frequency control (low byte)
    private const int FREQ_HI = 1;        // Frequency control (high byte)
    private const int PW_LO = 2;          // Pulse width (low byte)
    private const int PW_HI = 3;          // Pulse width (high byte)
    private const int CONTROL = 4;        // Control register
    private const int ATTACK_DECAY = 5;   // Attack/decay register
    private const int SUSTAIN_RELEASE = 6; // Sustain/release register

    // Control register bits
    private const byte GATE = 0x01;
    private const byte SYNC = 0x02;
    private const byte RING_MOD = 0x04;
    private const byte TEST = 0x08;
    private const byte TRIANGLE = 0x10;
    private const byte SAWTOOTH = 0x20;
    private const byte PULSE = 0x40;
    private const byte NOISE = 0x80;

    // Filter/volume registers
    private const int FILTER_FC_LO = 0x15;  // Filter cutoff frequency (low byte)
    private const int FILTER_FC_HI = 0x16;  // Filter cutoff frequency (high byte)
    private const int FILTER_RES_FILT = 0x17; // Filter resonance and routing
    private const int FILTER_MODE_VOL = 0x18; // Filter mode and volume

    // Voice state for sound generation
    private class Voice
    {
        public int Phase;         // Current phase of oscillator
        public int PhaseIncrement; // Per-cycle phase increment
        public int PulseWidth;     // Current pulse width value
        public byte Control;       // Current control register value
        public byte AttackDecay;   // Current attack/decay register
        public byte SustainRelease; // Current sustain/release register
        public int EnvelopeValue;  // Current ADSR envelope value (0-255)
        public int EnvelopeState;  // 0=release, 1=attack, 2=decay, 3=sustain
        public int EnvelopeCounter; // Counter for envelope timing
    }

    private Voice[] voices = new Voice[3];
    private byte[] registers = new byte[REG_COUNT];
    private int cycleCount;

    // Audio output values
    private int[] outputBuffer;
    private int outputIndex;
    private int sampleRate;
    private int cyclesPerSample;

    public MOS6581(Board board)
    {
        this.board = board;

        // Initialize voices
        for (int i = 0; i < 3; i++)
        {
            voices[i] = new Voice();
        }

        // Initialize audio buffer
        sampleRate = 44100; // Default sample rate
        cyclesPerSample = 985248 / sampleRate; // C64 clock rate / sample rate
        outputBuffer = new int[sampleRate / 60]; // 1/60th second buffer

        Reset();
    }

    public void Reset()
    {
        // Clear all registers
        Array.Clear(registers, 0, registers.Length);

        // Reset voice states
        for (int i = 0; i < 3; i++)
        {
            voices[i].Phase = 0;
            voices[i].PhaseIncrement = 0;
            voices[i].PulseWidth = 0;
            voices[i].Control = 0;
            voices[i].AttackDecay = 0;
            voices[i].SustainRelease = 0;
            voices[i].EnvelopeValue = 0;
            voices[i].EnvelopeState = 0;
            voices[i].EnvelopeCounter = 0;
        }

        cycleCount = 0;
        outputIndex = 0;
    }

    public void Clock()
    {
        cycleCount++;

        // Update voices every cycle
        UpdateVoices();

        // Generate audio sample every N cycles
        if (cycleCount >= cyclesPerSample)
        {
            cycleCount = 0;
            GenerateSample();
        }
    }

    private void UpdateVoices()
    {
        for (int v = 0; v < 3; v++)
        {
            Voice voice = voices[v];

            // Update frequency
            int offset = v * VOICE_OFFSET;
            int freq = registers[offset + FREQ_LO] | (registers[offset + FREQ_HI] << 8);
            voice.PhaseIncrement = freq;

            // Update pulse width
            voice.PulseWidth = (registers[offset + PW_LO] | ((registers[offset + PW_HI] & 0x0F) << 8)) << 12;

            // Update control register and check for GATE bit changes
            byte newControl = registers[offset + CONTROL];
            bool gateOn = (newControl & GATE) != 0;
            bool oldGateOn = (voice.Control & GATE) != 0;

            if (gateOn && !oldGateOn)
            {
                // Gate turned on - start attack
                voice.EnvelopeState = 1; // Attack
            }
            else if (!gateOn && oldGateOn)
            {
                // Gate turned off - start release
                voice.EnvelopeState = 0; // Release
            }

            voice.Control = newControl;
            voice.AttackDecay = registers[offset + ATTACK_DECAY];
            voice.SustainRelease = registers[offset + SUSTAIN_RELEASE];

            // Update oscillator phase
            voice.Phase = (voice.Phase + voice.PhaseIncrement) & 0xFFFFFF;

            // Update envelope
            UpdateEnvelope(voice);
        }
    }

    private void UpdateEnvelope(Voice voice)
    {
        // Simple ADSR envelope processing
        // This is a simplified version - a full implementation would require more complex timing
        voice.EnvelopeCounter++;

        int rate = 0;
        switch (voice.EnvelopeState)
        {
            case 0: // Release
                rate = voice.SustainRelease & 0x0F;
                if (voice.EnvelopeCounter >= rate * 8)
                {
                    voice.EnvelopeCounter = 0;
                    if (voice.EnvelopeValue > 0) voice.EnvelopeValue--;
                }
                break;

            case 1: // Attack
                rate = (voice.AttackDecay >> 4) & 0x0F;
                if (voice.EnvelopeCounter >= rate * 8)
                {
                    voice.EnvelopeCounter = 0;
                    if (voice.EnvelopeValue < 255) voice.EnvelopeValue++;
                    if (voice.EnvelopeValue >= 255)
                    {
                        voice.EnvelopeValue = 255;
                        voice.EnvelopeState = 2; // Move to decay
                    }
                }
                break;

            case 2: // Decay
                rate = voice.AttackDecay & 0x0F;
                int sustain = ((voice.SustainRelease >> 4) & 0x0F) * 17;
                if (voice.EnvelopeCounter >= rate * 8)
                {
                    voice.EnvelopeCounter = 0;
                    if (voice.EnvelopeValue > sustain) voice.EnvelopeValue--;
                    if (voice.EnvelopeValue <= sustain)
                    {
                        voice.EnvelopeValue = sustain;
                        voice.EnvelopeState = 3; // Move to sustain
                    }
                }
                break;

            case 3: // Sustain
                // Maintain current level
                break;
        }
    }

    private void GenerateSample()
    {
        // Generate waveforms for each voice
        int output = 0;

        for (int v = 0; v < 3; v++)
        {
            Voice voice = voices[v];
            int sample = 0;

            // Generate waveform based on control register
            if ((voice.Control & TRIANGLE) != 0)
            {
                sample = GenerateTriangle(voice);
            }
            else if ((voice.Control & SAWTOOTH) != 0)
            {
                sample = GenerateSawtooth(voice);
            }
            else if ((voice.Control & PULSE) != 0)
            {
                sample = GeneratePulse(voice);
            }
            else if ((voice.Control & NOISE) != 0)
            {
                sample = GenerateNoise(voice);
            }

            // Apply envelope
            sample = (sample * voice.EnvelopeValue) >> 8;

            // Mix voice output
            output += sample;
        }

        // Apply volume control
        int volume = registers[FILTER_MODE_VOL] & 0x0F;
        output = (output * volume) >> 4;

        // Store in output buffer
        if (outputIndex < outputBuffer.Length)
        {
            outputBuffer[outputIndex++] = output;
        }
    }

    private int GenerateTriangle(Voice voice)
    {
        // Triangle wave generation
        int phase = voice.Phase >> 11; // 0-8191
        if ((phase & 0x800) != 0)
            return (~phase) & 0x7FF;
        return phase & 0x7FF;
    }

    private int GenerateSawtooth(Voice voice)
    {
        // Sawtooth wave generation
        return (voice.Phase >> 12) & 0xFFF;
    }

    private int GeneratePulse(Voice voice)
    {
        // Pulse wave generation with variable pulse width
        return (voice.Phase > voice.PulseWidth) ? 0xFFF : 0x000;
    }

    private int GenerateNoise(Voice voice)
    {
        // Simple noise generation - for a proper implementation,
        // this should use a linear feedback shift register
        return (int)((voice.Phase * 13) ^ (voice.Phase * 7)) & 0xFFF;
    }

    // Original memory access functions
    byte[] bytes = new byte[0x0400];

    public byte Address(ushort addr)
    {
        // Handle special read registers (random values for oscillators, etc.)
        if (addr == 0x1B) // Voice 3 oscillator
            return (byte)(voices[2].Phase >> 16);
        if (addr == 0x1C) // Voice 3 envelope
            return (byte)voices[2].EnvelopeValue;
        return registers[addr];
    }

    public void Address(ushort addr, byte value)
    {
        int regIndex = addr - 0xD400;
        registers[regIndex] = value;
    }

    // Get audio data for playback
    public int[] GetAudioData()
    {
        int[] result = new int[outputIndex];
        Array.Copy(outputBuffer, result, outputIndex);
        outputIndex = 0;
        return result;
    }
}