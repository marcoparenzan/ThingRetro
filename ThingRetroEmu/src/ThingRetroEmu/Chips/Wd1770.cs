namespace ThingRetroEmu.Chips;

/*
    Western Digital WD1770 floppy disk controller, at sector level.
      $0 command (write) / status (read)    $1 track register    $2 sector register    $3 data register
    The commands find a sector and move its 512 bytes through the data register at the speed of an MFM disk (250 kbit/s: a byte every
    32 microseconds), with DRQ for each byte and INTRQ at the end; the MFM bits, the CRCs and the gaps are not modelled. The head
    steps through the drive connector (IFloppyDrive) with the step rates of the chip.
      Type I    $0x restore, $1x seek, $2x/$3x step, $4x/$5x step in, $6x/$7x step out   (h = bit 3 head load, V = bit 2 verify, r1 r0 step rate, u = bit 4)
      Type II   $8x read sector, $9x read sectors, $Ax write sector, $Bx write sectors   (E = bit 2: 15 ms delay)
      Type III  $Cx read address, $Ex read track (not done), $Fx write track (format)
      Type IV   $Dx force interrupt
    Status of Type I: 7 not ready, 6 write protect, 5 head loaded, 4 seek error, 3 CRC error, 2 track 0, 1 index, 0 busy.
    Status of the others: 7 not ready, 6 write protect, 5 record type / write fault, 4 record not found, 3 CRC error, 2 lost data, 1 DRQ, 0 busy.
    Reference: WD1770/1772 data sheet
 */
public sealed class Wd1770 : IWd1770Pinout
{
    const int SECTOR_SIZE = 512;
    const int TRACK_BYTES = 6250;

    int byteCycles;

    // The clock of whoever ticks the chip: the timings are in these cycles. The 1581 clocks it from its 2 MHz CPU
    public Wd1770(long clockHz = 2_000_000)
    {
        ClockHz = clockHz;
        byteCycles = (int)(clockHz * 32 / 1_000_000);       // 32 µs
    }

    public long ClockHz { get; }

    public IFloppyDrive? Drive { get; set; }

    public bool Intrq { get; private set; }
    public bool Drq { get; private set; }

    // ---- registers ------------------------------------------------------------------------------------------------------------

    byte track, sector, data;
    bool busy, headLoaded, seekError, crcError, lostData, recordNotFound, writeFault;
    bool typeOne = true;                     // the status is the one of the last command's type
    bool lastStepIn;                          // the direction of the last step

    enum Phase { Idle, Stepping, Verify, FindSector, ReadBytes, WriteStart, WriteBytes, ReadAddress, WriteTrack, WriteTrackEnd }
    Phase phase = Phase.Idle;
    int wait;                                 // cycles to the next thing
    int command;
    int stepsLeft;
    bool multiple;
    readonly byte[] buffer = new byte[SECTOR_SIZE];
    int position;
    int idCursor;                             // the sector under the head, for Read Address
    readonly byte[] idField = new byte[6];
    readonly List<byte> trackStream = [];

    // The chip takes a moment to decode a command: BUSY is seen at once, even when there is nothing to do (a restore on track 0)
    int StartDelay => (int)(ClockHz * 40 / 1_000_000);

    int StepCycles(int command) => (int)(ClockHz * (command & 3) switch { 0 => 6, 1 => 12, 2 => 2, _ => 3 } / 1000);
    int Ms(int milliseconds) => (int)(ClockHz * milliseconds / 1000);

    public void Reset()
    {
        track = sector = data = 0;
        busy = seekError = crcError = lostData = recordNotFound = writeFault = false;
        headLoaded = false;
        typeOne = true;
        Intrq = Drq = false;
        phase = Phase.Idle;
        wait = 0;
    }

    public byte Read(int register)
    {
        switch (register & 3)
        {
            case 0:
                Intrq = false;                // reading the status resets INTRQ
                return Status();
            case 1: return track;
            case 2: return sector;
            default:
                Drq = false;
                return data;
        }
    }

