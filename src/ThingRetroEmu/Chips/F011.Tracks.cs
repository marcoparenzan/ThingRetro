namespace ThingRetroEmu.Chips;

// Free-format track commands of the F011: WTT (write a track, used to format) and RDT (read a track). The disk turns
// at 300 rpm, a track is 200 ms long and a byte takes 32 us: 6250 bytes. A write begins at the first index pulse after the
// command and ends at the next one; a read starts at once and goes on until the command is cancelled.
//
// What goes through the data register is the byte stream of the track, not the MFM bits: every byte has its clock (FF
// normally; the A1 marks are written with the clock FB, "missing clock"). A track written is read back as the sectors it
// contains: the stream is searched for header marks (A1 A1 A1 FE, track, side, sector, length, CRC) and data marks
// (A1 A1 A1 FB, the data, CRC), the CRCs are checked, and the sectors found replace those of the track, which
// is erased first (formatting destroys what was there). A track read is made the same way from the sectors of the disk,
// in the layout of the manual's "Formatting a track". Reference: "F011 specification", chapter "Command Descriptions"
public partial class F011
{
    enum TrackOp { None, Write, Read }

    TrackOp trackOp;
    bool waitingForIndex;          // WTT: the track starts at the next index pulse
    long trackStart;               // cycle in which the revolution started
    byte latch;                    // unbuffered: the byte the CPU supplied (write) or the disk read last (read)
    bool freshByte;                // write: the CPU supplied a byte since the last one was taken
    List<(byte Data, bool Mark)> written = [];
    List<byte> image = [];         // read: the track as bytes
    int imagePosition;
    bool buffered;                 // buffer pointers instead of the data register alone

    public const int TRACK_BYTES = 6250;

    // Cycles of one revolution (300 rpm at 1 MHz); shorter in tests that format whole disks
    public int RevolutionCycles { get; set; } = 200_000;
    int ByteCycles => Math.Max(1, RevolutionCycles / TRACK_BYTES);

    bool TrackBusy => trackOp != TrackOp.None;

    void StartTrack(TrackOp operation)
    {
        trackOp = operation;
        buffered = (command & 1) == 0;
        latch = operation == TrackOp.Write ? (byte)0xFF : (byte)0;
        freshByte = false;
        imagePosition = 0;
        written = [];
        if (operation == TrackOp.Read)
        {
            image = BuildTrackImage();
            waitingForIndex = false;
            trackStart = cycles;
        }
        else waitingForIndex = true; // WTT: writing begins with the first index pulse
    }

    void TrackTick()
    {
        if (waitingForIndex)
        {
            if (cycles % RevolutionCycles != 0) return;
            waitingForIndex = false;
            trackStart = cycles;
            statusB |= WGATE | RUN;
            return;
        }

        long elapsed = cycles - trackStart;
        if (elapsed > 0 && elapsed % ByteCycles == 0) TrackByte();
        if (trackOp == TrackOp.Write && elapsed >= RevolutionCycles) FinishWrite();
    }

    // One byte time: the disk takes a byte from the CPU (write) or gives one (read)
    void TrackByte()
    {
        if (trackOp == TrackOp.Write)
        {
            byte value;
            if (buffered)
            {
                value = buffer[diskPointer];
                diskPointer = (diskPointer + 1) & 511;
                statusA = (byte)(diskPointer == cpuPointer ? statusA | EQ : statusA & ~EQ);
            }
            else
            {
                if (!freshByte) statusA |= LOST; // the CPU was late: the previous byte goes on the disk again
                value = latch;
                freshByte = false;
            }
            written.Add((value, clock != 0xFF));
            statusA |= DRQ; // the byte is on the disk: the next one is due
        }
        else
        {
            byte value = image[imagePosition];
            imagePosition = (imagePosition + 1) % image.Count;
            if (buffered)
            {
                buffer[diskPointer] = value;
                diskPointer = (diskPointer + 1) & 511;
                statusA = (byte)(diskPointer == cpuPointer ? statusA | EQ : statusA & ~EQ);
            }
            else
            {
                if ((statusA & DRQ) != 0) statusA |= LOST; // the previous byte was not read
                latch = value;
            }
            statusA |= DRQ;
        }
    }

    // The CPU cancels a track command: a write is lost, a read just stops
    void CancelTrack()
    {
        trackOp = TrackOp.None;
        waitingForIndex = false;
        statusB = (byte)(statusB & ~(WGATE | RUN));
        statusB |= IRQ;
    }

    void FinishWrite()
    {
        trackOp = TrackOp.None;
        statusB = (byte)((statusB & ~(WGATE | RUN)) | IRQ);
        if (media is null || media.WriteProtected)
        {
            statusA |= RNF;
            return;
        }
        ApplyTrack();
    }

