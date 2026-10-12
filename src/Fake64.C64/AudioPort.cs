namespace Fake64.C64;

// Uscita audio della Board (il pin AUDIO della presa A/V). Riceve a ogni ciclo il livello di AUDIO OUT del SID e
// fa quello che fanno lo stadio d'uscita della scheda e la scheda audio del PC:
// - ricampiona alla frequenza dell'altoparlante con la media dei cicli di ogni campione (un passa basso);
// - toglie la componente continua come il condensatore di accoppiamento in uscita (passa alto a 16 Hz);
// - converte in campioni a 16 bit, inviati all'altoparlante a blocchi di 10 ms
public sealed class AudioPort
{
    const double HIGH_PASS_HZ = 16;
    const double GAIN = 0.3;            // tre voci a piena ampiezza restano entro la scala dei 16 bit

    readonly long clockHz;
    readonly object plugLock = new();
    volatile State? state;

    internal AudioPort(long clockHz)
    {
        this.clockHz = clockHz;
    }

    public bool IsConnected => state is not null;

    internal void Plug(ISpeaker speaker)
    {
        lock (plugLock)
        {
            if (state is not null && !ReferenceEquals(state.Speaker, speaker))
                throw new InvalidOperationException("All'uscita audio è già collegato un altoparlante: scollegarlo prima di collegarne un altro");
            if (speaker.SampleRate <= 0 || speaker.SampleRate > clockHz)
                throw new ArgumentException($"Frequenza di campionamento non valida: {speaker.SampleRate}", nameof(speaker));
            state ??= new State(speaker, clockHz);
        }
    }

    internal void Unplug(ISpeaker speaker)
    {
        lock (plugLock)
        {
            if (state is not null && ReferenceEquals(state.Speaker, speaker)) state = null;
        }
    }

    // Chiamato dal thread di emulazione a ogni ciclo
    internal void Sample(double level) => state?.Add(level);

    // Tutto lo stato del ricampionamento vive con il collegamento: un nuovo altoparlante riparte da zero
    sealed class State(ISpeaker speaker, long clockHz)
    {
        public readonly ISpeaker Speaker = speaker;
        readonly int sampleRate = speaker.SampleRate;
        readonly short[] block = new short[Math.Max(1, speaker.SampleRate / 100)];
        readonly double highPassFactor = 1 / (1 + 2 * Math.PI * HIGH_PASS_HZ / speaker.SampleRate);
        int length;

        // Ogni ciclo aggiunge sampleRate a phase: quando supera la frequenza del clock è pronto un campione.
        // Il conto è esatto, senza errori di arrotondamento che si accumulano
        long phase;
        double sum;
        int count;
        double previousInput, previousOutput;

        public void Add(double level)
        {
            sum += level;
            count++;
            phase += sampleRate;
            if (phase < clockHz) return;
            phase -= clockHz;

            double input = sum / count;
            sum = 0;
            count = 0;

            double output = highPassFactor * (previousOutput + input - previousInput);
            previousInput = input;
            previousOutput = output;

            block[length++] = (short)Math.Clamp(output * GAIN * short.MaxValue, short.MinValue, short.MaxValue);
            if (length == block.Length)
            {
                Speaker.Play(block);
                length = 0;
            }
        }
    }
}
