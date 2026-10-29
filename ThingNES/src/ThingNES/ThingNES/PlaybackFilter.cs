namespace ThingNES;

// Lightweight playback-only DC blocker and low-pass. Not a composite/analog NES audio model.
public sealed class PlaybackFilter
{
    double previousInput, highPass, lowPass;
    public void Reset() => previousInput = highPass = lowPass = 0;
    public short Process(short sample)
    {
        double input = sample / 32768.0;
        highPass = input - previousInput + 0.995 * highPass;
        previousInput = input;
        lowPass += 0.864 * (highPass - lowPass);
        return (short)Math.Clamp(lowPass * 0.5 * 32767, short.MinValue, short.MaxValue);
    }
}
