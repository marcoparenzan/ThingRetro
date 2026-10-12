using System.Buffers.Binary;
using System.Text;

namespace Fake64.Samples;

// Scrive un file .crt (formato VICE): intestazione di 64 byte e un pacchetto CHIP per ogni banco.
// I campi numerici sono big-endian; EXROM e GAME indicano il livello delle linee (0 = attiva)
public static class CrtImage
{
    public const int TypeNormal = 0;

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

    // ROM di 8 KB: il codice all'inizio, il resto a $FF come una EPROM vuota
    public static byte[] Rom8K(byte[] code)
    {
        if (code.Length > 0x2000) throw new ArgumentException("Il codice non entra in 8 KB", nameof(code));
        var rom = new byte[0x2000];
        Array.Fill(rom, (byte)0xFF);
        code.CopyTo(rom, 0);
        return rom;
    }
}
