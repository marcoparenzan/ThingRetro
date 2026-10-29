namespace ThingNES;

public enum Mirroring { Horizontal, Vertical }

// Standard iNES NROM, UxROM and CNROM board variants. Reject ambiguous/unsupported layouts.
public sealed class Cartridge
{
    readonly byte[] prg, chr;
    readonly bool chrRam, busConflicts;
    readonly byte[] ram = new byte[8192];
    int selectedBank;
    public Mirroring Mirroring { get; }
    public int MapperNumber { get; }
    public string MapperName => MapperNumber switch { 0 => "NROM", 2 => "UxROM", 3 => "CNROM", _ => throw new InvalidOperationException() };
    public int PrgSize => prg.Length;
    public int ChrSize => chr.Length;
    public bool HasChrRam => chrRam;
    public bool BusConflicts => busConflicts && MapperNumber != 0;
    public int SelectedBank => selectedBank;

    private Cartridge(byte[] prg, byte[] chr, bool chrRam, Mirroring mirroring, int mapper, bool busConflicts)
        => (this.prg, this.chr, this.chrRam, Mirroring, MapperNumber, this.busConflicts) = (prg, chr, chrRam, mirroring, mapper, busConflicts);

    public static Cartridge Load(byte[] image, bool busConflicts = true)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Length < 16 || !image.AsSpan(0, 4).SequenceEqual("NES\u001a"u8))
            throw new InvalidDataException("Invalid iNES header.");
        if ((image[7] & 12) == 8) throw new NotSupportedException("NES 2.0 is not supported yet.");
        if ((image[7] & 3) != 0) throw new NotSupportedException("Only standard NES consoles are supported.");
        int mapper = (image[6] >> 4) | (image[7] & 0xf0);
        if (mapper is not (0 or 2 or 3)) throw new NotSupportedException($"Mapper {mapper} is not supported; use NROM (0), UxROM (2) or CNROM (3).");
        if ((image[6] & 8) != 0) throw new NotSupportedException("Four-screen boards are not supported.");
        int prgBanks = image[4], chrBanks = image[5];
        bool powerOfTwo = prgBanks > 0 && (prgBanks & (prgBanks - 1)) == 0;
        bool valid = mapper switch
        {
            0 => prgBanks is 1 or 2 && chrBanks <= 1,
            2 => powerOfTwo && prgBanks is >= 2 and <= 16 && chrBanks == 0,
            3 => prgBanks is 1 or 2 && chrBanks is 1 or 2 or 4,
            _ => false
        };
        if (!valid) throw new InvalidDataException($"Unsupported memory layout for mapper {mapper}: {prgBanks} PRG banks, {chrBanks} CHR banks.");
        int offset = 16 + ((image[6] & 4) != 0 ? 512 : 0);
        int prgLength = prgBanks * 16384, chrLength = chrBanks * 8192;
        if (image.Length < offset + prgLength + chrLength) throw new InvalidDataException("Truncated ROM.");
        var cart = new Cartridge(image.AsSpan(offset, prgLength).ToArray(),
            chrLength == 0 ? new byte[8192] : image.AsSpan(offset + prgLength, chrLength).ToArray(),
            chrLength == 0, (image[6] & 1) != 0 ? Mirroring.Vertical : Mirroring.Horizontal, mapper, busConflicts);
        if ((image[6] & 4) != 0) image.AsSpan(16, 512).CopyTo(cart.ram.AsSpan(0x1000));
        return cart;
    }

    public byte ReadCpu(ushort address)
    {
        if (address < 0x6000) return 0;
        if (address < 0x8000) return ram[address - 0x6000];
        if (MapperNumber == 2)
        {
            int bank = address < 0xc000 ? selectedBank : prg.Length / 16384 - 1;
            return prg[bank * 16384 + (address & 0x3fff)];
        }
        return prg[(address - 0x8000) % prg.Length];
    }
    public void WriteCpu(ushort address, byte value)
    {
        if (address is >= 0x6000 and < 0x8000) { ram[address - 0x6000] = value; return; }
        if (address < 0x8000 || MapperNumber == 0) return;
        if (busConflicts) value &= ReadCpu(address); // ROM and CPU drive the data lines together.
        selectedBank = MapperNumber == 2 ? value & (prg.Length / 16384 - 1) : (value & 3) & (chr.Length / 8192 - 1);
    }
    public byte ReadChr(int address) => chr[(MapperNumber == 3 ? selectedBank * 8192 : 0) + (address & 0x1fff)];
    public void WriteChr(int address, byte value) { if (chrRam) chr[address & 0x1fff] = value; }
}
