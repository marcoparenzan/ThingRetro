namespace Thing65.C65;

// Makes .d81 disks: formatted, with a directory and the files given. It is the layout of the 1581 / C65 DOS:
// 80 logical tracks of 40 sectors of 256 bytes (a physical 512-byte sector holds two of them); track 40 has the
// header (sector 0), the two BAM sectors (1 and 2) and the directory (from 3); every other sector holds 254 bytes
// of a file and the link to the next one. Used to give a program to the C65 the way it expects: from a disk
public static class D81Writer
{
    const int LogicalTracks = 80, LogicalSectors = 40, SectorSize = 256;
    const int DirectoryTrack = 40;

    // A disk with the given files (name -> contents including the 2-byte load address, as in a .prg)
    public static D81Image Create(string diskName, IReadOnlyList<(string Name, byte[] Prg)> files)
    {
        var data = new byte[D81Image.SIZE];
        var free = new bool[LogicalTracks + 1, LogicalSectors];
        for (int t = 1; t <= LogicalTracks; t++)
            for (int s = 0; s < LogicalSectors; s++) free[t, s] = true;

        Span<byte> Sector(int track, int sector) => data.AsSpan(((track - 1) * LogicalSectors + sector) * SectorSize, SectorSize);

        // The directory track: header, BAM and the first directory sector are in use
        for (int s = 0; s < 4; s++) free[DirectoryTrack, s] = false;

        // Files: sectors from the outside tracks inwards, skipping the directory track
        var entries = new List<(string Name, int Track, int Sector, int Blocks)>();
        int nextTrack = 1, nextSector = 0;
        foreach (var (name, prg) in files)
        {
            int blocks = Math.Max(1, (prg.Length + 253) / 254);
            var chain = new List<(int Track, int Sector)>();
            for (int i = 0; i < blocks; i++)
            {
                while (nextTrack == DirectoryTrack || !free[nextTrack, nextSector])
                {
                    if (++nextSector == LogicalSectors) { nextSector = 0; nextTrack++; }
                    if (nextTrack > LogicalTracks) throw new InvalidOperationException("The files do not fit on a disk");
                }
                chain.Add((nextTrack, nextSector));
                free[nextTrack, nextSector] = false;
            }
            for (int i = 0; i < blocks; i++)
            {
                var sector = Sector(chain[i].Track, chain[i].Sector);
                int offset = i * 254;
                int length = Math.Min(254, prg.Length - offset);
                prg.AsSpan(offset, length).CopyTo(sector[2..]);
                if (i < blocks - 1) { sector[0] = (byte)chain[i + 1].Track; sector[1] = (byte)chain[i + 1].Sector; }
                else { sector[0] = 0; sector[1] = (byte)(length + 1); } // last sector: the index of the last byte used
            }
            entries.Add((name, chain[0].Track, chain[0].Sector, blocks));
        }

        // Header
        var header = Sector(DirectoryTrack, 0);
        header[0] = DirectoryTrack; header[1] = 3;
        header[2] = (byte)'D';
        header.Slice(4, 16).Fill(0xA0);
        PadName(diskName).CopyTo(header[4..]);
        header[20] = header[21] = 0xA0;
        header[22] = (byte)'6'; header[23] = (byte)'5';  // disk ID
        header[24] = 0xA0;
        header[25] = (byte)'3'; header[26] = (byte)'D';  // DOS version and format
        header[27] = header[28] = 0xA0;

        // BAM: two sectors of 40 tracks each: free sector count and a bitmap of 5 bytes per track (1 = free)
        for (int half = 0; half < 2; half++)
        {
            var bam = Sector(DirectoryTrack, 1 + half);
            bam[0] = half == 0 ? (byte)DirectoryTrack : (byte)0;
            bam[1] = half == 0 ? (byte)2 : (byte)0xFF;
            bam[2] = (byte)'D'; bam[3] = (byte)(~'D' & 0xFF);
            bam[4] = (byte)'6'; bam[5] = (byte)'5';
            bam[6] = 0xC0;
            for (int i = 0; i < 40; i++)
            {
                int track = half * 40 + i + 1;
                int count = 0;
                for (int s = 0; s < LogicalSectors; s++)
                {
                    if (!free[track, s]) continue;
                    count++;
                    bam[16 + i * 6 + 1 + s / 8] |= (byte)(1 << (s % 8));
                }
                bam[16 + i * 6] = (byte)count;
            }
        }

        // Directory: one sector (8 entries of 32 bytes), more if needed
        int entryIndex = 0, dirSector = 3;
        while (entryIndex < Math.Max(1, entries.Count))
        {
            var sector = Sector(DirectoryTrack, dirSector);
            bool more = entries.Count - entryIndex > 8;
            sector[0] = more ? (byte)DirectoryTrack : (byte)0;
            sector[1] = more ? (byte)(dirSector + 1) : (byte)0xFF;
            for (int slot = 0; slot < 8 && entryIndex < entries.Count; slot++, entryIndex++)
            {
                var entry = sector.Slice(slot * 32, 32);
                var (name, track, firstSector, blocks) = entries[entryIndex];
                entry[2] = 0x82; // PRG, closed
                entry[3] = (byte)track; entry[4] = (byte)firstSector;
                entry.Slice(5, 16).Fill(0xA0);
                PadName(name).CopyTo(entry[5..]);
                entry[30] = (byte)blocks; entry[31] = (byte)(blocks >> 8);
            }
            if (!more) break;
            for (int s = dirSector + 1; s < dirSector + 2; s++) free[DirectoryTrack, s] = false;
            dirSector++;
        }
        return new D81Image(data);
    }

    // A disk with a single program
    public static D81Image Create(string name, byte[] prg) => Create(name, [(name, prg)]);

    // The name in PETSCII (uppercase), at most 16 characters
    static byte[] PadName(string name)
    {
        var bytes = new byte[Math.Min(16, name.Length)];
        for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)char.ToUpperInvariant(name[i]);
        return bytes;
    }
}
