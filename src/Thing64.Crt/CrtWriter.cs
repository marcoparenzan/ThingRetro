using System.Buffers.Binary;
using System.Text;

namespace Thing64.Crt;

// Writes a .crt file (VICE format): 64-byte header and one CHIP packet per bank.
// Numeric fields are big-endian; EXROM and GAME give the level of the lines (0 = active)
public static class CrtWriter
{
    public const int TypeNormal = 0;
    public const int TypeMagicDesk = 19;

    public static byte[] Build(string name, int hardwareType, bool exromActive, bool gameActive, params (int Bank, ushort LoadAddress, byte[] Rom)[] chips)
    {
        var crt = new List<byte>();
        var header = new byte[0x40];
        Encoding.ASCII.GetBytes("C64 CARTRIDGE   ").CopyTo(header, 0);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0x10), 0x40);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(0x14), 0x0100);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(0x16), (ushort)hardwareType);
        header[0x18] = (byte)(exromActive ? 0 : 1);
        header[0x19] = (byte)(gameActive ? 0 : 1);
        Encoding.ASCII.GetBytes(name.ToUpperInvariant()).AsSpan(0, Math.Min(name.Length, 32)).CopyTo(header.AsSpan(0x20));
        crt.AddRange(header);

        foreach (var (bank, loadAddress, rom) in chips)
        {
            var chip = new byte[0x10];
            Encoding.ASCII.GetBytes("CHIP").CopyTo(chip, 0);
            BinaryPrimitives.WriteUInt32BigEndian(chip.AsSpan(4), (uint)(0x10 + rom.Length));
            BinaryPrimitives.WriteUInt16BigEndian(chip.AsSpan(8), 0); // ROM
            BinaryPrimitives.WriteUInt16BigEndian(chip.AsSpan(10), (ushort)bank);
            BinaryPrimitives.WriteUInt16BigEndian(chip.AsSpan(12), loadAddress);
            BinaryPrimitives.WriteUInt16BigEndian(chip.AsSpan(14), (ushort)rom.Length);
            crt.AddRange(chip);
            crt.AddRange(rom);
        }
        return crt.ToArray();
    }

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
