namespace ThingRetroEmu.Media;

// A 1541 disk as a .d64 file: 35 tracks of 17 to 21 sectors of 256 bytes, one after the other (174,848 bytes; 175,531 with
// the error byte of each sector, which are ignored)
public sealed class D64Image
{
    public const int TRACKS = 35;
    public const int SECTOR_SIZE = 256;
    public const int SIZE = 683 * SECTOR_SIZE;
    public const int SIZE_WITH_ERRORS = SIZE + 683;

    readonly byte[] data;
    readonly int[] firstSector = new int[TRACKS + 2];

    public D64Image(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length != SIZE && content.Length != SIZE_WITH_ERRORS)
            throw new InvalidDataException($"A .d64 file has {SIZE} bytes (or {SIZE_WITH_ERRORS} with the errors), not {content.Length}");
        data = content;
        for (int track = 1; track <= TRACKS; track++) firstSector[track + 1] = firstSector[track] + SectorsPerTrack(track);
    }

    public static D64Image Blank() => new(new byte[SIZE]);

    public byte[] Data => data;
    public bool WriteProtected { get; set; }
    public bool Modified { get; private set; }
    public void MarkSaved() => Modified = false;

    public static int SectorsPerTrack(int track) => track switch
    {
        <= 0 => throw new ArgumentOutOfRangeException(nameof(track)),
        <= 17 => 21,
        <= 24 => 19,
        <= 30 => 18,
        <= TRACKS => 17,
        _ => throw new ArgumentOutOfRangeException(nameof(track)),
    };

    // The speed zone of a track: bytes of the track go by every 32 - 2 * zone cycles (1 MHz)
    public static int SpeedZone(int track) => track <= 17 ? 3 : track <= 24 ? 2 : track <= 30 ? 1 : 0;

    // Bytes in the track at 300 rpm: 200,000 cycles of the drive per turn
    public static int TrackBytes(int track) => 200_000 / (32 - 2 * SpeedZone(track));

    int Offset(int track, int sector)
    {
        if (track is < 1 or > TRACKS || sector < 0 || sector >= SectorsPerTrack(track)) return -1;
        return (firstSector[track] + sector) * SECTOR_SIZE;
    }

    public bool ReadSector(int track, int sector, Span<byte> buffer)
    {
        int offset = Offset(track, sector);
        if (offset < 0) return false;
        data.AsSpan(offset, SECTOR_SIZE).CopyTo(buffer);
        return true;
    }

    public bool WriteSector(int track, int sector, ReadOnlySpan<byte> bytes)
    {
        int offset = Offset(track, sector);
        if (offset < 0 || WriteProtected) return false;
        bytes[..SECTOR_SIZE].CopyTo(data.AsSpan(offset, SECTOR_SIZE));
        Modified = true;
        return true;
    }

    (byte, byte)? headerId;

    // The two bytes of the disk ID that every sector header carries. A disk made by D64Writer, or read from a file, has the
    // ones in the BAM (track 18 sector 0, offsets $A2 and $A3); once the drive has formatted tracks it is the ID it wrote in the
    // headers, which the BAM only gets later
    public (byte Id1, byte Id2) Id
    {
        get
        {
            if (headerId is { } id) return id;
            int bam = Offset(18, 0);
            return (data[bam + 0xA2], data[bam + 0xA3]);
        }
        set => headerId = value;
    }
}
