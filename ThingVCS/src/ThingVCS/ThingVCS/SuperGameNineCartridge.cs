namespace ThingVCS;

/// <summary>144 KiB SuperGame board: extra first bank and eight switchable banks.</summary>
public sealed class SuperGameNineCartridge : ICartridgePinout
{
    readonly byte[] rom;
    public int SelectedBank { get; private set; }
    public SuperGameNineCartridge(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 147456) throw new ArgumentException("Expected nine 16 KiB banks.", nameof(bytes));
        rom = bytes.ToArray();
    }
    public byte Read(ushort address)
    {
        if (address < 0x4000) return 0xff;
        int bank = address < 0x8000 ? 0 : address < 0xc000 ? SelectedBank : 8;
        return rom[bank * 16384 + (address & 0x3fff)];
    }
    public void Write(ushort address, byte value)
    {
        if (address is >= 0x8000 and < 0xc000) SelectedBank = (value & 7) + 1;
    }
    public void Reset() => SelectedBank = 0; // Matches the reference loader's reset latch.
}
