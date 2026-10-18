using System.Buffers.Binary;
using System.Text;

namespace ThingRetroEmu.Media;

/// <summary>
/// A cartridge as a <c>.crt</c> file (the VICE format): a 64-byte header and one CHIP packet per bank of ROM. All numeric fields are big-endian.
/// This class reads and writes the container; what the hardware type means (how the banks are switched) is up to the machine
/// </summary>
public sealed class CrtImage
{
    public const string SIGNATURE = "C64 CARTRIDGE   ";
    const int HEADER_SIZE = 0x40;
    const int CHIP_HEADER_SIZE = 0x10;

    /// <summary>One CHIP packet: type (0 = ROM), bank, the address the chip is seen at, and its bytes.</summary>
    public sealed record Chip(int Type, int Bank, int LoadAddress, byte[] Data);

    public CrtImage(string name, int hardwareType, bool exromHigh, bool gameHigh, IReadOnlyList<Chip> chips)
    {
        Name = name;
        HardwareType = hardwareType;
        ExromHigh = exromHigh;
        GameHigh = gameHigh;
        Chips = chips;
    }

    public string Name { get; }
    public int HardwareType { get; }

    /// <summary>The level of the EXROM line in the header (0 = line active/low).</summary>
    public bool ExromHigh { get; }

    /// <summary>The level of the GAME line in the header (0 = line active/low).</summary>
    public bool GameHigh { get; }

    public IReadOnlyList<Chip> Chips { get; }

    public static CrtImage Parse(byte[] crt)
    {
        ArgumentNullException.ThrowIfNull(crt);
        if (crt.Length < HEADER_SIZE || Encoding.ASCII.GetString(crt, 0, 16) != SIGNATURE)
            throw new InvalidDataException("Not a .crt file: the \"C64 CARTRIDGE\" header is missing");

        int headerLength = (int)BinaryPrimitives.ReadUInt32BigEndian(crt.AsSpan(0x10));
        int hardwareType = BinaryPrimitives.ReadUInt16BigEndian(crt.AsSpan(0x16));
        string name = Encoding.ASCII.GetString(crt, 0x20, 32).TrimEnd('\0', ' ');
        var chips = ReadChips(crt, Math.Max(headerLength, HEADER_SIZE));
        if (chips.Count == 0)
            throw new InvalidDataException("The .crt file contains no ROM (no CHIP packet)");
        return new CrtImage(name, hardwareType, exromHigh: crt[0x18] != 0, gameHigh: crt[0x19] != 0, chips);
    }

    static List<Chip> ReadChips(byte[] crt, int offset)
    {
        var chips = new List<Chip>();
        while (offset + CHIP_HEADER_SIZE <= crt.Length)
        {
            if (Encoding.ASCII.GetString(crt, offset, 4) != "CHIP")
                throw new InvalidDataException($"Invalid CHIP packet at offset ${offset:X}");

            int packetLength = (int)BinaryPrimitives.ReadUInt32BigEndian(crt.AsSpan(offset + 4));
            int type = BinaryPrimitives.ReadUInt16BigEndian(crt.AsSpan(offset + 8));
            int bank = BinaryPrimitives.ReadUInt16BigEndian(crt.AsSpan(offset + 10));
            int loadAddress = BinaryPrimitives.ReadUInt16BigEndian(crt.AsSpan(offset + 12));
            int size = BinaryPrimitives.ReadUInt16BigEndian(crt.AsSpan(offset + 14));

            if (offset + CHIP_HEADER_SIZE + size > crt.Length || packetLength < CHIP_HEADER_SIZE)
                throw new InvalidDataException($"Truncated CHIP packet at offset ${offset:X}");

            chips.Add(new Chip(type, bank, loadAddress, crt.AsSpan(offset + CHIP_HEADER_SIZE, size).ToArray()));
            offset += packetLength;
        }
        return chips;
    }

    /// <summary>The file: header (version 1.0) and one CHIP packet per chip.</summary>
    public byte[] ToBytes()
    {
        var crt = new List<byte>();
        var header = new byte[HEADER_SIZE];
        Encoding.ASCII.GetBytes(SIGNATURE).CopyTo(header, 0);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0x10), HEADER_SIZE);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(0x14), 0x0100);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(0x16), (ushort)HardwareType);
        header[0x18] = (byte)(ExromHigh ? 1 : 0);
        header[0x19] = (byte)(GameHigh ? 1 : 0);
        var name = Encoding.ASCII.GetBytes(Name.ToUpperInvariant());
        name.AsSpan(0, Math.Min(name.Length, 32)).CopyTo(header.AsSpan(0x20));
        crt.AddRange(header);

        foreach (var chip in Chips)
        {
            var packet = new byte[CHIP_HEADER_SIZE];
            Encoding.ASCII.GetBytes("CHIP").CopyTo(packet, 0);
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(4), (uint)(CHIP_HEADER_SIZE + chip.Data.Length));
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(8), (ushort)chip.Type);
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(10), (ushort)chip.Bank);
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(12), (ushort)chip.LoadAddress);
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(14), (ushort)chip.Data.Length);
            crt.AddRange(packet);
            crt.AddRange(chip.Data);
        }
        return crt.ToArray();
    }
}
