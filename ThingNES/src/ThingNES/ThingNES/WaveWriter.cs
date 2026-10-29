using System.Text;

namespace ThingNES;

// Streaming 44.1 kHz mono PCM16 WAV; leaves the caller's stream open.
public sealed class WaveWriter : IDisposable
{
    readonly BinaryWriter writer;
    readonly long start;
    uint bytes;
    bool disposed;
    public WaveWriter(Stream stream)
    {
        if (!stream.CanSeek || !stream.CanWrite) throw new ArgumentException("WAV output must be writable and seekable.", nameof(stream));
        start = stream.Position;
        writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write("RIFF"u8); writer.Write(0u); writer.Write("WAVEfmt "u8); writer.Write(16u);
        writer.Write((ushort)1); writer.Write((ushort)1); writer.Write(Apu.SampleRate); writer.Write(Apu.SampleRate * 2);
        writer.Write((ushort)2); writer.Write((ushort)16); writer.Write("data"u8); writer.Write(0u);
    }
    public void WriteSample(short sample)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (bytes > uint.MaxValue - 38) throw new IOException("WAV exceeds RIFF size limit.");
        writer.Write(sample); bytes += 2;
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        writer.Flush(); long end = writer.BaseStream.Position;
        writer.BaseStream.Position = start + 4; writer.Write(bytes + 36);
        writer.BaseStream.Position = start + 40; writer.Write(bytes);
        writer.BaseStream.Position = end; writer.Dispose();
    }
}