    public void Write(int register, byte value)
    {
        switch (register & 3)
        {
            case 0: Command(value); break;
            case 1: track = value; break;
            case 2: sector = value; break;
            default:
                data = value;
                Drq = false;
                break;
        }
    }

    byte Status()
    {
        var drive = Drive;
        int s = 0;
        if (drive is null || !drive.Ready) s |= 0x80;
        if (typeOne)
        {
            if (drive?.WriteProtected == true) s |= 0x40;
            if (headLoaded) s |= 0x20;
            if (seekError) s |= 0x10;
            if (crcError) s |= 0x08;
            if (drive?.Track0 == true) s |= 0x04;
            if (drive?.Index == true) s |= 0x02;
        }
        else
        {
            if (drive?.WriteProtected == true && command >= 0xA0) s |= 0x40;
            if (writeFault) s |= 0x20;
            if (recordNotFound) s |= 0x10;
            if (crcError) s |= 0x08;
            if (lostData) s |= 0x04;
            if (Drq) s |= 0x02;
        }
        if (busy) s |= 0x01;
        return (byte)s;
    }

    // ---- commands -------------------------------------------------------------------------------------------------------------

    void Command(byte value)
    {
        if (value >> 4 == 0xD)
        {
            // Force interrupt: whatever is going on stops
            bool wasBusy = busy;
            phase = Phase.Idle;
            busy = false;
            Drq = false;
            typeOne = true;
            if ((value & 0x08) != 0) Intrq = true;
            else Intrq = false;
            return;
        }
        if (busy) return;                     // the other commands are ignored while one is going on

        command = value;
        Intrq = false;
        Drq = false;
        seekError = crcError = lostData = recordNotFound = writeFault = false;

        if ((value & 0x80) == 0) StartTypeOne(value);
        else if ((value & 0x40) == 0) StartTypeTwo(value);
        else StartTypeThree(value);
    }

    void End()
    {
        busy = false;
        Drq = false;
        phase = Phase.Idle;
        Intrq = true;
    }

    // ---- Type I -------------------------------------------------------------------------------------------------------------

    void StartTypeOne(byte value)
    {
        typeOne = true;
        busy = true;
        headLoaded = (value & 0x08) != 0;
        phase = Phase.Stepping;
        switch (value >> 4)
        {
            case 0: // restore
                track = 0xFF;
                stepsLeft = 255;
                lastStepIn = false;
                wait = StartDelay;
                break;
            case 1: // seek
                stepsLeft = 255;
                wait = StartDelay;
                break;
            default: // step, step in, step out
                if ((value >> 4 & 0xE) == 4) lastStepIn = true;
                else if ((value >> 4 & 0xE) == 6) lastStepIn = false;
                stepsLeft = 1;
                wait = StartDelay;
                break;
        }
    }

    // One step of a Type I command (or its end)
    void TypeOneStep()
    {
        var drive = Drive;
        int kind = command >> 4;
        if (kind == 0)
        {
            if (drive is null || drive.Track0) { track = 0; FinishTypeOne(); return; }
            if (stepsLeft-- <= 0) { seekError = true; FinishTypeOne(); return; }
            drive.Step(false);
            wait = StepCycles(command);
            return;
        }
        if (kind == 1)
        {
            if (track == data) { FinishTypeOne(); return; }
            bool inward = data > track;
            drive?.Step(inward);
            track = (byte)(track + (inward ? 1 : -1));
            wait = StepCycles(command);
            lastStepIn = inward;
            // The seek is over when the track register reaches the target, and the loop ends there
            return;
        }
        // single steps
        drive?.Step(lastStepIn);
        if ((command & 0x10) != 0) track = (byte)(track + (lastStepIn ? 1 : -1));
        wait = StepCycles(command);
        stepsLeft = 0;
        phase = Phase.Verify;                // the settle time
    }

