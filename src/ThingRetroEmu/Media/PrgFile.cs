namespace ThingRetroEmu.Media;

/// <summary>
/// A <c>.prg</c> file of the Commodore machines: the first two bytes are the load address (little-endian), the rest is the data.
/// It knows the format only; putting the bytes into a machine's RAM is up to the machine (Thing64 and Thing65 have their own loaders)
/// </summary>
public static class PrgFile
{
    /// <summary>Addresses taken by the program (<c>End</c> excluded). Throws <see cref="ArgumentException"/> if the file is not valid.</summary>
    public static (ushort Start, int End) GetRange(byte[] prg)
    {
        ArgumentNullException.ThrowIfNull(prg);
        if (prg.Length < 3)
            throw new ArgumentException("A .prg file holds at least the load address and one data byte", nameof(prg));

        ushort start = (ushort)(prg[0] | prg[1] << 8);
        int end = start + prg.Length - 2;
        if (end > 0x10000)
            throw new ArgumentException($"The program (${start:X4}, {prg.Length - 2} bytes) goes past the end of memory", nameof(prg));

        return (start, end);
    }

    /// <summary>The data without the load address.</summary>
    public static ReadOnlySpan<byte> Data(byte[] prg) => prg.AsSpan(2);

    /// <summary>A .prg: the load address followed by the data.</summary>
    public static byte[] Build(ushort loadAddress, ReadOnlySpan<byte> data)
    {
        var prg = new byte[data.Length + 2];
        prg[0] = (byte)loadAddress;
        prg[1] = (byte)(loadAddress >> 8);
        data.CopyTo(prg.AsSpan(2));
        return prg;
    }
}
