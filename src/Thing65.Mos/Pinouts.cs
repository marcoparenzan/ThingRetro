namespace Thing65.Mos;

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

// MOS 6510, DIP-40: the C64's CPU (a 6502 with an on-chip I/O port)
public interface I6510Pinout
{
    IBus? Bus { get; set; }              // A0-A15, D0-D7, R/W
    Func<bool> IrqInput { get; set; }    // /IRQ (level triggered)
    Func<bool> NmiInput { get; set; }    // /NMI (edge triggered)
    Func<bool> RdyInput { get; set; }    // RDY: false = the CPU stops at the first read cycle (BA of the VIC-II)
    Func<bool> AecInput { get; set; }    // AEC: false = the bus belongs to someone else (the VIC-II), the CPU is halted
    ParallelPort Port { get; }           // P0-P5, registers $0000 (direction) and $0001 (data)
    void Reset();                        // /RES
    void Tick();                         // one φ2 cycle
}

// Address/data bus of the 4510: 20 address bits (A0-A19) and the MAP/ output, which tells whether the address
// has been translated by the on-chip memory mapper (mapped) or is the CPU's own 16-bit address (unmapped)
public interface IMappedBus
{
    byte Read(int address, bool mapped);
    void Write(int address, bool mapped, byte value);
}

// CSG 4510, 84-pin PLCC: the C65's CPU (a 65CE02 with a memory mapper that reaches 1 MB)
public interface I4510Pinout
{
    IMappedBus? Bus { get; set; }        // A0-A19, D0-D7, R/W, MAP/
    Func<bool> IrqInput { get; set; }    // /IRQ (level triggered)
    Func<bool> NmiInput { get; set; }    // /NMI (edge triggered)
    Func<bool> AecInput { get; set; }    // AEC: false = the bus belongs to someone else, the CPU is halted
    Func<bool> DmaInput { get; set; }    // /DMA: false = the CPU is halted at the end of the cycle (any cycle, writes too)
    void Reset();                        // /RES
    void Tick();                         // one φ2 cycle
}

// Memory as seen by the DMAgic: absolute 20-bit addresses, and the I/O bit that selects the memory mapped I/O space
// ($D000-$DFFF of the bank) instead of the memory
public interface IDmaMemory
{
    byte Read(int address, bool io);
    void Write(int address, bool io, byte value);
}