    void FinishTypeOne()
    {
        if ((command & 0x04) != 0)
        {
            // Verify: the ID of a sector of the track must say that this is the track
            phase = Phase.Verify;
            wait = Ms(15);
            return;
        }
        End();
    }

    void VerifyDone()
    {
        if ((command & 0x04) != 0)
        {
            var drive = Drive;
            bool found = drive?.Media is { } media && drive.Cylinder == track && media.ReadSector(drive.Cylinder, drive.Side, 1, new byte[SECTOR_SIZE]);
            if (!found) seekError = true;
        }
        End();
    }

    // ---- Type II ------------------------------------------------------------------------------------------------------------

    void StartTypeTwo(byte value)
    {
        typeOne = false;
        busy = true;
        multiple = (value & 0x10) != 0;
        var drive = Drive;
        if (drive is null || !drive.Ready)
        {
            End();
            return;
        }
        if ((value & 0xE0) == 0xA0 && drive.WriteProtected)
        {
            End();
            return;
        }
        phase = Phase.FindSector;
        wait = (value & 0x04) != 0 ? Ms(15) : 1;
    }

    // The sector under the head: the next one in the track
    void FindSector()
    {
        var drive = Drive;
        if (drive?.Media is not { } media || drive.Cylinder != track || sector < 1 || sector > media.SectorsPerTrack)
        {
            recordNotFound = true;
            wait = Ms(1000);                  // five turns of the disk and it gives up
            phase = Phase.Idle;
            pendingEnd = true;
            return;
        }
        bool write = (command & 0xE0) == 0xA0;
        if (!write && !media.ReadSector(drive.Cylinder, drive.Side, sector, buffer))
        {
            recordNotFound = true;
            wait = Ms(1000);
            phase = Phase.Idle;
            pendingEnd = true;
            return;
        }
        position = 0;
        // The ID field, the gap and the data mark go by before the first byte
        wait = byteCycles * (write ? 22 : 30);
        phase = write ? Phase.WriteStart : Phase.ReadBytes;
        if (write) Array.Clear(buffer);
    }

    bool pendingEnd;

    void ReadByte()
    {
        if (Drq) lostData = true;             // the CPU did not take the last one
        data = buffer[position++];
        Drq = true;
        if (position < SECTOR_SIZE)
        {
            wait = byteCycles;
            return;
        }
        // End of the sector: with the multiple sector bit, the next one follows
        if (multiple)
        {
            sector++;
            phase = Phase.FindSector;
            wait = byteCycles * 40;
            return;
        }
        wait = byteCycles;
        phase = Phase.Idle;
        pendingEnd = true;
    }

    void WriteByte()
    {
        if (Drq)
        {
            // The CPU did not give the byte: the command ends
            lostData = true;
            Drq = false;
            End();
            return;
        }
        buffer[position++] = data;
        if (position >= SECTOR_SIZE)
        {
            var drive = Drive;
            var media = drive?.Media;
            if (media is null || !media.WriteSector(drive!.Cylinder, drive.Side, sector, buffer))
                writeFault = true;
            if (multiple && !writeFault)
            {
                sector++;
                phase = Phase.FindSector;
                wait = byteCycles * 40;
                return;
            }
            wait = byteCycles * 2;
            phase = Phase.Idle;
            pendingEnd = true;
            return;
        }
        Drq = true;                           // the next byte, please
        wait = byteCycles;
    }

    // ---- Type III -----------------------------------------------------------------------------------------------------------

