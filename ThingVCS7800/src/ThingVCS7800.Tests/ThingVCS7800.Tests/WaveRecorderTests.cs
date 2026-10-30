using System.Buffers.Binary;
using ThingRetroEmu.Audio;
namespace ThingVCS7800.Tests;
public class WaveRecorderTests
{
    [Fact] public void WritesPcmAndFinalizesHeaderLeavingStreamOpen()
    {
        using var stream = new MemoryStream(); var recorder = new WaveRecorder(stream, 22050, true);
        recorder.Play([short.MinValue, 0, short.MaxValue]); Assert.Equal(3, recorder.SampleCount);
        recorder.Dispose(); recorder.Dispose();
        byte[] bytes = stream.ToArray(); Assert.Equal(50, bytes.Length);
        Assert.Equal("RIFF"u8.ToArray(), bytes[..4]);
        Assert.Equal(42u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)));
        Assert.Equal(22050, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(24)));
        Assert.Equal(6u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(40)));
        Assert.Equal(short.MinValue, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(44)));
        Assert.Equal(short.MaxValue, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(48)));
        Assert.Throws<ObjectDisposedException>(() => recorder.Play([]));
    }
    [Fact] public void EmptyRecordingIsValidAndDefaultDisposeClosesStream()
    {
        var stream = new MemoryStream(); using (var recorder = new WaveRecorder(stream)) Assert.Equal(0, recorder.SampleCount);
        Assert.Equal(44, stream.ToArray().Length); Assert.False(stream.CanWrite);
    }
    [Theory][InlineData(0)][InlineData(-1)][InlineData(192001)]
    public void RejectsInvalidRates(int rate) => Assert.Throws<ArgumentOutOfRangeException>(() => new WaveRecorder(new MemoryStream(), rate));
    [Fact] public void DoesNotOverwriteExistingData()
    {
        using var stream = new MemoryStream(); stream.WriteByte(42); stream.Position = 0;
        Assert.Throws<ArgumentException>(() => new WaveRecorder(stream)); Assert.Equal(42, stream.ToArray()[0]);
    }
}