// CSG F018 DMAgic: the C65's DMA controller (copy, fill, with modulo windows, chaining), four registers at $D700
public interface IF018Pinout
{
    byte Read(int register);             // RS0-RS1, D0-D7, R/W = 1, /CS
    void Write(int register, byte value);
    IDmaMemory? Memory { get; set; }     // the system bus, taken over while a job runs
    Func<bool> BusGrant { get; set; }    // false = someone with a higher priority (the VIC-III) has the bus: wait
    Func<bool> InterruptRequest { get; set; } // IRQ or NMI is asserted: a job with the INT bit stops until the status is read
    bool Busy { get; }                   // true = the CPU must be halted (its /DMA pin)
    void Reset();                        // /RES
    void Tick();                         // one cycle: one bus access
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

// CSG F011 disk controller, the C65's built-in 3.5" drive: sector-level commands, a 512-byte buffer, eight registers
public interface IF011Pinout
{
    byte Read(int register);             // A0-A3, D0-D7, R/W = 1, /CS
    void Write(int register, byte value);
    IDiskMedia? Media { get; set; }      // the disk in the local drive (drive 0), if any
    bool Irq { get; }                    // /IRQ
    bool Motor { get; }                  // MOT: the motor (and the drive's LED) is on
    void Reset();                        // /RES
    void Tick();                         // one cycle (1 MHz)
}

// MOS 6526 CIA, DIP-40: timers, clock, parallel ports, serial line
public interface I6526Pinout
{
    byte Read(int register);             // RS0-RS3, D0-D7, R/W = 1, /CS
    void Write(int register, byte value);
    ParallelPort PortA { get; }          // PA0-PA7
    ParallelPort PortB { get; }          // PB0-PB7
    bool Irq { get; }                    // /IRQ
    void Reset();                        // /RES
    void Tick();                         // φ2
    // Not modelled: CNT, SP, FLAG, /PC, TOD (the TOD is counted internally)
}

// Memory as seen by the VIC-II: 14 address bits (A0-A13) and the color nibble (D8-D11).
// Bits A14/A15 (the bank) are added by the board, which knows where they come from
public interface IVicMemory
{
    byte Read(int address);              // A0-A13 -> D0-D7
    byte ReadColor(int address);         // A0-A9 -> D8-D11 (color RAM)
}

// MOS 6569 VIC-II (PAL), DIP-40: graphics, raster, bus DMA
public interface I6569Pinout
{
    byte Read(int register);             // A0-A5, D0-D7, R/W = 1, /CS
    void Write(int register, byte value);
    IVicMemory? Memory { get; set; }     // video bus
    bool Irq { get; }                    // /IRQ
    bool BusAvailable { get; }           // BA: false = the VIC-II will take the bus in 3 cycles (the CPU finishes its writes)
    bool AddressEnable { get; }          // AEC: false = the VIC-II is using the bus in the CPU's half of the cycle too
    Action? FrameCompleted { get; set; } // video output: a full frame is ready
    unsafe void Render(byte* pixels);    // copies the last frame (SCREEN_WIDTH x SCREEN_HEIGHT, BGRA)
    void Reset();                        // the VIC-II has no /RES: clears its state at power on
    void Tick();                         // one φ0 cycle
}

// Memory as seen by the VIC-III: the VIC-II's 16 KB bank, and the first 128 KB of RAM for the bitplanes and the DAT
public interface IVic3Memory : IVicMemory
{
    int Bank { get; }                    // the 16 KB VIC-II bank (0-3), from CIA 2
    byte ReadRam(int address);           // A0-A16: the 128 KB of RAM (bitplanes: even ones in the first 64 KB, odd in the second)
    void WriteRam(int address, byte value);
}

// CSG 4567 VIC-III: the C65's video chip, a VIC-II (same pinout) with more registers: $D02F-$D07F, a palette, and the
// bits of control register A that make it decode the system ROMs. In "VIC-II mode" (after reset, and C64 mode) it
// behaves as a 6569; the registers beyond $2E appear after writing $A5 and $96 to the KEY register ($D02F)
public interface I4567Pinout : I6569Pinout
{
    new IVic3Memory? Memory { get; set; } // video bus
    bool NewMode { get; }                // VIC-III mode: the new registers, and the new I/O map ($D080-$D7FF), are visible
    bool Fast { get; }                   // FAST: the CPU runs at 3.5 MHz instead of 1 MHz
    byte ControlA { get; }               // $D030: ROM@E000, CROM@9000, ROM@C000, ROM@A000, ROM@8000, PAL, EXT SYNC, CRAM@DC00
    byte ControlB { get; }               // $D031: H640, FAST, ATTR, BPM, V400, H1280, MONO, INT
    void WritePalette(int component, int index, byte value); // component 0 = red, 1 = green, 2 = blue ($D100-$D3FF)
}

// MOS 6581 SID, DIP-28: three voices, envelopes, filter
public interface I6581Pinout
{
    byte Read(int register);             // A0-A4, D0-D7, R/W = 1, /CS
    void Write(int register, byte value);
    Func<byte> PotXInput { get; set; }   // POT X: resistance connected, as a 0-255 value ($FF = none)
    Func<byte> PotYInput { get; set; }   // POT Y
    double AudioOutput { get; }          // AUDIO OUT: instantaneous level, updated every cycle
    void Reset();                        // /RES
    void Tick();                         // φ2
}

// The C65's UART (a 6511-style serial port): data, status, control, baud rate, interrupt mask and flags at $D600-$D606,
// the fast serial control at $D609. Registers 7 and 8 ($D607/$D608, the port with the CAPS LOCK sense and the keyboard's
// ninth line) are the board's
public interface IUartPinout
{
    byte Read(int register);
    void Write(int register, byte value);
    Action<byte>? Transmit { get; set; } // TXD: a character leaves the UART
    void Receive(byte value);            // RXD: a character arrives
    bool Irq { get; }                    // /IRQ
    bool Nmi { get; }                    // /NMI
    void Reset();                        // /RES
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
