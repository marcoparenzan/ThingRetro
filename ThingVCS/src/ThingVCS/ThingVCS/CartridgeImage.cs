using System.Buffers.Binary;
using System.Text;
namespace ThingVCS;

/// <summary>Validated raw ROM or legacy A78 v1-v3 linear NTSC cartridge. No mapper inference.</summary>
public sealed class CartridgeImage
{
    readonly byte[] rom;
    public string Title { get; }
    public int Size => rom.Length;
    public bool HasHeader { get; }
    public byte Controller1Type { get; private init; }
    public byte Controller2Type { get; private init; }
    public bool RequiresLightGun => Controller1Type == 2 || Controller2Type == 2;
    public ThingRetroEmu.Video.VideoSystem TvSystem { get; private init; } = ThingRetroEmu.Video.VideoSystem.Ntsc;
    public byte SavePeripheral { get; private init; }
    public bool HighScoreCartridgeAbsent => SavePeripheral == 1;
    CartridgeImage(ReadOnlySpan<byte> data, string title, bool header)
    {
        rom = data.ToArray(); Title = title; HasHeader = header;
    }
    public ushort MapperFlags { get; private init; }
    public string MapperName => MapperFlags == 0x0080 ? "Linear + mirrored RAM (A8 disconnected)" : MapperFlags == 0x0200 ? "Absolute" : MapperFlags == 3 ? "SuperGame + POKEY (partial)" : MapperFlags == 10 ? "SuperGame 9 banks" : MapperFlags == 1 ? "Linear + POKEY (partial)" : MapperFlags == 0 ? "Linear" : (MapperFlags & 4) != 0 ? "SuperGame + 16 KiB RAM" : "SuperGame";
    public string Description => $"{Title} — {Size / 1024} KiB — {MapperName} (flags ${MapperFlags:X4})";
    public ICartridgePinout CreateCartridge() => MapperFlags == 0x0080 ? new MirroredRamCartridge(rom) : MapperFlags == 0x0200 ? new AbsoluteCartridge(rom) : MapperFlags == 3 ? new PokeyCartridge(rom, superGame: true) : MapperFlags == 10 ? new SuperGameNineCartridge(rom) : MapperFlags == 1 ? new PokeyCartridge(rom) : MapperFlags == 0 ? new LinearCartridge(rom) : new SuperGameCartridge(rom, (MapperFlags & 4) != 0);
    public ushort OriginalMapperFlags { get; private init; }
    public bool UsesLegacyFallback => OriginalMapperFlags != MapperFlags;
    public static CartridgeImage Parse(ReadOnlySpan<byte> data, bool a78, bool allowLegacy0502 = false, bool allowUnknownControllers = false, bool allowAbsolute0300 = false, bool allowUnknownSavePeripheral = false, bool allowMirroredRam0006 = false, bool allowKnownRamCorrection = false)
    {
        if (!a78)
        {
            ValidateSize(data.Length);
            return new(data, "", false);
        }
        if (data.Length < 128) throw new InvalidDataException("Truncated A78 header.");
        if (!data.Slice(1, 9).SequenceEqual("ATARI7800"u8))
            throw new InvalidDataException("Invalid A78 signature.");
        if (data[0] is < 1 or > 3)
            throw new NotSupportedException("Only legacy A78 header versions 1-3 are supported.");
        uint size = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(49, 4));
        if (size != data.Length - 128)
            throw new InvalidDataException($"A78 v{data[0]}, file {data.Length} bytes: declared ROM {size} bytes does not match actual payload {data.Length - 128} bytes; flags ${BinaryPrimitives.ReadUInt16BigEndian(data.Slice(53, 2)):X4}.");
        ushort mapper = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(53, 2));
        ushort originalMapper = mapper;
        if (HasKnownDevelopmentCard(data))
            throw new NotSupportedException("Atari 7800 Development Card (Devcard), identified by payload SHA1 4df3657f57e3086bee26fb5c6ea946c509d08553: development-board hardware is not implemented. Its 16 KiB ROM must not be inferred as SuperGame RAM from legacy flags $0006. The MAME software catalog marks this card unsupported and its assigned POKEY slot as incorrect. No mapper override has been applied.");
        if (allowKnownRamCorrection && HasKnownMissingRam(data)) mapper = 6;
        // Explicit compatibility option only for the observed legacy header, never general masking.
        if (allowLegacy0502 && mapper == 0x0502) mapper = 2;
        if (allowAbsolute0300 && mapper == 0x0300) mapper = 0x0200;
        if (allowMirroredRam0006 && mapper == 6 && size == 32768) mapper = 0x0080;
        if (mapper == 0x0080)
        {
            if (size is not (16384 or 32768)) throw new NotSupportedException($"Mirrored RAM flags ${originalMapper:X4}: unsupported ROM size {size} bytes (expected 16/32 KiB).");
        }
        else if (mapper == 0x0200)
        {
            if (size != 65536) throw new NotSupportedException($"A78 v{data[0]}, original flags ${originalMapper:X4}: Absolute requires 65536 ROM bytes; declared/actual size is {size}.");
        }
        else if (mapper == 0)
        {
            if (size is not (16384 or 32768 or 49152))
                throw new NotSupportedException($"A78 v{data[0]}, file {data.Length} bytes, declared/actual ROM {size} bytes, flags ${originalMapper:X4}: unsupported linear capacity (currently 16/32/48 KiB). A larger ROM requires an explicitly supported cartridge layout.");
        }
        else if (mapper == 10)
        {
            if (size != 147456) throw new NotSupportedException($"A78 v{data[0]}, flags ${originalMapper:X4}: SuperGame 9 banks requires 147456 ROM bytes; actual/declared size is {size}.");
        }
        else if (mapper == 1)
        {
            if (size is not (16384 or 32768)) throw new NotSupportedException("Linear POKEY cartridges currently support 16/32 KiB ROMs.");
        }
        else if (mapper is 2 or 3 or 6 or 18)
        {
            if (size is not (65536 or 131072))
                throw new NotSupportedException($"A78 v{data[0]}, file {data.Length} bytes, declared/actual ROM {size} bytes ({size / 1024} KiB), original flags ${originalMapper:X4}, effective flags ${mapper:X4}: SuperGame capacity is not supported (currently 64/128 KiB). No larger-board mapper has been inferred.");
        }
        else throw new NotSupportedException($"A78 v{data[0]}, {size} bytes, flags ${mapper:X4}: unsupported mapper/expansion. Supported: Linear ($0000), Linear POKEY ($0001), SuperGame ($0002/$0012), SuperGame POKEY ($0003), SuperGame RAM ($0006), SuperGame 9 banks ($000A).");
        if (data[57] > 1)
            throw new NotSupportedException($"A78 v{data[0]}, {size} bytes, mapper ${originalMapper:X4}: unknown TV system byte 57 = ${data[57]:X2}. Supported: NTSC ($00), PAL ($01).");
        if (!allowUnknownControllers && (data[55] > 2 || data[56] > 2))
            throw new NotSupportedException($"A78 controller metadata: port 1 = ${data[55]:X2}, port 2 = ${data[56]:X2}. Recognized: disconnected ($00), joystick ($01), Light Gun ($02).");
        if (data[58] == 2 || (data[58] > 2 && !allowUnknownSavePeripheral))
            throw new NotSupportedException($"A78 save peripheral byte 58 = ${data[58]:X2}: SaveKey or unknown peripheral is not implemented.");
        string title = Encoding.ASCII.GetString(data.Slice(17, 32)).TrimEnd('\0', ' ');
        title = new string(title.Select(c => char.IsControl(c) ? ' ' : c).ToArray());
        return new(data[128..], title, true) { MapperFlags = mapper, OriginalMapperFlags = originalMapper, TvSystem = data[57] == 1 ? ThingRetroEmu.Video.VideoSystem.Pal : ThingRetroEmu.Video.VideoSystem.Ntsc, SavePeripheral = data[58], Controller1Type = data[55], Controller2Type = data[56] };
    }
    public static bool HasKnownDevelopmentCard(ReadOnlySpan<byte> data) =>
        data.Length == 16384 + 128 && data[0] is >= 1 and <= 3 &&
        data.Slice(1, 9).SequenceEqual("ATARI7800"u8) &&
        BinaryPrimitives.ReadUInt32BigEndian(data.Slice(49, 4)) == 16384 &&
        Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(data[128..])) ==
            "4DF3657F57E3086BEE26FB5C6EA946C509D08553";

    // MAME hash/a7800.xml sumgames: exact payload identity, not a filename heuristic.
    public static bool HasKnownMissingRam(ReadOnlySpan<byte> data) =>
        data.Length == 131072 + 128 && data[0] is >= 1 and <= 3 &&
        data.Slice(1, 9).SequenceEqual("ATARI7800"u8) &&
        BinaryPrimitives.ReadUInt32BigEndian(data.Slice(49, 4)) == 131072 &&
        BinaryPrimitives.ReadUInt16BigEndian(data.Slice(53, 2)) == 2 &&
        Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(data[128..])) is
            "31D46B32A2AA8D5BD6028CFF1F41037426B22409" or
            "B3725D6F0834C68AC62CE13EB9EB0D01D1124E5E";

    static void ValidateSize(int size)
    {
        if (size is not (16384 or 32768 or 49152))
            throw new InvalidDataException("Only linear 16/32/48 KiB ROMs are implemented.");
    }
}
