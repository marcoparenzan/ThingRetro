namespace ThingRetroEmu.Audio;

// The audio cable: connects the audio output of a Board to a speaker. Dispose unplugs it
public sealed class AudioCable : IDisposable
{
    readonly AudioPort port;

    public AudioCable(AudioPort port, ISpeaker speaker)
    {
        this.port = port;
        Speaker = speaker;
        port.Plug(speaker);
    }

    public ISpeaker Speaker { get; }

    public void Dispose() => port.Unplug(Speaker);
}
