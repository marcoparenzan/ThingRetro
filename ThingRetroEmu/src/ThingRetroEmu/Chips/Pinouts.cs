namespace ThingRetroEmu.Chips;

// Chip pinouts: each one is the contract of a socket. Any object that implements it "fits" the socket,
// like an electrically compatible chip (an 8580 instead of a 6581, a modern replacement, a probe that
// records the traffic...).
// Transaction model: the address, data, R/W and chip select pins are grouped into Read/Write
// (the chip is only called when it is selected); clock and reset are Tick and Reset;
// control lines are properties. Active-low lines (/IRQ, /NMI, /RES) are "true = active".

// Address/data bus as seen by whoever drives it (A0-A15, D0-D7, R/W)
public interface IBus
{
    byte Read(ushort address);
    void Write(ushort address, byte value);
}

// Address/data bus of the 4510: 20 address bits (A0-A19) and the MAP/ output, which tells whether the address
// has been translated by the on-chip memory mapper (mapped) or is the CPU's own 16-bit address (unmapped)
public interface IMappedBus
{
    byte Read(int address, bool mapped);
    void Write(int address, bool mapped, byte value);
}

// A floppy disk as the controller sees it: 512-byte sectors addressed by physical track (cylinder), side and
// sector number (1-based). The medium of a drive; null in the drive = no disk
public interface IDiskMedia
{
    bool WriteProtected { get; }
    int SectorsPerTrack { get; }
    void EraseTrack(int track, int side);                                      // the track becomes unformatted: no sector is found until it is written
    bool ReadSector(int track, int side, int sector, Span<byte> buffer);       // false = sector not found
    bool WriteSector(int track, int side, int sector, ReadOnlySpan<byte> data); // false = not found or protected
    bool FormatSector(int track, int side, int sector, ReadOnlySpan<byte> data); // writes a sector header and its data: the sector exists from now on
}

// The RAM expansion card: the REC's four-bit control register ($D0A0) and 1 MB of RAM in two banks of 512 KB, which the
// CPU (and the DMAgic) see at $80000-$FFFFF; the VIC-III can see a 128 KB window of it instead of the internal RAM
public interface IRamExpansionPinout
{
    void WriteControl(byte value);       // $D0A0: write only, four bits
    byte Read(int address);              // physical address $80000-$FFFFF
    void Write(int address, byte value);
    bool VicEnabled { get; }             // the VIC-III reads the card instead of the internal RAM
    byte ReadVic(int address);           // 17 bits: the address inside the 128 KB window
    void WriteVic(int address, byte value);
    void Reset();                        // /RES: the control register is cleared, the RAM is not
}

// A cartridge in the C65's cartridge port: ROM seen at physical $40000-$7FFFF (up to 256 KB)
public interface ICartridgePinout
{
    int Size { get; }
    byte Read(int offset);
}

// A floppy drive as the controller sees it, through the connector of the drive: the step and direction lines, the sensors and the
// heads over a disk. The controller does not know whether the disk is 3.5" or 5.25"
public interface IFloppyDrive
{
    bool Ready { get; }                  // /READY: a disk is in and spinning
    bool Track0 { get; }                 // /TR00: the head is on the outermost track
    bool WriteProtected { get; }         // /WPRT
    bool Index { get; }                  // /IP: the index hole passes under the sensor
    int Cylinder { get; }                // where the head is
    int Side { get; }                    // the head that is selected (0 or 1)
    IDiskMedia? Media { get; }
    void Step(bool towardsCentre);       // STEP with DIRC
}

// Western Digital WD1770, DIP-28: the floppy disk controller of the 1570, 1571 and 1581. Four registers (command and status, track,
// sector, data), the interrupt request and the data request, and the drive connector. Sector level: the commands find a sector and
// move its bytes one by one at the speed of the disk, without the MFM bits
public interface IWd1770Pinout
{
    byte Read(int register);             // A0-A1, D0-D7, R/W = 1, /CS
    void Write(int register, byte value);
    bool Intrq { get; }                  // INTRQ: the command ended
    bool Drq { get; }                    // DRQ: a byte waits in the data register, or is wanted
    IFloppyDrive? Drive { get; set; }    // STEP, DIRC, /TR00, /IP, /WPRT, READY
    void Reset();                        // /MR
    void Tick();                         // one cycle of the clock of the chip's users (the cycle count of the timings is in these)
}

