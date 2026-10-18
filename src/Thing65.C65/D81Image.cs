using ThingMOS;
using ThingRetroEmu.Chips;
using ThingRetroEmu.Buses;

namespace Thing65.C65;

// A 3.5" disk image in the .d81 format: 80 tracks, 2 sides, 10 sectors of 512 bytes (800 KB). The file holds the
// cylinders one after the other, side 0 then side 1 in each, sectors numbered from 1.
// A track can be unformatted (a blank disk, or a track the F011 just erased to format it): its sectors are not found
// until a formatting writes their headers. Disks read from a file and those the DOS formats are formatted.
// Modified tells that the disk was written to since it was loaded or last saved
public sealed class D81Image : IDiskMedia
{
    public const int TRACKS = 80;
    public const int SIDES = 2;
    public const int SECTORS = 10;
    public const int SECTOR_SIZE = 512;
    public const int SIZE = TRACKS * SIDES * SECTORS * SECTOR_SIZE;

    readonly byte[] data;
    readonly bool[] present = new bool[TRACKS * SIDES * SECTORS]; // the sectors that have a header

    public D81Image(byte[] data, bool writeProtected = false, bool formatted = true)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length != SIZE) throw new ArgumentException($"A .d81 image is {SIZE} bytes, not {data.Length}", nameof(data));
        this.data = data;
        WriteProtected = writeProtected;
        Array.Fill(present, formatted);
    }

    // A disk that has never been formatted: the DOS needs HEADER (or NEW) before it can use it
    public static D81Image Blank() => new(new byte[SIZE], formatted: false);

    public bool WriteProtected { get; }
    public int SectorsPerTrack => SECTORS;
    public bool Modified { get; private set; }

    // The contents, for saving after the DOS wrote to the disk
    public byte[] Data => data;

    public void MarkSaved() => Modified = false;

    static int Index(int track, int side, int sector) =>
        track is < 0 or >= TRACKS || side is < 0 or >= SIDES || sector is < 1 or > SECTORS ? -1 : (track * SIDES + side) * SECTORS + sector - 1;

    public bool ReadSector(int track, int side, int sector, Span<byte> buffer)
    {
        int index = Index(track, side, sector);
        if (index < 0 || !present[index]) return false;
        data.AsSpan(index * SECTOR_SIZE, SECTOR_SIZE).CopyTo(buffer);
        return true;
    }

    public bool WriteSector(int track, int side, int sector, ReadOnlySpan<byte> sectorData)
    {
        int index = Index(track, side, sector);
        if (index < 0 || !present[index] || WriteProtected) return false;
        sectorData[..SECTOR_SIZE].CopyTo(data.AsSpan(index * SECTOR_SIZE));
        Modified = true;
        return true;
    }

    public void EraseTrack(int track, int side)
    {
        if (WriteProtected) return;
        for (int sector = 1; sector <= SECTORS; sector++)
        {
            int index = Index(track, side, sector);
            if (index < 0) return;
            present[index] = false;
            data.AsSpan(index * SECTOR_SIZE, SECTOR_SIZE).Clear();
        }
        Modified = true;
    }

    public bool FormatSector(int track, int side, int sector, ReadOnlySpan<byte> sectorData)
    {
        int index = Index(track, side, sector);
        if (index < 0 || WriteProtected) return false;
        present[index] = true;
        sectorData[..SECTOR_SIZE].CopyTo(data.AsSpan(index * SECTOR_SIZE));
        Modified = true;
        return true;
    }
}
