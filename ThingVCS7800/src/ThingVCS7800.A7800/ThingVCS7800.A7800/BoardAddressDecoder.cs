namespace ThingVCS7800.A7800;

public enum BoardChipSelect { None, Tia, Maria, RiotRegisters, RiotRam, MainRam, Cartridge }
public readonly record struct BoardAddressSelection(BoardChipSelect Chip, int Offset)
{
    public bool Slow => Chip is BoardChipSelect.Tia or BoardChipSelect.RiotRegisters or BoardChipSelect.RiotRam;
}

/// <summary>7800 development-mode decoder. BIOS/2600 mode and revision-dependent $2800 alias are not simulated.</summary>
public static class BoardAddressDecoder
{
    public static BoardAddressSelection Decode2600(ushort cpuAddress)
    {
        int address = cpuAddress & 0x1fff;
        if ((address & 0x1000) != 0) return new(BoardChipSelect.Cartridge, address);
        if ((address & 0x80) == 0) return new(BoardChipSelect.Tia, address & 0x3f);
        if ((address & 0x200) == 0) return new(BoardChipSelect.RiotRam, address & 127);
        return new(BoardChipSelect.RiotRegisters, address & 31);
    }
    public static BoardAddressSelection Decode(ushort address)
    {
        // TIA/MARIA ignore A8/A9; only the lowest 64 bytes of each page select them.
        if (address < 0x400 && (address & 0xff) < 0x40)
            return new((address & 0x20) == 0 ? BoardChipSelect.Tia : BoardChipSelect.Maria, address & 31);
        // RIOT register selects ignore A5/A6/A8, but preserve A7 and A9.
        if ((address & ~0x160) is >= 0x280 and <= 0x29f)
            return new(BoardChipSelect.RiotRegisters, address & 31);
        if ((address & ~0x100) is >= 0x480 and <= 0x4ff)
            return new(BoardChipSelect.RiotRam, address & 127);
        // RAM under the low I/O overlay remains accessible at $2000/$2100.
        if (address <= 0x1ff)
            return new(BoardChipSelect.MainRam, 0x800 + address);
        if (address is >= 0x1800 and <= 0x27ff)
            return new(BoardChipSelect.MainRam, address - 0x1800);
        if (address >= 0x4000)
            return new(BoardChipSelect.Cartridge, address);
        return new(BoardChipSelect.None, 0);
    }
}
