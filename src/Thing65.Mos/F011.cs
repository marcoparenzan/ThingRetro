namespace Thing65.Mos;

// CSG F011: the C65's disk controller. The real chip reads and writes MFM; this one works one sector at a time (what
// the C65 DOS does with it): the head position, the 512-byte buffer with its two pointers (one for the CPU, one for
// the disk), the status bits and the commands READ SECTOR, WRITE SECTOR, step in/out, spin up, cancel, clear buffer.
// Free-format track read/write (RDT, WTT) is not implemented: it finds no record.
// A command keeps BUSY set for a while (BusyCycles) and its effects appear when it ends, together with IRQ.
//
// Registers ($D080-$D08A): 0 control, 1 command, 2 status A, 3 status B, 4 track, 5 sector, 6 side, 7 data,
// 8 clock, 9 step, 10 protect code
// Reference: "C65 Specifications", chapter 2.5
public class F011 : IF011Pinout
{
    // Control register bits
    const int CONTROL_IRQ = 0x80;
    const int CONTROL_MOTOR = 0x20;
    const int CONTROL_SWAP = 0x10;

    // Status A bits
    const int BUSY = 0x80, DRQ = 0x40, EQ = 0x20, RNF = 0x10, CRC = 0x08, LOST = 0x04, PROT = 0x02, TK0 = 0x01;
    // Status B bits
    const int RDREQ = 0x80, WTREQ = 0x40, RUN = 0x20, WGATE = 0x10, DSKIN = 0x08, INDEX = 0x04, IRQ = 0x02, DSKCHG = 0x01;

    const int SECTOR_SIZE = 512;
    const int INDEX_PERIOD = 200_000;   // 300 rpm = 200 ms per turn, at 1 MHz
    const int INDEX_WIDTH = 4_000;      // the index pulse

    // Cycles a command takes (the real times: a sector read ~ 5-10 ms, a head step ~ 3-16 ms)
    public int BusyCycles { get; set; } = 1000;

    IDiskMedia? media;
    public IDiskMedia? Media
    {
        get => media;
        set
        {
            media = value;
            statusB |= DSKCHG; // a disk was put in or taken out
        }
    }

    public bool Irq => (control & CONTROL_IRQ) != 0 && (statusB & IRQ) != 0;
    public bool Motor => (control & CONTROL_MOTOR) != 0;

    byte control, command, track, sector, side, clock = 0xFF, step = 0xFF;
    byte statusA, statusB;            // the flags the chip keeps; BUSY, TK0, PROT, DSKIN, INDEX are computed
    int busy;                         // cycles left; a command is running while > 0
    int pending = -1;                 // command waiting for the end of BUSY
    int drive;
    int headTrack;                    // cylinder under the head of the local drive
    long cycles;

    readonly byte[] buffer = new byte[SECTOR_SIZE];
    int cpuPointer;                   // buffer position for the CPU (data register)
    int diskPointer;                  // buffer position for the disk side

    int SwapMask => (control & CONTROL_SWAP) != 0 ? 0x100 : 0;

    public void Reset()
    {
        control = command = track = sector = side = 0;
        clock = step = 0xFF;
        statusA = 0;
        statusB = 0;
        busy = 0;
        pending = -1;
        drive = 0;
        headTrack = 0;
        cpuPointer = diskPointer = 0;
    }

    public void Tick()
    {
        cycles++;
        if (busy > 0 && --busy == 0) Complete();
    }

    // ---- Registers --------------------------------------------------------------------------------------
    public byte Read(int register)
    {
        switch (register & 0x0F)
        {
            case 0: return control;
            case 1: return command;
            case 2: return StatusA;
            case 3: return StatusB;
            case 4: return track;
            case 5: return sector;
            case 6: return side;
            case 7: return ReadData();
            case 8: return clock;
            case 9: return step;
            case 10: return 0; // protect code: no protected sectors
            default: return 0xFF;
        }
    }

    public void Write(int register, byte value)
    {
        switch (register & 0x0F)
        {
            case 0: WriteControl(value); break;
            case 1: WriteCommand(value); break;
            case 4: track = value; break;
            case 5: sector = value; break;
            case 6: side = value; break;
            case 7: WriteData(value); break;
            case 8: clock = value; break;
            case 9: step = value; break;
        }
    }

    // BUSY, TK0 (head on track 0) and PROT (write protected) are read from the drive
    byte StatusA => (byte)((statusA & (DRQ | EQ | RNF | CRC | LOST)) | (busy > 0 ? BUSY : 0) |
                           (headTrack == 0 ? TK0 : 0) | (media?.WriteProtected == true ? PROT : 0));

