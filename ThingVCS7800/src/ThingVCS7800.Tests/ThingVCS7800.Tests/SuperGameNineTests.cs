using System.Buffers.Binary;
using ThingVCS;
using ThingVCS7800.A7800;
namespace ThingVCS7800.Tests;
public class SuperGameNineTests
{
    static byte[] Image()
    {
        var data = new byte[147456 + 128]; data[0] = 1;
        "ATARI7800"u8.CopyTo(data.AsSpan(1)); data[54] = 10;
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(49), 147456);
        for (int i = 0; i < 9; i++) Array.Fill(data, (byte)i, 128 + i * 16384, 16384);
        return data;
    }
    [Fact] public void HeaderAndAllBanksAreMappedThroughBoard()
    {
        var host = new DevelopmentComputer(); host.InsertImage(Image(), true);
        Assert.IsType<SuperGameNineCartridge>(host.Board.CartridgeSocket.Chip);
        for (int i = 0; i < 256; i++)
        {
            host.Board.Write(0x8000, (byte)i);
            Assert.Equal((i & 7) + 1, host.Board.Read(0x8000));
            Assert.Equal((i & 7) + 1, host.Board.Read(0xbfff));
            Assert.Equal(0, host.Board.Read(0x4000)); Assert.Equal(0, host.Board.Read(0x7fff));
            Assert.Equal(8, host.Board.Read(0xc000)); Assert.Equal(8, host.Board.Read(0xffff));
        }
        host.Board.Write(0xc000, 0); Assert.Equal(8, host.Board.Read(0x8000));
        host.Reset(); Assert.Equal(0, host.Board.Read(0x8000));
    }
    [Fact] public void WrongSizeRejectedAndRomIsCopied()
    {
        Assert.Throws<ArgumentException>(() => new SuperGameNineCartridge(new byte[131072]));
        var bytes = Image(); var image = CartridgeImage.Parse(bytes, true); Array.Clear(bytes);
        Assert.Equal(8, image.CreateCartridge().Read(0xffff));
        bytes = new byte[131072 + 128]; Image().AsSpan(0, 128).CopyTo(bytes);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(49), 131072);
        Assert.Throws<NotSupportedException>(() => CartridgeImage.Parse(bytes, true));
    }
}
