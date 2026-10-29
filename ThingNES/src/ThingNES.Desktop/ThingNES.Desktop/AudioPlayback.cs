using NAudio.Wave;

namespace ThingNES.Desktop;

// WinMM output with a bounded 120 ms managed queue; never retains unlimited emulator samples.
internal sealed class AudioPlayback : IDisposable
{
    readonly WaveOutEvent output = new() { DesiredLatency = 60, NumberOfBuffers = 3 };
    readonly BufferedWaveProvider provider = new(new WaveFormat(Apu.SampleRate, 16, 1))
    { BufferDuration = TimeSpan.FromMilliseconds(120), DiscardOnBufferOverflow = true, ReadFully = true };
    readonly byte[] packet = new byte[1024];
    readonly PlaybackFilter filter = new();
    int packetBytes;
    public AudioPlayback()
    {
        try { output.Init(provider); }
        catch { output.Dispose(); throw; }
    }
    public void Start() => output.Play();
    public void Pause()
    {
        output.Stop(); provider.ClearBuffer(); packetBytes = 0; filter.Reset();
    }
    public void AddSample(short sample)
    {
        short value = filter.Process(sample);
        packet[packetBytes++] = (byte)value; packet[packetBytes++] = (byte)(value >> 8);
        if (packetBytes == packet.Length) { provider.AddSamples(packet, 0, packetBytes); packetBytes = 0; }
    }
    public void Dispose() { output.Stop(); output.Dispose(); provider.ClearBuffer(); }
}
