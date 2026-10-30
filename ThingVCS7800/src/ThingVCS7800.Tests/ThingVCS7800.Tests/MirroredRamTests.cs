using System.Buffers.Binary;
using ThingVCS;
using ThingVCS7800.A7800;
namespace ThingVCS7800.Tests;
public class MirroredRamTests
{
    static byte[] Image(ushort flags, int size = 32768)
    {
        var data = new byte[size + 128]; data[0] = 1;
        "ATARI7800"u8.CopyTo(data.AsSpan(1));
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(49), (uint)size);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(53), flags); data[128] = 42;
        return data;
    }
    [Fact] public void A8IsDisconnectedButOtherAddressBitsRemainDistinct()
    {
        var host = new DevelopmentComputer(); host.InsertImage(Image(0x80), true);
        host.Board.Write(0x4000, 23); Assert.Equal(23, host.Board.Read(0x4100));
        host.Board.Write(0x4300, 55); Assert.Equal(55, host.Board.Read(0x4200));
        Assert.Equal(23, host.Board.Read(0x4000));
        host.Board.Write(0x7fff, 77); Assert.Equal(77, host.Board.Read(0x7eff));
        Assert.Equal(42, host.Board.Read(0x8000)); host.Board.Write(0x8000, 99);
        Assert.Equal(42, host.Board.Read(0x8000)); host.Reset(); Assert.Equal(23, host.Board.Read(0x4100));
    }
    [Fact] public void OverrideIsNarrowExplicitAndPreservesHeader()
    {
        var bytes = Image(6); var original = bytes.ToArray();
        Assert.Throws<NotSupportedException>(() => CartridgeImage.Parse(bytes, true));
        var image = CartridgeImage.Parse(bytes, true, allowMirroredRam0006: true);
        Assert.Equal(6, image.OriginalMapperFlags); Assert.Equal(0x80, image.MapperFlags);
        Assert.Equal(original, bytes); Assert.IsType<MirroredRamCartridge>(image.CreateCartridge());
        Assert.IsType<SuperGameCartridge>(CartridgeImage.Parse(Image(6, 65536), true, allowMirroredRam0006: true).CreateCartridge());
        Assert.Throws<NotSupportedException>(() => CartridgeImage.Parse(Image(2), true, allowMirroredRam0006: true));
    }
}
