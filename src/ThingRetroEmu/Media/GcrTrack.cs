namespace ThingRetroEmu.Media;

// A track of a 1541 disk as the head sees it: bytes of GCR code, and where the SYNC marks are (runs of ones that no data can
// contain). Each sector is: SYNC, header (8 bytes in GCR), a gap, SYNC, data block (260 bytes in GCR), a gap
public sealed class GcrTrack
{
    static readonly byte[] Gcr = [0x0A, 0x0B, 0x12, 0x13, 0x0E, 0x0F, 0x16, 0x17, 0x09, 0x19, 0x1A, 0x1B, 0x0D, 0x1D, 0x1E, 0x15];

    const int SYNC_BYTES = 5;
    const int HEADER_GAP = 9;

    public byte[] Bytes { get; }
    public bool[] Sync { get; }
    public int Length => Bytes.Length;

    GcrTrack(int length)
    {
        Bytes = new byte[length];
        Sync = new bool[length];
    }

    public static GcrTrack Encode(D64Image image, int track)
    {
        int length = D64Image.TrackBytes(track);
        int sectors = D64Image.SectorsPerTrack(track);
        var result = new GcrTrack(length);
        var (id1, id2) = image.Id;
        int tailGap = (length - sectors * (SYNC_BYTES + 10 + HEADER_GAP + SYNC_BYTES + 325)) / sectors;
        var sector = new byte[D64Image.SECTOR_SIZE];
        int position = 0;

        void Put(byte value, bool sync = false)
        {
            result.Bytes[position] = value;
            result.Sync[position] = sync;
            position++;
        }
        void PutSync() { for (int i = 0; i < SYNC_BYTES; i++) Put(0xFF, sync: true); }
        void PutGcr(byte[] block) { foreach (byte b in EncodeBlock(block)) Put(b); }

        for (int number = 0; number < sectors; number++)
        {
            image.ReadSector(track, number, sector);

            PutSync();
            PutGcr([0x08, (byte)(number ^ track ^ id2 ^ id1), (byte)number, (byte)track, id2, id1, 0x0F, 0x0F]);
            for (int i = 0; i < HEADER_GAP; i++) Put(0x55);

            PutSync();
            var block = new byte[260];
            block[0] = 0x07;
            byte checksum = 0;
            for (int i = 0; i < sector.Length; i++)
            {
                block[1 + i] = sector[i];
                checksum ^= sector[i];
            }
            block[257] = checksum;
            PutGcr(block);
            for (int i = 0; i < tailGap; i++) Put(0x55);
        }
        while (position < length) Put(0x55);
        return result;
    }

    // SYNC is a run of ones longer than any data can have: the GCR code never gives two $FF bytes in a row, though a single $FF can
    // happen inside data. After the track was written the marks are found again from the bytes
    public void FindSync()
    {
        for (int i = 0; i < Length; i++)
            Sync[i] = Bytes[i] == 0xFF && (Bytes[(i + Length - 1) % Length] == 0xFF || Bytes[(i + 1) % Length] == 0xFF);
    }

    // The sectors the head has left on the track, written back to the disk image: a SYNC mark, a header block that names this
    // track and a sector, then the next SYNC and a data block with the right checksum. Whatever else is on the track (partly
    // written sectors, garbage) is ignored. Returns the number of sectors found
    public int Decode(D64Image image, int track)
    {
        int found = 0;
        for (int start = 0; start < Length; start++)
        {
            if (!EndsSync(start)) continue;
            var header = DecodeAt(start, 10);
            if (header is null || header[0] != 0x08 || header[3] != track || header[2] >= D64Image.SectorsPerTrack(track)) continue;
            if (header[1] != (byte)(header[2] ^ header[3] ^ header[4] ^ header[5])) continue;

            int next = (start + 10) % Length;
            for (int gap = 0; gap < 60; gap++, next = (next + 1) % Length)
            {
                if (!Sync[next] || !EndsSync((next + 1) % Length)) continue;
                var block = DecodeAt((next + 1) % Length, 325);
                if (block is null || block[0] != 0x07) break;
                byte checksum = 0;
                for (int i = 1; i <= 256; i++) checksum ^= block[i];
                if (checksum != block[257]) break;
                if (image.WriteSector(track, header[2], block.AsSpan(1, 256))) found++;
                image.Id = (header[5], header[4]);   // the disk ID the drive wrote in the header
                break;
            }
        }
        return found;
    }

    // Position i is the first byte after a SYNC mark
    bool EndsSync(int i) => !Sync[i] && Sync[(i + Length - 1) % Length];

    byte[]? DecodeAt(int start, int count)
    {
        var gcr = new byte[count];
        for (int i = 0; i < count; i++)
        {
            int at = (start + i) % Length;
            if (Sync[at]) return null;
            gcr[i] = Bytes[at];
        }
        return DecodeBlock(gcr);
    }

    // Groups of 5 bytes of GCR become 4; null if one of the codes does not exist
    static byte[]? DecodeBlock(byte[] gcr)
    {
        var output = new byte[gcr.Length / 5 * 4];
        for (int g = 0, o = 0; g < gcr.Length; g += 5, o += 4)
        {
            ulong bits = 0;
            for (int i = 0; i < 5; i++) bits = bits << 8 | gcr[g + i];
            for (int n = 0; n < 4; n++)
            {
                int high = Array.IndexOf(Gcr, (byte)(bits >> (35 - 10 * n) & 0x1F));
                int low = Array.IndexOf(Gcr, (byte)(bits >> (30 - 10 * n) & 0x1F));
                if (high < 0 || low < 0) return null;
                output[o + n] = (byte)(high << 4 | low);
            }
        }
        return output;
    }

    // Groups of 4 bytes become 5: each nibble is a 5-bit code
    static byte[] EncodeBlock(byte[] block)
    {
        var output = new byte[block.Length / 4 * 5];
        for (int i = 0, o = 0; i < block.Length; i += 4, o += 5)
        {
            ulong bits = 0;
            for (int n = 0; n < 4; n++)
            {
                bits = bits << 5 | Gcr[block[i + n] >> 4];
                bits = bits << 5 | Gcr[block[i + n] & 0x0F];
            }
            for (int b = 0; b < 5; b++) output[o + b] = (byte)(bits >> (32 - 8 * b));
        }
        return output;
    }
}
