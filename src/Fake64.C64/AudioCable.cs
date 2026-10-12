namespace Fake64.C64;

// Il cavo audio: collega l'uscita audio di una Board a un altoparlante. Dispose lo scollega
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
