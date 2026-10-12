namespace Fake64.Mos;

// MOS 6502 NMOS: il nucleo della CPU, senza periferiche integrate. La 6510 del C64 lo estende con la
// porta di I/O a $0000/$0001 (vedi MOS6510)
public partial class MOS6502
{
    // Pin collegati dalla scheda
    public IBus? Bus { get; set; }
    public Func<bool> IrqInput { get; set; } = () => false;
    public Func<bool> NmiInput { get; set; } = () => false;
    public Func<bool> RdyInput { get; set; } = () => true;
    public Func<bool> AecInput { get; set; } = () => true;

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
        // Semplificazione: il chip impiega 7 cicli prima di leggere il vettore; qui lo legge subito
        PC = ReadWord(0xFFFC);
    }

    // Reset delle periferiche integrate, prima della lettura del vettore
    protected virtual void OnReset() { }

    // internal per permettere ai test di preparare e verificare lo stato della CPU
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

    // Un accesso al bus per ciclo
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

        // Imposta il flag Carry (C) se reg >= val
        Status = (byte)((Status & ~FLAG_C) | (reg >= val ? FLAG_C : 0));

        // Imposta il flag Zero (Z) se reg == val
        Status = (byte)((Status & ~FLAG_Z) | ((result == 0) ? FLAG_Z : 0));

        // Imposta il flag Negative (N) in base al bit più significativo del risultato
        Status = (byte)((Status & ~FLAG_N) | ((result & 0x80) != 0 ? FLAG_N : 0));
    }
}
