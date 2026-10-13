using System.Buffers.Binary;
using System.Text;
using Thing64.C64;

namespace Thing64.Crt;

// Reading of the .crt format (VICE): "C64 CARTRIDGE   " header followed by "CHIP" packets with the ROMs.
// All numeric fields are big-endian
public static class CrtFile
{
    const string SIGNATURE = "C64 CARTRIDGE   ";
    const int CHIP_HEADER_SIZE = 0x10;

    public const int TYPE_NORMAL = 0;
    public const int TYPE_OCEAN = 5;
    public const int TYPE_SYSTEM3 = 15;
    public const int TYPE_MAGIC_DESK = 19;
    public const int TYPE_EASYFLASH = 32;

    public record Chip(int Type, int Bank, int LoadAddress, byte[] Data);

    public static Cartridge Load(string path) => Load(File.ReadAllBytes(path));

    public static Cartridge Load(byte[] crt)
    {
        if (crt.Length < 0x40 || Encoding.ASCII.GetString(crt, 0, 16) != SIGNATURE)
            throw new InvalidDataException("Not a .crt file: the \"C64 CARTRIDGE\" header is missing");

        int headerLength = (int)BinaryPrimitives.ReadUInt32BigEndian(crt.AsSpan(0x10));
        int hardwareType = BinaryPrimitives.ReadUInt16BigEndian(crt.AsSpan(0x16));
        bool exrom = crt[0x18] != 0; // 0 = line active (low)
        bool game = crt[0x19] != 0;
        string name = Encoding.ASCII.GetString(crt, 0x20, 32).TrimEnd('\0', ' ');

        var chips = ReadChips(crt, Math.Max(headerLength, 0x40));
        if (chips.Count == 0)
            throw new InvalidDataException("The .crt file contains no ROM (no CHIP packet)");

        return hardwareType switch
        {
            TYPE_NORMAL => new NormalCartridge(name, exrom, game, Roml(chips), Romh(chips)),
            TYPE_OCEAN => new OceanCartridge(name, exrom, game, ByBank(chips)),
            TYPE_SYSTEM3 => new System3Cartridge(name, ByBank(chips)),
            TYPE_MAGIC_DESK => new MagicDeskCartridge(name, ByBank(chips)),
            TYPE_EASYFLASH => new EasyFlashCartridge(name, Roml(chips), Romh(chips)),
            _ => throw new NotSupportedException($"Cartridge type {hardwareType} is not supported " +
                 "(supported: 0 Normal, 5 Ocean, 15 System 3, 19 Magic Desk, 32 EasyFlash)"),
        };
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

    static byte[][] NewBanks(List<Chip> chips)
    {
        int count = chips.Max(c => c.Bank) + 1;
        var banks = new byte[count][];
        for (int i = 0; i < count; i++)
        {
            banks[i] = new byte[BankedCartridge.BANK_SIZE];
            Array.Fill(banks[i], (byte)0xFF);
        }
        return banks;
    }

    // Spreads the bytes of each chip by address: $8000-$9FFF into ROML, $A000-$BFFF and $E000-$FFFF into ROMH
    // (a 16 KB chip at $8000 fills both)
    static byte[][] Roml(List<Chip> chips) => Place(chips, romh: false);
    static byte[][] Romh(List<Chip> chips) => Place(chips, romh: true);

    static byte[][] Place(List<Chip> chips, bool romh)
    {
        var banks = NewBanks(chips);
        foreach (var chip in chips)
        {
            for (int i = 0; i < chip.Data.Length; i++)
            {
                int address = chip.LoadAddress + i;
                bool isRomh = address >= 0xA000;
                if (isRomh != romh || address > 0xFFFF) continue;
                if (address >= 0xC000 && address < 0xE000) continue;
                banks[chip.Bank][address & 0x1FFF] = chip.Data[i];
            }
        }
        return banks;
    }

    // For the types where the selected bank applies to both ROML and ROMH: one 8 KB bank per chip
    static byte[][] ByBank(List<Chip> chips)
    {
        var banks = NewBanks(chips);
        foreach (var chip in chips)
            Array.Copy(chip.Data, banks[chip.Bank], Math.Min(chip.Data.Length, BankedCartridge.BANK_SIZE));
        return banks;
    }
}
