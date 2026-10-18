namespace ThingMOS;

// CSG 4510: the C65's CPU. A 65CE02 core (a CMOS 6502 with the Z index register, a relocatable zero page called
// "base page", a 16-bit stack pointer and stack-relative addressing, all 256 opcodes defined) plus a memory mapper
// that turns the 16-bit addresses into 20-bit ones (1 MB), driven by the MAP instruction.
// The on-chip peripherals (timers, UART, ports) are not part of this class: the board provides them.
// Reference: "C65 Specifications", chapter 2.3
public partial class MOS4510 : I4510Pinout
{
    // Pins wired by the board
    public IMappedBus? Bus { get; set; }
    public Func<bool> IrqInput { get; set; } = () => false;
    public Func<bool> NmiInput { get; set; } = () => false;
    public Func<bool> AecInput { get; set; } = () => true;
    public Func<bool> DmaInput { get; set; } = () => true;

    // internal so that the tests can set up and check the CPU state
    internal byte A, X, Y, Z, B;
    internal ushort PC;
    internal ushort SP;          // 16 bit: SPH (stack page) and SPL
    internal byte Status;

    const byte FLAG_C = 1 << 0;
    const byte FLAG_Z = 1 << 1;
    const byte FLAG_I = 1 << 2;
    const byte FLAG_D = 1 << 3;
    const byte FLAG_B = 1 << 4;
    const byte FLAG_E = 1 << 5; // extend disable: set = 8-bit stack pointer (6502 compatible), clear = 16-bit
    const byte FLAG_V = 1 << 6;
    const byte FLAG_N = 1 << 7;

    internal byte SPL { get => (byte)SP; set => SP = (ushort)((SP & 0xFF00) | value); }
    internal byte SPH { get => (byte)(SP >> 8); set => SP = (ushort)((SP & 0x00FF) | (value << 8)); }

    // /RES: B and Z are cleared, the stack pointer is in byte mode with the stack in page 1, E and I are set.
    // Simplification: the chip spends 4 cycles doing nothing before reading the vector; here it is read at once
    public void Reset()
    {
        A = X = Y = Z = B = 0;
        SP = 0x01FF;
        Status = FLAG_E | FLAG_I;
        mapMask = 0;
        lowerOffset = upperOffset = 0;
        mapInhibit = false;
        cycle = 0;
        inInterrupt = false;
        interruptPending = false;
        nmiPending = false;
        nmiWasLow = false;
        PC = ReadWord(0xFFFC);
    }

    // ---- Memory mapper ----------------------------------------------------------------------------------
    // The 64 KB window is divided in eight 8 KB blocks: 0-3 (lower region) and 4-7 (upper region). Each block is
    // mapped or not (bit of the mask); a mapped block gets its region's offset added to the address, in units of
    // 256 bytes, giving 20 bits. Programmed by MAP from A, X, Y and Z
    byte mapMask;            // bit n = block n is mapped
    int lowerOffset;         // bytes, multiple of 256
    int upperOffset;
    bool mapInhibit;         // after MAP all interrupts are inhibited until EOM (a NOP)

    internal int LowerOffset => lowerOffset;
    internal int UpperOffset => upperOffset;
    internal byte MapMask => mapMask;

    void Map()
    {
        lowerOffset = ((X & 0x0F) << 16) | (A << 8);
        upperOffset = ((Z & 0x0F) << 16) | (Y << 8);
        mapMask = (byte)((X >> 4) | (Z & 0xF0));
        mapInhibit = true;
    }

    // Logical (CPU) address -> physical address on A0-A19
    internal (int Address, bool Mapped) Translate(ushort address)
    {
        int block = address >> 13;
        if ((mapMask & (1 << block)) == 0) return (address, false);
        return ((address + (block < 4 ? lowerOffset : upperOffset)) & 0xFFFFF, true);
    }

    // One bus access per cycle
    byte ReadByte(ushort address)
    {
        var (physical, mapped) = Translate(address);
        return Bus?.Read(physical, mapped) ?? 0xFF;
    }

    void WriteByte(ushort address, byte value)
    {
        var (physical, mapped) = Translate(address);
        Bus?.Write(physical, mapped, value);
    }

    ushort ReadWord(ushort address) => (ushort)(ReadByte(address) | (ReadByte((ushort)(address + 1)) << 8));

    // Base page: the B register is the high byte of the address; the 8-bit offset wraps inside the page
    ushort BasePage(byte offset) => (ushort)((B << 8) | offset);

    // ---- Stack ------------------------------------------------------------------------------------------
    // SP points to the next free location. In byte mode (E set) only SPL changes and wraps inside the stack
    // page (SPH); in word mode the whole 16-bit pointer moves
    void Push(byte value)
    {
        WriteByte(SP, value);
        if ((Status & FLAG_E) != 0) SPL--; else SP--;
    }

    byte Pop()
    {
        if ((Status & FLAG_E) != 0) SPL++; else SP++;
        return ReadByte(SP);
    }

    // ---- Flags ------------------------------------------------------------------------------------------
    void SetZN(byte value) =>
        Status = (byte)((Status & ~(FLAG_Z | FLAG_N)) | (value == 0 ? FLAG_Z : 0) | (value & FLAG_N));

    void SetZN16(int value) =>
        Status = (byte)((Status & ~(FLAG_Z | FLAG_N)) | ((value & 0xFFFF) == 0 ? FLAG_Z : 0) | ((value >> 8) & FLAG_N));

    void SetFlag(byte flag, bool on) => Status = (byte)(on ? Status | flag : Status & ~flag);

    // PLP and RTI restore N V D I Z C: B and E are not touched
    void PullStatus(byte value) =>
        Status = (byte)((value & ~(FLAG_B | FLAG_E)) | (Status & (FLAG_B | FLAG_E)));
}
