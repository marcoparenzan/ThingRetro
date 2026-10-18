namespace ThingRetroEmu.Media;

// A cassette as a .tap file: the signal as the lengths of its pulses, from one falling edge to the next. The header is
// "C64-TAPE-RAW", the version (0 or 1), three bytes, and the size of the data. Each data byte is a length in units of 8 cycles of
// the computer's clock; a zero is an overflow: in version 0 it is 256 units, in version 1 the next three bytes (little endian)
// are the length in cycles
public sealed class TapImage
{
    public const string SIGNATURE = "C64-TAPE-RAW";
    const int HEADER = 20;

    readonly List<int> pulses = [];       // in cycles

    public TapImage() { }

    public TapImage(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length < HEADER || System.Text.Encoding.ASCII.GetString(content, 0, 12) != SIGNATURE)
            throw new InvalidDataException("Not a .tap file: the header C64-TAPE-RAW is missing");
        int version = content[12];
        if (version > 1) throw new InvalidDataException($"The version {version} of the .tap format is not supported (0 and 1 are)");
        int size = BitConverter.ToInt32(content, 16);
        int end = Math.Min(content.Length, HEADER + size);
        for (int i = HEADER; i < end; i++)
        {
            int value = content[i];
            if (value != 0) pulses.Add(value * 8);
            else if (version == 0) pulses.Add(256 * 8);
            else if (i + 3 < end)
            {
                pulses.Add(content[i + 1] | content[i + 2] << 8 | content[i + 3] << 16);
                i += 3;
            }
        }
    }

    // The lengths of the pulses, in cycles
    public IReadOnlyList<int> Pulses => pulses;
    public int Count => pulses.Count;
    public bool Modified { get; private set; }
    public void MarkSaved() => Modified = false;

    public long TotalCycles
    {
        get
        {
            long total = 0;
            foreach (int pulse in pulses) total += pulse;
            return total;
        }
    }

    // A pulse recorded from the write line: length in cycles
    public void Append(int cycles)
    {
        pulses.Add(Math.Max(1, cycles));
        Modified = true;
    }

    // Overwrites from a pulse on (a recording that starts in the middle of the tape erases what follows)
    public void TruncateAt(int index)
    {
        if (index < pulses.Count)
        {
            pulses.RemoveRange(index, pulses.Count - index);
            Modified = true;
        }
    }

    // The file, version 1
    public byte[] ToBytes()
    {
        var data = new List<byte>();
        foreach (int cycles in pulses)
        {
            int units = (cycles + 4) / 8;
            if (units is >= 1 and <= 255) data.Add((byte)units);
            else
            {
                data.Add(0);
                data.Add((byte)(cycles & 0xFF));
                data.Add((byte)(cycles >> 8 & 0xFF));
                data.Add((byte)(cycles >> 16 & 0xFF));
            }
        }
        var file = new byte[HEADER + data.Count];
        System.Text.Encoding.ASCII.GetBytes(SIGNATURE).CopyTo(file, 0);
        file[12] = 1;
        BitConverter.GetBytes(data.Count).CopyTo(file, 16);
        data.CopyTo(file, HEADER);
        return file;
    }
}
