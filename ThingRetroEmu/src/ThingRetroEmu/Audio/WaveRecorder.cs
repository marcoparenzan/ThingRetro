using System.Text;
namespace ThingRetroEmu.Audio;

/// <summary>PCM16 mono recorder usable through the same audio cable as any speaker.</summary>
public sealed class WaveRecorder : ISpeaker, IDisposable
{
    readonly Stream stream;
    readonly bool leaveOpen;
    long samples;
    bool disposed;
    public int SampleRate { get; }
    public long SampleCount => samples;
    public WaveRecorder(Stream stream, int sampleRate = 44100, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanWrite || !stream.CanSeek) throw new ArgumentException("A writable, seekable stream is required.", nameof(stream));
        if (stream.Position != 0 || stream.Length != 0) throw new ArgumentException("The output stream must be empty.", nameof(stream));
        if (sampleRate is <= 0 or > 192000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        this.stream = stream; this.leaveOpen = leaveOpen; SampleRate = sampleRate;
        WriteHeader(0);
    }
    void WriteHeader(uint bytes)
    {
        using var writer = new BinaryWriter(stream, Encoding.ASCII, true);
        writer.Write("RIFF"u8); writer.Write(bytes + 36); writer.Write("WAVEfmt "u8);
        writer.Write(16u); writer.Write((ushort)1); writer.Write((ushort)1);
        writer.Write(SampleRate); writer.Write(SampleRate * 2); writer.Write((ushort)2); writer.Write((ushort)16);
        writer.Write("data"u8); writer.Write(bytes);
    }
    public void Play(ReadOnlySpan<short> data)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if ((samples + data.Length) * 2 > uint.MaxValue - 36L) throw new IOException("PCM exceeds the RIFF size limit.");
        using var writer = new BinaryWriter(stream, Encoding.ASCII, true);
        foreach (short sample in data) writer.Write(sample);
        samples += data.Length;
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try
        {
            stream.Position = 0; WriteHeader(checked((uint)(samples * 2)));
            stream.Position = stream.Length; stream.Flush();
        }
        finally { if (!leaveOpen) stream.Dispose(); }
    }
}
