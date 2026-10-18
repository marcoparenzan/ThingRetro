namespace ThingRetroEmu.Chips;

// MOS 6502 NMOS: the CPU core, without on-chip peripherals. The C64's 6510 extends it with the
// I/O port at $0000/$0001 (see MOS6510)
public partial class MOS6502 : I6502Pinout
{
    // Pins wired by the board
    public IBus? Bus { get; set; }
    public Func<bool> IrqInput { get; set; } = () => false;
    public Func<bool> NmiInput { get; set; } = () => false;
    public Func<bool> RdyInput { get; set; } = () => true;
    public Func<bool> AecInput { get; set; } = () => true;

    // SO: a falling edge sets the V flag (the 1541 signals "byte ready" to its CPU this way). High when not connected
    public Func<bool> SoInput { get; set; } = () => true;

    // /RES
    public void Reset()
    {
        OnReset();
        SP = 0xFD;
        Status = 0x34; // or 0x20?
        cycle = 0;
        accessCycle = 0;
        inInterrupt = false;
        interruptPending = false;
        jammed = false;
        nmiPending = false;
        nmiWasLow = false;
        // Simplification: the chip takes 7 cycles before reading the vector; here it is read at once
        PC = ReadWord(0xFFFC);
    }

    // Reset of the on-chip peripherals, before the vector is read
    protected virtual void OnReset() { }

    // internal so that the tests can set up and check the CPU state
    internal byte A, X, Y, SP;
    internal ushort PC;
    internal byte Status;

    const byte FLAG_C = 1 << 0;
    const byte FLAG_Z = 1 << 1;
    const byte FLAG_I = 1 << 2;
    const byte FLAG_D = 1 << 3;
    const byte FLAG_B = 1 << 4;
    const byte FLAG_U = 1 << 5; // UNUSED
    const byte FLAG_V = 1 << 6;
    const byte FLAG_N = 1 << 7;

    // One bus access per cycle
    protected virtual byte ReadByte(ushort addr) => Bus?.Read(addr) ?? 0xFF;

    protected virtual void WriteByte(ushort addr, byte val) => Bus?.Write(addr, val);

    private ushort ReadWord(ushort addr) => (ushort)(ReadByte(addr) | (ReadByte((ushort)(addr + 1)) << 8));

    private void SetZeroNegativeFlags(byte val)
    {
        Status = (byte)((Status & ~(FLAG_Z | FLAG_N)) |
                        (val == 0 ? FLAG_Z : 0) |
                        (val & 0x80));
    }

    private void SetZN(byte value)
    {
        // Set Zero flag if value is zero
        if (value == 0)
            Status |= FLAG_Z;
        else
            Status &= unchecked((byte)~FLAG_Z);

        // Set Negative flag if bit 7 is set
        if ((value & 0x80) != 0)
            Status |= FLAG_N;
        else
            Status &= unchecked((byte)~FLAG_N);
    }

    private void Compare(byte reg, byte val)
    {
        int result = reg - val;

        // Carry (C) set if reg >= val
        Status = (byte)((Status & ~FLAG_C) | (reg >= val ? FLAG_C : 0));

        // Zero (Z) set if reg == val
        Status = (byte)((Status & ~FLAG_Z) | ((result == 0) ? FLAG_Z : 0));

        // Negative (N) from the most significant bit of the result
        Status = (byte)((Status & ~FLAG_N) | ((result & 0x80) != 0 ? FLAG_N : 0));
    }
}
