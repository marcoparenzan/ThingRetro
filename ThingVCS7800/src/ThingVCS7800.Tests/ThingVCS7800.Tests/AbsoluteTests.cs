using System.Buffers.Binary;
using ThingVCS;
using ThingVCS7800.A7800;
namespace ThingVCS7800.Tests;
public class AbsoluteTests
{
    static byte[] Image(ushort flags)
    {
        var data = new byte[65536 + 128]; data[0] = 2;
        "ATARI7800"u8.CopyTo(data.AsSpan(1));
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(49), 65536);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(53), flags);
        for (int i = 0; i < 4; i++) Array.Fill(data, (byte)i, 128 + i * 16384, 16384);
        return data;
    }
    [Fact] public void SelectionAndFixedWindowsWorkThroughConnector()
    {
        var host = new DevelopmentComputer(); host.InsertImage(Image(0x0200), true);
        Assert.IsType<AbsoluteCartridge>(host.Board.CartridgeSocket.Chip);
        host.Board.Write(0x8000, 2); Assert.Equal(1, host.Board.Read(0x4000)); Assert.Equal(1, host.Board.Read(0x7fff));
        Assert.Equal(2, host.Board.Read(0x8000)); Assert.Equal(3, host.Board.Read(0xffff));
        host.Board.Write(0x8001, 1); Assert.Equal(1, host.Board.Read(0x4000));
        host.Board.Write(0x8000, 0); Assert.Equal(1, host.Board.Read(0x4000));
        host.Board.Write(0x8000, 3); Assert.Equal(0, host.Board.Read(0x4000));
        host.Board.Write(0x8000, 2); host.Reset(); Assert.Equal(0, host.Board.Read(0x4000));
    }
    [Fact] public void AmbiguousHeaderNeedsOptInAndIsNotModified()
    {
        var bytes = Image(0x0300); var original = bytes.ToArray();
        Assert.Throws<NotSupportedException>(() => CartridgeImage.Parse(bytes, true));
        var image = CartridgeImage.Parse(bytes, true, allowAbsolute0300: true);
        Assert.Equal(0x0300, image.OriginalMapperFlags); Assert.Equal(0x0200, image.MapperFlags);
        Assert.True(image.UsesLegacyFallback); Assert.Equal(original, bytes);
        Assert.IsType<AbsoluteCartridge>(image.CreateCartridge());
        Assert.Throws<NotSupportedException>(() => CartridgeImage.Parse(Image(0x0100), true, allowAbsolute0300: true));
        Assert.Throws<ArgumentException>(() => new AbsoluteCartridge(new byte[32768]));
    }
}
