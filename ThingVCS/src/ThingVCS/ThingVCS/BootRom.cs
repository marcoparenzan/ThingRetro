namespace ThingVCS;

public interface IBootRomPinout { bool Selects(ushort address); byte Read(ushort address); }

/// <summary>Read-only externally supplied BIOS; no copyrighted firmware bundled.</summary>
public sealed class BootRom : IBootRomPinout
{
    readonly byte[] bytes;
    public int Size => bytes.Length;
    public BootRom(ReadOnlySpan<byte> data)
    {
        if (data.Length is not (4096 or 16384)) throw new ArgumentException("Supported BIOS sizes: 4 or 16 KiB.", nameof(data));
        bytes = data.ToArray();
    }
    public bool Selects(ushort address) => address >= 65536 - bytes.Length;
    public byte Read(ushort address) => Selects(address) ? bytes[address - (65536 - bytes.Length)] : (byte)0xff;
}

public sealed class Vcs2600LinearCartridge : ICartridgePinout
{
    readonly byte[] bytes;
    public Vcs2600LinearCartridge(ReadOnlySpan<byte> data)
    {
        if (data.Length is not (2048 or 4096)) throw new ArgumentException("2600 linear cartridges must be 2 or 4 KiB.", nameof(data));
        bytes = data.ToArray();
    }
    public byte Read(ushort address) => bytes[address & (bytes.Length - 1)];
    public void Write(ushort address, byte value) { }
    public void Reset() { }
}
