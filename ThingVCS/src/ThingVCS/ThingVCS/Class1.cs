namespace ThingVCS;

// Logical cartridge connector: address/data and R/W transactions, not an invented DIP pinout.
public interface ICartridgePinout
{
    byte Read(ushort address);
    void Write(ushort address, byte value);
    void Reset() { }
}

/// <summary>Development ROM, mapped at the top of the 64K address space. No mapper or A78 parser.</summary>
public sealed class LinearCartridge : ICartridgePinout
{
    readonly byte[] rom;
    public LinearCartridge(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length is not (16384 or 32768 or 49152))
            throw new ArgumentException("Expected a raw 16, 32 or 48 KiB ROM.", nameof(bytes));
        rom = bytes.ToArray();
    }
    public byte Read(ushort address) => address >= 65536 - rom.Length ? rom[address - (65536 - rom.Length)] : (byte)0xff;
    public void Write(ushort address, byte value) { }
}
