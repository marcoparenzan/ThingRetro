using ThingRetroEmu.Media;

namespace ThingCommodore.Cartridges;

// Reading of the .crt format (VICE): "C64 CARTRIDGE   " header followed by "CHIP" packets with the ROMs.
// All numeric fields are big-endian
public static class CrtFile
{
    public const int TYPE_NORMAL = 0;
    public const int TYPE_OCEAN = 5;
    public const int TYPE_SYSTEM3 = 15;
    public const int TYPE_MAGIC_DESK = 19;
    public const int TYPE_EASYFLASH = 32;

    public static Cartridge Load(string path) => Load(File.ReadAllBytes(path));

    public static Cartridge Load(byte[] crt)
    {
        var image = CrtImage.Parse(crt);   // the container: ThingRetroEmu
        int hardwareType = image.HardwareType;
        bool exrom = image.ExromHigh;      // 0 = line active (low)
        bool game = image.GameHigh;
        string name = image.Name;
        var chips = image.Chips.ToList();

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

    static byte[][] NewBanks(List<CrtImage.Chip> chips)
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
    static byte[][] Roml(List<CrtImage.Chip> chips) => Place(chips, romh: false);
    static byte[][] Romh(List<CrtImage.Chip> chips) => Place(chips, romh: true);

    static byte[][] Place(List<CrtImage.Chip> chips, bool romh)
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
    static byte[][] ByBank(List<CrtImage.Chip> chips)
    {
        var banks = NewBanks(chips);
        foreach (var chip in chips)
            Array.Copy(chip.Data, banks[chip.Bank], Math.Min(chip.Data.Length, BankedCartridge.BANK_SIZE));
        return banks;
    }
}