    // ---- The data register while a track command runs ----------------------------------------------------
    bool UnbufferedTrackRead => trackOp == TrackOp.Read && !buffered;
    bool UnbufferedTrackWrite => trackOp == TrackOp.Write && !buffered;

    // ---- Track stream <-> sectors -----------------------------------------------------------------------
    const int SYNC_BYTES = 12, GAP2 = 22, GAP3 = 24;

    // CRC-CCITT as the F011 does it: all ones at the start, polynomial $1021, over the marks and the field
    static ushort Crc(IEnumerable<byte> bytes)
    {
        ushort crc = 0xFFFF;
        foreach (byte b in bytes)
        {
            crc ^= (ushort)(b << 8);
            for (int bit = 0; bit < 8; bit++) crc = (ushort)((crc & 0x8000) != 0 ? crc << 1 ^ 0x1021 : crc << 1);
        }
        return crc;
    }

    // The track under the head as the manual's format lays it out: 4E gap, then for every sector the header and the data field
    List<byte> BuildTrackImage()
    {
        var bytes = new List<byte>(TRACK_BYTES);
        bytes.AddRange(Enumerable.Repeat((byte)0x4E, 60));
        int physicalTrack = headTrack, physicalSide = (control & 8) != 0 ? 0 : 1; // the SIDE bit set selects side 0
        if (media is not null)
        {
            var data = new byte[SECTOR_SIZE];
            for (int sectorNumber = 1; sectorNumber <= media.SectorsPerTrack; sectorNumber++)
            {
                if (!media.ReadSector(physicalTrack, physicalSide, sectorNumber, data)) continue;
                bytes.AddRange(Enumerable.Repeat((byte)0x00, SYNC_BYTES));
                var header = new List<byte> { 0xA1, 0xA1, 0xA1, 0xFE, (byte)physicalTrack, (byte)physicalSide, (byte)sectorNumber, 2 };
                bytes.AddRange(header);
                ushort headerCrc = Crc(header);
                bytes.Add((byte)(headerCrc >> 8)); bytes.Add((byte)headerCrc);
                bytes.AddRange(Enumerable.Repeat((byte)0x4E, GAP2));
                bytes.AddRange(Enumerable.Repeat((byte)0x00, SYNC_BYTES));
                var field = new List<byte> { 0xA1, 0xA1, 0xA1, 0xFB };
                field.AddRange(data);
                bytes.AddRange(field);
                ushort dataCrc = Crc(field);
                bytes.Add((byte)(dataCrc >> 8)); bytes.Add((byte)dataCrc);
                bytes.AddRange(Enumerable.Repeat((byte)0x4E, GAP3));
            }
        }
        while (bytes.Count < TRACK_BYTES) bytes.Add(0x4E);
        return bytes;
    }

    // The track written: erased, then the sectors that the stream contains are written
    void ApplyTrack()
    {
        int physicalTrack = headTrack, physicalSide = (control & 8) != 0 ? 0 : 1;
        media!.EraseTrack(physicalTrack, physicalSide);

        int i = 0;
        while (i + 3 < written.Count)
        {
            if (!IsMark(i, 0xFE)) { i++; continue; }

            // Header: A1 A1 A1 FE track side sector length crc crc
            if (i + 10 > written.Count) break;
            var header = written.Skip(i).Take(8).Select(entry => entry.Data).ToList();
            var crcBytes = written.Skip(i + 8).Take(2).Select(entry => entry.Data).ToArray();
            i += 10;
            if (Crc(header) != (ushort)(crcBytes[0] << 8 | crcBytes[1])) continue;
            int length = 128 << (header[7] & 3);

            // The data field follows, after the gap: look for its mark within the next 60 bytes
            int limit = Math.Min(written.Count - 4, i + 60);
            int found = -1;
            for (int j = i; j < limit; j++)
                if (IsMark(j, 0xFB)) { found = j; break; }
            if (found < 0 || found + 4 + length + 2 > written.Count) continue;

            var field = written.Skip(found).Take(4 + length).Select(entry => entry.Data).ToList();
            var dataCrc = written.Skip(found + 4 + length).Take(2).Select(entry => entry.Data).ToArray();
            i = found + 4 + length + 2;
            if (Crc(field) != (ushort)(dataCrc[0] << 8 | dataCrc[1])) continue;
            if (length == SECTOR_SIZE) media.FormatSector(header[4], header[5], header[6], field.Skip(4).ToArray());
        }
    }

    // Three marked A1 followed by the mark byte
    bool IsMark(int index, byte markByte) =>
        index + 3 < written.Count &&
        written[index].Data == 0xA1 && written[index].Mark &&
        written[index + 1].Data == 0xA1 && written[index + 1].Mark &&
        written[index + 2].Data == 0xA1 && written[index + 2].Mark &&
        written[index + 3].Data == markByte;
}
