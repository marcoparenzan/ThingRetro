using ThingRetroEmu.Media;

namespace ThingCommodore.Cartridges;

// Writes a .crt file (VICE format): 64-byte header and one CHIP packet per bank.
// Numeric fields are big-endian; EXROM and GAME give the level of the lines (0 = active)
public static class CrtWriter
{
    public const int TypeNormal = 0;
    public const int TypeMagicDesk = 19;

    public static byte[] Build(string name, int hardwareType, bool exromActive, bool gameActive, params (int Bank, ushort LoadAddress, byte[] Rom)[] chips) =>
        new CrtImage(name, hardwareType, exromHigh: !exromActive, gameHigh: !gameActive,
            chips.Select(c => new CrtImage.Chip(0, c.Bank, c.LoadAddress, c.Rom)).ToList()).ToBytes();

    // 8 KB ROM: the code at the start, the rest at $FF like a blank EPROM
    public static byte[] Rom8K(byte[] code)
    {
        if (code.Length > 0x2000) throw new ArgumentException("The code does not fit in 8 KB", nameof(code));
        var rom = new byte[0x2000];
        Array.Fill(rom, (byte)0xFF);
        code.CopyTo(rom, 0);
        return rom;
    }
}