    void StartTypeThree(byte value)
    {
        typeOne = false;
        busy = true;
        var drive = Drive;
        if (drive is null || !drive.Ready)
        {
            End();
            return;
        }
        switch (value >> 4)
        {
            case 0xC: // read address
                idField[0] = (byte)drive.Cylinder;
                idField[1] = (byte)drive.Side;
                idCursor = idCursor % (drive.Media?.SectorsPerTrack ?? 10) + 1;
                idField[2] = (byte)idCursor;
                idField[3] = 2;               // 512 bytes
                int crc = Crc16(0xFFFF, [0xA1, 0xA1, 0xA1, 0xFE, idField[0], idField[1], idField[2], idField[3]]);
                idField[4] = (byte)(crc >> 8);   // the CRC the formatting wrote, high byte first: the DOS checks it
                idField[5] = (byte)crc;
                position = 0;
                phase = Phase.ReadAddress;
                wait = byteCycles * 4 + Ms(1);
                break;
            case 0xF: // write track
                if (drive.WriteProtected) { End(); return; }
                trackStream.Clear();
                Drq = true;
                position = 0;
                phase = Phase.WriteTrack;
                wait = byteCycles;
                break;
            default:  // read track: not done
                recordNotFound = true;
                End();
                break;
        }
    }

    // CRC-CCITT as the MFM disks use it: polynomial $1021, started from $FFFF, over the three $A1 sync bytes, the mark and the fields
    static int Crc16(int crc, ReadOnlySpan<byte> bytes)
    {
        foreach (byte b in bytes)
        {
            crc ^= b << 8;
            for (int bit = 0; bit < 8; bit++) crc = (crc & 0x8000) != 0 ? crc << 1 ^ 0x1021 : crc << 1;
            crc &= 0xFFFF;
        }
        return crc;
    }

    void ReadAddressByte()
    {
        if (Drq) lostData = true;
        data = idField[position++];
        if (position == 1) sector = data;     // the track number of the ID goes to the sector register
        Drq = true;
        wait = byteCycles;
        if (position >= 6)
        {
            phase = Phase.Idle;
            pendingEnd = true;
        }
    }

    void WriteTrackByte()
    {
        if (Drq)
        {
            lostData = true;
            Drq = false;
            End();
            return;
        }
        trackStream.Add(data);
        position++;
        if (position >= TRACK_BYTES)
        {
            CommitTrack();
            wait = byteCycles;
            phase = Phase.Idle;
            pendingEnd = true;
            return;
        }
        Drq = true;
        wait = byteCycles;
    }

    // The bytes the DOS wrote are the track: ID marks ($FE track side sector size, then $F7 for the CRC) and data marks ($FB, then
    // 512 bytes and $F7). The sectors found are the ones the disk will have
    void CommitTrack()
    {
        var drive = Drive;
        if (drive?.Media is not { } media) return;
        media.EraseTrack(drive.Cylinder, drive.Side);
        var s = trackStream;
        int track = -1, side = -1, sec = -1;
        for (int i = 0; i < s.Count; i++)
        {
            if (s[i] == 0xFE && i + 4 < s.Count)
            {
                track = s[i + 1];
                side = s[i + 2];
                sec = s[i + 3];
                i += 4;
            }
            else if ((s[i] is 0xFB or 0xF8) && sec >= 1 && i + SECTOR_SIZE < s.Count)
            {
                var sectorData = new byte[SECTOR_SIZE];
                for (int k = 0; k < SECTOR_SIZE; k++) sectorData[k] = s[i + 1 + k];
                media.FormatSector(drive.Cylinder, drive.Side, sec, sectorData);
                i += SECTOR_SIZE;
                sec = -1;
            }
        }
        _ = track;
        _ = side;
    }

    // ---- the clock ------------------------------------------------------------------------------------------------------------

    public void Tick()
    {
        if (wait == 0) return;
        if (--wait > 0) return;

        if (pendingEnd)
        {
            pendingEnd = false;
            End();
            return;
        }
        switch (phase)
        {
            case Phase.Stepping: TypeOneStep(); break;
            case Phase.Verify: VerifyDone(); break;
            case Phase.FindSector: FindSector(); break;
            case Phase.ReadBytes: ReadByte(); break;
            case Phase.WriteStart:
                Drq = true;                   // the first byte, please
                wait = byteCycles * 2;
                phase = Phase.WriteBytes;
                break;
            case Phase.WriteBytes: WriteByte(); break;
            case Phase.ReadAddress: ReadAddressByte(); break;
            case Phase.WriteTrack: WriteTrackByte(); break;
        }
    }
}
