namespace ThingRetroEmu.Media;

// Makes .d64 disks with programs on them: the format of the 1541 DOS (BAM at 18/0, directory at 18/1). Programs go on the
// tracks from the first one up, skipping the directory track, one sector after the other
public static class D64Writer
{
    // name: the disk name; files: programs by name with their content (including the two bytes of the load address)
    public static D64Image Create(string name, IEnumerable<(string Name, byte[] Content)> files, string id = "65")
    {
        var disk = D64Image.Blank();
        var bam = new byte[256];
        var directory = new byte[256];
        bam[0] = 18; bam[1] = 1; bam[2] = 0x41;
        for (int track = 1; track <= D64Image.TRACKS; track++)
        {
            int sectors = D64Image.SectorsPerTrack(track);
            bam[4 * track] = (byte)sectors;
            for (int sector = 0; sector < sectors; sector++) bam[4 * track + 1 + sector / 8] |= (byte)(1 << sector % 8);
        }
        void Use(int track, int sector)
        {
            bam[4 * track]--;
            bam[4 * track + 1 + sector / 8] &= (byte)~(1 << sector % 8);
        }
        Use(18, 0);
        Use(18, 1);

        Array.Fill(bam, (byte)0xA0, 0x90, 0x1C);
        PetsciiName(name).CopyTo(bam, 0x90);
        bam[0xA2] = (byte)id[0];
        bam[0xA3] = (byte)id[1];
        bam[0xA5] = (byte)'2';
        bam[0xA6] = (byte)'A';

        int nextTrack = 1, nextSector = 0, entry = 0;
        (int Track, int Sector) Allocate()
        {
            if (nextTrack == 18) nextTrack = 19;
            if (nextTrack > D64Image.TRACKS) throw new InvalidOperationException("The disk is full");
            var result = (nextTrack, nextSector);
            Use(nextTrack, nextSector);
            if (++nextSector == D64Image.SectorsPerTrack(nextTrack))
            {
                nextSector = 0;
                nextTrack++;
            }
            return result;
        }

        directory[0] = 0;
        directory[1] = 0xFF;
        foreach (var (fileName, content) in files)
        {
            if (entry == 8) throw new InvalidOperationException("The directory sector is full");
            int blocks = Math.Max(1, (content.Length + 253) / 254);
            var (firstTrack, firstSector) = Allocate();
            var (track, sector) = (firstTrack, firstSector);
            for (int block = 0; block < blocks; block++)
            {
                var data = new byte[256];
                int offset = block * 254;
                int length = Math.Min(254, content.Length - offset);
                Array.Copy(content, offset, data, 2, length);
                if (block == blocks - 1)
                {
                    data[0] = 0;
                    data[1] = (byte)(length + 1);
                    disk.WriteSector(track, sector, data);
                }
                else
                {
                    var (t, s) = Allocate();
                    data[0] = (byte)t;
                    data[1] = (byte)s;
                    disk.WriteSector(track, sector, data);
                    (track, sector) = (t, s);
                }
            }

            int at = entry * 32;
            directory[at + 2] = 0x82;                       // PRG, closed
            directory[at + 3] = (byte)firstTrack;
            directory[at + 4] = (byte)firstSector;
            Array.Fill(directory, (byte)0xA0, at + 5, 16);
            PetsciiName(fileName).CopyTo(directory, at + 5);
            directory[at + 30] = (byte)(blocks & 0xFF);
            directory[at + 31] = (byte)(blocks >> 8);
            entry++;
        }

        disk.WriteSector(18, 0, bam);
        disk.WriteSector(18, 1, directory);
        disk.MarkSaved();
        return disk;
    }

    // Names in the directory are capitals (the same codes in ASCII and PETSCII), at most 16 characters
    static byte[] PetsciiName(string name) => System.Text.Encoding.ASCII.GetBytes(name.ToUpperInvariant()).Take(16).ToArray();
}
