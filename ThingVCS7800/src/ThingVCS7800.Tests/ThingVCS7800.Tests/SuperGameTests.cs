using System.Buffers.Binary;
using ThingVCS;
using ThingVCS7800.A7800;
namespace ThingVCS7800.Tests;
public class SuperGameTests
{
    static byte[] Rom(int size)
    {
        var bytes = new byte[size];
        for (int bank = 0; bank < size / 16384; bank++) Array.Fill(bytes, (byte)bank, bank * 16384, 16384);
        return bytes;
    }
    static byte[] Image(int size, ushort flags)
    {
        var bytes = new byte[size + 128]; bytes[0] = 3;
        "ATARI7800"u8.CopyTo(bytes.AsSpan(1));
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(49), (uint)size);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(53), flags);
        Rom(size).CopyTo(bytes, 128); return bytes;
    }
    [Theory] [InlineData(65536)] [InlineData(131072)]
    public void FixedWindowsAndSelection(int size)
    {
        var cart = new SuperGameCartridge(Rom(size)); int last = size / 16384 - 1;
        Assert.Equal(last - 1, cart.Read(0x4000)); Assert.Equal(last, cart.Read(0xffff));
        cart.Write(0x8000, 2); Assert.Equal(2, cart.Read(0xbfff));
        cart.Write(0xc000, 0); Assert.Equal(2, cart.SelectedBank);
        cart.Write(0xbfff, 255); Assert.Equal(last, cart.Read(0x8000));
        cart.Reset(); Assert.Equal(0, cart.Read(0x8000));
    }
    [Fact] public void RamIsWritableAndRetainedOnReset()
    {
        var board = new Board(); var cart = new SuperGameCartridge(Rom(131072), true);
        board.CartridgeSocket.Insert(cart);
        board.Write(0x4000, 23); board.Write(0x7fff, 42); board.Write(0x8000, 5);
        Assert.Equal(5, board.Read(0x8000)); board.Reset();
        Assert.Equal(0, cart.SelectedBank); Assert.Equal(23, board.Read(0x4000)); Assert.Equal(42, board.Read(0x7fff));
        Assert.Equal(7, board.Read(0xc000));
    }
    [Theory] [InlineData(2)] [InlineData(6)] [InlineData(18)]
    public void ParserCreatesMapperFromFlags(int flags)
    {
        var image = CartridgeImage.Parse(Image(131072, (ushort)flags), true);
        Assert.Equal(flags, image.MapperFlags); Assert.IsType<SuperGameCartridge>(image.CreateCartridge());
        var host = new DevelopmentComputer(); host.InsertImage(Image(131072, (ushort)flags), true);
        host.Board.Write(0x8000, 3); Assert.Equal(3, host.Board.Read(0x8000));
    }
    [Fact] public void UnsupportedExpansionPreservesInsertedCart()
    {
        var host = new DevelopmentComputer(); host.InsertImage(Image(131072, 6), true);
        var cart = host.Board.CartridgeSocket.Chip;
        Assert.Throws<NotSupportedException>(() => host.InsertImage(Image(131072, 0x43), true));
        Assert.Same(cart, host.Board.CartridgeSocket.Chip);
    }
    [Theory] [InlineData(262144, 2)] [InlineData(524288, 2)] [InlineData(262144, 0)]
    public void LargeHeadersAreDiagnosedWithoutReplacingCart(int size, int flags)
    {
        var host = new DevelopmentComputer(); host.InsertImage(Image(131072, 2), true);
        var previous = host.Board.CartridgeSocket.Chip;
        var error = Assert.Throws<NotSupportedException>(() => host.InsertImage(Image(size, (ushort)flags), true));
        Assert.Contains($"{size} bytes", error.Message);
        Assert.Contains($"${flags:X4}", error.Message);
        Assert.Contains("A78 v3", error.Message);
        Assert.Same(previous, host.Board.CartridgeSocket.Chip);
    }
    [Fact] public void Legacy0502RequiresExplicitOptInAndDoesNotModifyFile()
    {
        var bytes = Image(131072, 0x0502); bytes[0] = 2;
        var original = bytes.ToArray();
        Assert.Throws<NotSupportedException>(() => CartridgeImage.Parse(bytes, true));
        var image = CartridgeImage.Parse(bytes, true, true);
        Assert.Equal(0x0502, image.OriginalMapperFlags);
        Assert.Equal(2, image.MapperFlags); Assert.True(image.UsesLegacyFallback);
        Assert.Equal(original, bytes);
        var host = new DevelopmentComputer(); host.InsertImage(bytes, true, true);
        host.Board.Write(0x8000, 3); Assert.Equal(3, host.Board.Read(0x8000));
    }
    [Theory] [InlineData(0x0402)] [InlineData(0x0506)] [InlineData(0x43)]
    public void LegacyOptionDoesNotIgnoreOtherExpansions(int flags)
    {
        Assert.Throws<NotSupportedException>(() => CartridgeImage.Parse(Image(131072, (ushort)flags), true, true));
    }
    [Fact] public void RawLargeRomIsNotGuessedAndPayloadIsCopied()
    {
        Assert.Throws<InvalidDataException>(() => CartridgeImage.Parse(Rom(131072), false));
        var bytes = Image(131072, 2); var image = CartridgeImage.Parse(bytes, true); Array.Clear(bytes);
        Assert.Equal(7, image.CreateCartridge().Read(0xffff));
    }
}
