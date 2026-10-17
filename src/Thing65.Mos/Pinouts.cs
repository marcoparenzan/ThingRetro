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
