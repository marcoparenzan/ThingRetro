using System.Buffers.Binary;
using ThingVCS;
namespace ThingVCS7800.Tests;
public class PokeyTests
{
    [Fact] public void PureToneUsesBaseDividerAndResetSilences()
    {
        var chip = new Pokey(); chip.Write(15, 3); chip.Write(1, 0xaf);
        for (int i = 0; i < 27; i++) chip.Tick();
        Assert.Equal(0, chip.AudioLevel); chip.Tick(); Assert.Equal(.25, chip.AudioLevel);
        for (int i = 0; i < 28; i++) chip.Tick(); Assert.Equal(0, chip.AudioLevel);
        chip.Write(1, 0x1f); Assert.Equal(.25, chip.AudioLevel);
        chip.Reset(); Assert.Equal(0, chip.AudioLevel);
    }
    [Fact] public void RegistersAreNotRomAndChipCanBeRemoved()
    {
        var bytes = new byte[16384]; bytes[0] = 42;
        var cart = new PokeyCartridge(bytes);
        Assert.Equal(42, cart.Read(0xc000)); cart.Write(0x4001, 0x1f);
        Assert.Equal(.25, cart.AudioLevel); Assert.Equal(228, cart.Read(0x4001));
        cart.PokeySocket.Remove(); Assert.Equal(0, cart.AudioLevel);
        Assert.Equal(255, cart.Read(0x4000)); Assert.Equal(42, cart.Read(0xc000));
    }
    [Fact] public void SuperGamePokeyKeepsBankingSeparateFromAudio()
    {
        var bytes = new byte[131072 + 128]; bytes[0] = 1;
        "ATARI7800"u8.CopyTo(bytes.AsSpan(1)); bytes[54] = 3;
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(49), 131072);
        for (int bank = 0; bank < 8; bank++) Array.Fill(bytes, (byte)bank, 128 + bank * 16384, 16384);
        var cart = Assert.IsType<PokeyCartridge>(CartridgeImage.Parse(bytes, true).CreateCartridge());
        cart.Write(0x8000, 5); Assert.Equal(5, cart.Read(0x8000)); Assert.Equal(7, cart.Read(0xffff));
        cart.Write(0x4001, 0x1f); Assert.Equal(.25, cart.AudioLevel);
        Assert.Equal(228, cart.Read(0x4000)); Assert.Equal(5, cart.Read(0xbfff));
        cart.Reset(); Assert.Equal(0, cart.Read(0x8000)); Assert.Equal(0, cart.AudioLevel);
        cart.PokeySocket.Remove(); cart.Write(0x8000, 2); Assert.Equal(2, cart.Read(0x8000));
    }
    [Fact] public void HeaderCreatesLinearPokeyCartridge()
    {
        var bytes = new byte[16384 + 128]; bytes[0] = 1;
        "ATARI7800"u8.CopyTo(bytes.AsSpan(1));
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(49), 16384); bytes[54] = 1;
        Assert.IsType<PokeyCartridge>(CartridgeImage.Parse(bytes, true).CreateCartridge());
    }
}
