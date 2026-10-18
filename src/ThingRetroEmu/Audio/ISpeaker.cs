namespace ThingRetroEmu.Audio;

// Speaker (PC speakers, amplifier, recorder...) connected to the Board's audio output through an AudioCable.
// It receives signed 16-bit mono samples at the rate it declares. Play is called by the emulation thread
// every 10 ms of emulated audio: the implementation must copy the samples and return at once
public interface ISpeaker
{
    int SampleRate { get; }
    void Play(ReadOnlySpan<short> samples);
}
