namespace ThingVCS;

/// <summary>Linear ROM with cartridge RAM at $4000-$7FFF; RAM address A8 is disconnected.</summary>
public sealed class MirroredRamCartridge : ICartridgePinout
{
    readonly LinearCartridge rom;
    readonly byte[] ram = new byte[16384];
    public MirroredRamCartridge(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length is not (16384 or 32768)) throw new ArgumentException("Mirrored RAM cartridge requires 16/32 KiB ROM.", nameof(bytes));
        rom = new(bytes);
    }
    public byte Read(ushort address) => address is >= 0x4000 and < 0x8000
        ? ram[(address - 0x4000) & 0x3eff] : rom.Read(address);
    public void Write(ushort address, byte value)
    {
        if (address is >= 0x4000 and < 0x8000) ram[(address - 0x4000) & 0x3eff] = value;
    }
    public void Reset() { } // A reset does not erase cartridge RAM.
}
