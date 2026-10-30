using System.Buffers.Binary;
using ThingVCS;
using ThingVCS7800.A7800;
namespace ThingVCS7800.Tests;
public class CartridgeImageTests
{
    static byte[] Header(int size = 16384)
    {
        var data = new byte[size + 128]; data[0] = 3;
        "ATARI7800"u8.CopyTo(data.AsSpan(1)); "TEST ROM"u8.CopyTo(data.AsSpan(17));
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(49), (uint)size);
        data[55] = data[56] = 1; data[128] = 42; return data;
    }
    [Theory][InlineData(16384)][InlineData(32768)][InlineData(49152)]
    public void HeaderIsRemovedAndPayloadIsImmutable(int size)
    {
        var bytes = Header(size); var image = CartridgeImage.Parse(bytes, true);
        Assert.Equal("TEST ROM", image.Title); Assert.Equal(size, image.Size); Assert.True(image.HasHeader);
        bytes[128] = 99; Assert.Equal(42, image.CreateCartridge().Read((ushort)(65536 - size)));
    }
    [Theory][InlineData(1)][InlineData(2)][InlineData(3)]
    public void LegacyVersionsAreAccepted(int version)
    {
        var data = Header(); data[0] = (byte)version;
        Assert.True(CartridgeImage.Parse(data, true).HasHeader);
    }
    [Fact] public void RawRomIsNotGuessedFromItsContents()
    {
        var data = new byte[16384]; "ATARI7800"u8.CopyTo(data.AsSpan(1));
        Assert.False(CartridgeImage.Parse(data, false).HasHeader);
    }
    [Fact] public void BadSignatureAndSizeAreRejected()
    {
        var data = Header(); data[1] = 0;
        Assert.Throws<InvalidDataException>(() => CartridgeImage.Parse(data, true));
        data = Header(); data[52] = 0;
        // Change big-endian declared size from 0x4000 to 0x4100.
        data[51] = 0x41;
        Assert.Throws<InvalidDataException>(() => CartridgeImage.Parse(data, true));
        Assert.Throws<InvalidDataException>(() => CartridgeImage.Parse(new byte[127], true));
    }
    [Theory][InlineData(0,4)][InlineData(53,1)][InlineData(54,0x43)][InlineData(55,3)][InlineData(56,3)][InlineData(57,2)][InlineData(58,2)][InlineData(58,255)]
    public void UnsupportedMetadataIsRejected(int offset, int value)
    {
        var data = Header(); data[offset] = (byte)value;
        Assert.Throws<NotSupportedException>(() => CartridgeImage.Parse(data, true));
    }
    [Fact] public void InvalidImageDoesNotEjectExistingCartridge()
    {
        var host = new DevelopmentComputer(); host.InsertImage(Header(), true);
        var old = host.Board.CartridgeSocket.Chip;
        var data = Header(); data[54] = 2;
        Assert.Throws<NotSupportedException>(() => host.InsertImage(data, true));
        Assert.Same(old, host.Board.CartridgeSocket.Chip); Assert.False(host.Paused);
    }
    [Fact] public void HighScoreMetadataLoadsWithoutPretendingPeripheralExists()
    {
        var bytes = Header(); bytes[0] = 2; bytes[58] = 1;
        var image = CartridgeImage.Parse(bytes, true);
        Assert.Equal(1, image.SavePeripheral); Assert.True(image.HighScoreCartridgeAbsent);
        Assert.IsType<LinearCartridge>(image.CreateCartridge());
        var host = new DevelopmentComputer(); host.InsertImage(bytes, true);
        Assert.True(host.HasCartridge);
        Assert.False(CartridgeImage.Parse(Header(), true).HighScoreCartridgeAbsent);
    }
    [Theory] [InlineData(2, 0)] [InlineData(1, 2)]
    public void LightGunMetadataIsPreservedAndDoesNotBlockLoading(int first, int second)
    {
        var bytes = Header(); bytes[55] = (byte)first; bytes[56] = (byte)second;
        var image = CartridgeImage.Parse(bytes, true);
        Assert.True(image.RequiresLightGun); Assert.Equal(first, image.Controller1Type); Assert.Equal(second, image.Controller2Type);
        var host = new DevelopmentComputer(); host.InsertImage(bytes, true); Assert.True(host.HasCartridge);
    }
    [Theory] [InlineData(3,3)] [InlineData(255,1)]
    public void UnknownControllersRequireOptInAndKeepOriginalMetadata(int first, int second)
    {
        var bytes = Header(); bytes[55] = (byte)first; bytes[56] = (byte)second;
        var original = bytes.ToArray();
        Assert.Throws<NotSupportedException>(() => CartridgeImage.Parse(bytes, true));
        var image = CartridgeImage.Parse(bytes, true, allowUnknownControllers: true);
        Assert.Equal(first, image.Controller1Type); Assert.Equal(second, image.Controller2Type);
        Assert.False(image.RequiresLightGun); Assert.Equal(original, bytes);
        var host = new DevelopmentComputer(); host.InsertImage(bytes, true, allowUnknownControllers: true);
        Assert.True(host.HasCartridge);
        bytes[57] = 2;
        Assert.Throws<NotSupportedException>(() => CartridgeImage.Parse(bytes, true, allowUnknownControllers: true));
    }
    [Theory] [InlineData(0x9d)] [InlineData(0xff)]
    public void UnknownSaveMetadataRequiresConfirmationAndIsPreserved(int value)
    {
        var bytes = Header(); bytes[58] = (byte)value; var original = bytes.ToArray();
        Assert.Throws<NotSupportedException>(() => CartridgeImage.Parse(bytes, true));
        var image = CartridgeImage.Parse(bytes, true, allowUnknownSavePeripheral: true);
        Assert.Equal(value, image.SavePeripheral); Assert.False(image.HighScoreCartridgeAbsent);
        Assert.Equal(original, bytes);
        var host = new DevelopmentComputer(); host.InsertImage(bytes, true, allowUnknownSavePeripheral: true);
        Assert.True(host.HasCartridge);
        bytes[58] = 2;
        Assert.Throws<NotSupportedException>(() => CartridgeImage.Parse(bytes, true, allowUnknownSavePeripheral: true));
        bytes[58] = (byte)value; bytes[57] = 2;
        Assert.Throws<NotSupportedException>(() => CartridgeImage.Parse(bytes, true, allowUnknownSavePeripheral: true));
    }
    [Fact] public void HeaderTitleControlsAreSanitized()
    {
        var data = Header(); data[17] = 10;
        Assert.DoesNotContain('\n', CartridgeImage.Parse(data, true).Title);
    }
}
