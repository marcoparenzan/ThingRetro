namespace ThingVCS;

/// <summary>Absolute 64 KiB cartridge, logical bus connector; no name-based mapper inference.</summary>
public sealed class AbsoluteCartridge : ICartridgePinout
{
    readonly byte[] rom;
    public int SelectedBank { get; private set; }
    public AbsoluteCartridge(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 65536) throw new ArgumentException("Absolute requires 64 KiB ROM.", nameof(bytes));
        rom = bytes.ToArray();
    }
    public byte Read(ushort address) => address < 0x4000 ? (byte)0xff
        : address < 0x8000 ? rom[SelectedBank * 16384 + (address & 0x3fff)] : rom[address - 0x8000 + 32768];
    public void Write(ushort address, byte value)
    {
        if (address != 0x8000) return;
        if ((value & 1) != 0) SelectedBank = 0;
        else if ((value & 2) != 0) SelectedBank = 1;
    }
    public void Reset() => SelectedBank = 0;
}
