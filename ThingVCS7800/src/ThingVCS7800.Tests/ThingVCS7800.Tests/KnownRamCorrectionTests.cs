using System.Buffers.Binary;
using ThingVCS;
namespace ThingVCS7800.Tests;
public class KnownRamCorrectionTests
{
    static byte[] Image()
    {
        var bytes = new byte[131072 + 128]; bytes[0] = 1;
        "ATARI7800"u8.CopyTo(bytes.AsSpan(1));
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(49, 4), 131072);
        bytes[54] = 2;
        "Summer Games"u8.CopyTo(bytes.AsSpan(17));
        return bytes;
    }
    [Theory][InlineData(0)][InlineData(127)][InlineData(128)][InlineData(16512)]
    public void DevelopmentCardIdentificationRequiresPayloadIdentity(int size) =>
        Assert.False(CartridgeImage.HasKnownDevelopmentCard(new byte[size]));
    [Fact] public void DevelopmentCardTitleDoesNotIdentifyUnrelatedPayload()
    {
        var bytes = new byte[16384 + 128]; bytes[0] = 1;
        "ATARI7800"u8.CopyTo(bytes.AsSpan(1));
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(49, 4), 16384);
        "Devcard"u8.CopyTo(bytes.AsSpan(17));
        Assert.False(CartridgeImage.HasKnownDevelopmentCard(bytes));
        Assert.Equal(0, CartridgeImage.Parse(bytes, true).MapperFlags);
    }
    [Theory][InlineData("Summer Games")][InlineData("Winter Games")]
    public void MatchingTitleAndSizeAreNotEnoughToCorrectHeader(string title)
    {
        var bytes = Image();
        Array.Clear(bytes, 17, 32);
        System.Text.Encoding.ASCII.GetBytes(title).CopyTo(bytes, 17);
        Assert.False(CartridgeImage.HasKnownMissingRam(bytes));
        var parsed = CartridgeImage.Parse(bytes, true, allowKnownRamCorrection: true);
        Assert.Equal(2, parsed.MapperFlags);
        Assert.False(parsed.UsesLegacyFallback);
        var cart = parsed.CreateCartridge(); cart.Write(0x6000, 42);
        Assert.Equal(0, cart.Read(0x6000));
    }
    [Theory][InlineData(0)][InlineData(127)][InlineData(128)][InlineData(131072)]
    public void TruncatedAndRawInputsDoNotMatch(int size) =>
        Assert.False(CartridgeImage.HasKnownMissingRam(new byte[size]));
    [Fact] public void OptionDoesNotMutateInputOrOverrideOtherMetadata()
    {
        var bytes = Image(); var original = bytes.ToArray();
        CartridgeImage.Parse(bytes, true, allowKnownRamCorrection: true);
        Assert.Equal(original, bytes);
        bytes[57] = 2;
        Assert.Throws<NotSupportedException>(() => CartridgeImage.Parse(bytes, true, allowKnownRamCorrection: true));
    }
}
