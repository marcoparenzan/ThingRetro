namespace ThingRetroEmu.Audio;

/// <summary>Common audio distributor: one cable input, multiple same-rate destinations.
/// Does not own destinations; connection changes and Play require the same owner thread.</summary>
public sealed class AudioSplitter(int sampleRate = 44100) : ISpeaker
{
    readonly List<ISpeaker> outputs = [];
    public int SampleRate { get; } = sampleRate > 0 ? sampleRate : throw new ArgumentOutOfRangeException(nameof(sampleRate));
    public void Connect(ISpeaker speaker)
    {
        ArgumentNullException.ThrowIfNull(speaker);
        if (ReferenceEquals(speaker, this)) throw new ArgumentException("Cannot connect a distributor to itself.", nameof(speaker));
        if (speaker is AudioSplitter) throw new ArgumentException("Nested distributors are not supported.", nameof(speaker));
        if (speaker.SampleRate != SampleRate) throw new ArgumentException("Destination sample rates must match.", nameof(speaker));
        if (!outputs.Any(output => ReferenceEquals(output, speaker))) outputs.Add(speaker);
    }
    public void Disconnect(ISpeaker speaker) => outputs.RemoveAll(output => ReferenceEquals(output, speaker));
    public void Play(ReadOnlySpan<short> samples)
    {
        foreach (var output in outputs) output.Play(samples);
    }
}
