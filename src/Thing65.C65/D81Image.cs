using Thing65.Mos;

namespace Thing65.C65;

// A 3.5" disk image in the .d81 format: 80 tracks, 2 sides, 10 sectors of 512 bytes (800 KB). The file holds the
// cylinders one after the other, side 0 then side 1 in each, sectors numbered from 1
public sealed class D81Image : IDiskMedia
{
    public const int TRACKS = 80;
    public const int SIDES = 2;
    public const int SECTORS = 10;
    public const int SECTOR_SIZE = 512;
    public const int SIZE = TRACKS * SIDES * SECTORS * SECTOR_SIZE;

    readonly byte[] data;

    public D81Image(byte[] data, bool writeProtected = false)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length != SIZE) throw new ArgumentException($"A .d81 image is {SIZE} bytes, not {data.Length}", nameof(data));
        this.data = data;
        WriteProtected = writeProtected;
    }

    // A blank disk (all zeros: it needs formatting by the DOS)
    public static D81Image Blank() => new(new byte[SIZE]);

    public bool WriteProtected { get; }

    // The contents, for saving after the DOS wrote to the disk
    public byte[] Data => data;

    static int Offset(int track, int side, int sector)
    {
        if (track is < 0 or >= TRACKS || side is < 0 or >= SIDES || sector is < 1 or > SECTORS) return -1;
        return ((track * SIDES + side) * SECTORS + sector - 1) * SECTOR_SIZE;
    }

    public bool ReadSector(int track, int side, int sector, Span<byte> buffer)
    {
        int offset = Offset(track, side, sector);
        if (offset < 0) return false;
        data.AsSpan(offset, SECTOR_SIZE).CopyTo(buffer);
        return true;
    }

    public bool WriteSector(int track, int side, int sector, ReadOnlySpan<byte> sectorData)
    {
        int offset = Offset(track, side, sector);
        if (offset < 0 || WriteProtected) return false;
        sectorData[..SECTOR_SIZE].CopyTo(data.AsSpan(offset));
        return true;
    }
}
