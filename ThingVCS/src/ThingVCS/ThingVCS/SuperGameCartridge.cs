namespace ThingVCS;

/// <summary>Functional 64/128 KiB SuperGame board. No POKEY, 9-bank or bus-conflict model.</summary>
public sealed class SuperGameCartridge : ICartridgePinout
{
    readonly byte[] rom;
    readonly byte[]? ram;
    readonly int mask;
    public int SelectedBank { get; private set; }
    public SuperGameCartridge(ReadOnlySpan<byte> bytes, bool withRam = false)
    {
        if (bytes.Length is not (65536 or 131072))
            throw new ArgumentException("SuperGame requires 64 or 128 KiB ROM.", nameof(bytes));
        rom = bytes.ToArray(); mask = rom.Length / 16384 - 1;
        ram = withRam ? new byte[16384] : null;
    }
    public byte Read(ushort address)
    {
        if (address < 0x4000) return 0xff;
        int offset = address & 0x3fff;
        if (address < 0x8000 && ram is not null) return ram[offset];
        // Without RAM, follow the common second-to-last-bank mapping (board variants may differ).
        int bank = address < 0x8000 ? mask - 1 : address < 0xc000 ? SelectedBank : mask;
        return rom[bank * 16384 + offset];
    }
    public void Write(ushort address, byte value)
    {
        if (address is >= 0x4000 and < 0x8000 && ram is not null) ram[address & 0x3fff] = value;
        else if (address is >= 0x8000 and < 0xc000) SelectedBank = value & mask;
    }
    public void Reset() => SelectedBank = 0; // RAM contents are retained on reset.
}
