using System.Buffers.Binary;
using ThingRetroEmu.Video;
using ThingVCS7800.A7800;
namespace ThingVCS7800.Tests;
public class TiaAudioDiagnosticTests
{
    [Theory][InlineData(VideoSystem.Ntsc)][InlineData(VideoSystem.Pal)]
    public void CpuProgramsBothChannelsAndProducesFinalizedNonzeroWave(VideoSystem system)
    {
        using var stream = new MemoryStream();
        var result = new TiaAudioDiagnostic().Run(stream, system, 30);
        Assert.True(result.Passed); Assert.True(result.RegistersProgrammed);
        byte[] wave = stream.ToArray();
        Assert.Equal(result.Samples * 2 + 44, wave.Length);
        Assert.Equal(result.Samples * 2, BinaryPrimitives.ReadUInt32LittleEndian(wave.AsSpan(40)));
        Assert.Contains(wave.Skip(44), value => value != 0);
        Assert.True(stream.CanWrite); Assert.Contains("Synthetic", result.Kind);
    }
    [Theory][InlineData(0)][InlineData(19)][InlineData(10001)]
    public void InvalidDurationDoesNotWriteOutput(int duration)
    {
        using var stream = new MemoryStream();
        Assert.Throws<ArgumentOutOfRangeException>(() => new TiaAudioDiagnostic().Run(stream, milliseconds: duration));
        Assert.Equal(0, stream.Length);
    }
}