    // DSKIN (a disk is in) and INDEX (the index hole passes under the sensor, once per turn while the motor spins)
    byte StatusB => (byte)((statusB & (RDREQ | WTREQ | RUN | WGATE | IRQ | DSKCHG)) | (media is not null ? DSKIN : 0) |
                           (media is not null && Motor && cycles % INDEX_PERIOD < INDEX_WIDTH ? INDEX : 0));

    // Writing the control register selects the drive, the side and the motor. Selecting another drive clears DSKCHG
    void WriteControl(byte value)
    {
        control = value;
        int selected = value & 7;
        if (selected != drive)
        {
            drive = selected;
            statusB = (byte)(statusB & ~DSKCHG);
        }
        if (busy == 0) busy = 8; // a short BUSY, as the real chip shows after a control write
    }

    // ---- Commands ---------------------------------------------------------------------------------------
    void WriteCommand(byte value)
    {
        // A running command is only interrupted by CANcel
        if (busy > 0 && (value & 0xF8) != 0) return;

        command = value;
        // Cancel and clear buffer pointers take no time
        if ((value & 0xF8) == 0)
        {
            busy = 0;
            pending = -1;
            if ((value & 1) != 0)
            {
                cpuPointer = diskPointer = 0;
                statusA = (byte)(statusA & ~DRQ);
                statusB = (byte)(statusB & ~RDREQ);
            }
            return;
        }

        // Any other command: BUSY, and IRQ, RNF, CRC, LOST and the request flags cleared
        statusA = (byte)(statusA & ~(RNF | CRC | LOST));
        statusB = (byte)(statusB & ~(IRQ | RDREQ | WTREQ));
        pending = value;
        busy = BusyCycles;
    }

    // End of BUSY: the command does its job
    void Complete()
    {
        int value = pending;
        pending = -1;
        statusB |= IRQ;
        if (value < 0) return;

        switch (value & 0xF8)
        {
            case 0x40: ReadSector(); break;                 // RDS (bit 0 = unbuffered, handled as buffered)
            case 0x80: WriteSector(); break;                // WTS
            case 0x60: case 0xA0: statusA |= RNF; break;    // RDT, WTT: free format is not supported
            case 0x10:                                      // STOUT, or TIME if bit 2 is set (no pulse)
                if ((value & 4) == 0 && headTrack > 0) headTrack--;
                break;
            case 0x18:                                      // STIN
                if (headTrack < 85) headTrack++;
                break;
            case 0x20: statusA |= RNF; break;               // SPIN: the manual says RNF at the end of the wait
        }
    }

    void ReadSector()
    {
        var data = new byte[SECTOR_SIZE];
        if (media is null || !media.ReadSector(track, side, sector, data))
        {
            statusA |= RNF;
            statusB = (byte)(statusB & ~(RDREQ | WTREQ | RUN | WGATE));
            return;
        }
        for (int i = 0; i < SECTOR_SIZE; i++)
        {
            buffer[diskPointer] = data[i];
            diskPointer = (diskPointer + 1) & 511;
        }
        statusA = (byte)((statusA | DRQ) & ~EQ); // the CPU has the whole sector to read
        statusB |= RDREQ | RUN;
    }

    void WriteSector()
    {
        var data = new byte[SECTOR_SIZE];
        for (int i = 0; i < SECTOR_SIZE; i++)
        {
            data[i] = buffer[diskPointer];
            diskPointer = (diskPointer + 1) & 511;
        }
        if (media is null || media.WriteProtected || !media.WriteSector(track, side, sector, data))
        {
            statusA |= RNF;
            statusB = (byte)(statusB & ~(RDREQ | WTREQ | RUN | WGATE));
            return;
        }
        statusA = (byte)((statusA | DRQ) & ~EQ);
        statusB |= WTREQ | RUN;
    }

    // ---- Data register: the CPU's gateway to the buffer -------------------------------------------------
    byte ReadData()
    {
        statusA = (byte)(statusA & ~DRQ);
        statusB = (byte)(statusB & ~RDREQ);
        if ((statusA & EQ) != 0) statusA |= LOST; // another byte after the pointers met: the disk side lost data
        byte value = buffer[cpuPointer ^ SwapMask];
        AdvanceCpuPointer();
        return value;
    }

    void WriteData(byte value)
    {
        statusA = (byte)(statusA & ~DRQ);
        if ((statusA & EQ) != 0) statusA |= LOST;
        buffer[cpuPointer ^ SwapMask] = value;
        AdvanceCpuPointer();
    }

    // EQ is judged after the access: right after a sector is read both pointers are at the same place, and the DOS
    // does not expect EQ then
    void AdvanceCpuPointer()
    {
        cpuPointer = (cpuPointer + 1) & 511;
        statusA = (byte)(cpuPointer == diskPointer ? statusA | EQ : statusA & ~EQ);
    }
}
