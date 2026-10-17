namespace Thing65.C65;

// Audio output of the Board (the AUDIO pin of the A/V socket). It receives the level of the SID's AUDIO OUT at
// every cycle and does what the board's output stage and the PC sound card do:
// - resamples to the speaker's rate by averaging the cycles of each sample (a low-pass);
// - removes the DC component like the output coupling capacitor (16 Hz high-pass);
// - converts to 16-bit samples, sent to the speaker in 10 ms blocks
public sealed class AudioPort
{
    const double HIGH_PASS_HZ = 16;
    const double GAIN = 0.3;            // three voices at full amplitude stay within the 16-bit range

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
                throw new InvalidOperationException("A speaker is already connected to the audio output: unplug it before connecting another one");
            if (speaker.SampleRate <= 0 || speaker.SampleRate > clockHz)
                throw new ArgumentException($"Invalid sample rate: {speaker.SampleRate}", nameof(speaker));
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

    // Called by the emulation thread at every cycle
    internal void Sample(double level) => state?.Add(level);

    // All the resampling state lives with the connection: a new speaker starts from scratch
    sealed class State(ISpeaker speaker, long clockHz)
    {
        public readonly ISpeaker Speaker = speaker;
        readonly int sampleRate = speaker.SampleRate;
        readonly short[] block = new short[Math.Max(1, speaker.SampleRate / 100)];
        readonly double highPassFactor = 1 / (1 + 2 * Math.PI * HIGH_PASS_HZ / speaker.SampleRate);
        int length;

        // Every cycle adds sampleRate to phase: when it exceeds the clock frequency a sample is ready.
        // The count is exact, without rounding errors piling up
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
