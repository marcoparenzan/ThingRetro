namespace Fake64.Mos;

public partial class MOS6510 : I6510Pinout
{
    // Pin collegati dalla scheda
    public IBus? Bus { get; set; }
    public Func<bool> IrqInput { get; set; } = () => false;
    public Func<bool> NmiInput { get; set; } = () => false;
    public Func<bool> RdyInput { get; set; } = () => true;

    // P0-P5: la porta di I/O integrata, ai registri $0000 (direzione) e $0001 (dati)
    public ParallelPort Port { get; } = new();

    public void Reset()
    {
        // Al reset la porta è tutta in ingresso: i pin valgono quanto imposto dall'esterno (pull-up).
        // Il Changed della porta permette alla scheda di aggiornare la mappa di memoria prima del vettore
        Port.Reset();
        SP = 0xFD;
        Status = 0x34; // or 0x20?
        cyclesRemaining = 0;
        nmiPending = false;
        nmiWasLow = false;
        PC = ReadWord(0xFFFC);
    }

    // Un ciclo di clock: /NMI viene campionato sempre, il resto solo se RDY è alto
    public void Tick()
    {
        SampleNmi();
        if (!RdyInput()) return;
        ProcessSignals();
        Clock();
    }

    // Registri della porta come li vede la CPU: $00 = direzione, $01 = livello dei pin
    public byte ReadPort(ushort addr) => addr == 0 ? Port.Direction : Port.Pins;

    public void WritePort(ushort addr, byte value)
    {
        if (addr == 0) Port.Direction = value;
        else Port.Latch = value;
    }

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

    // $0000 e $0001 sono la porta integrata: la lettura non va sul bus, la scrittura sì
    // (sul C64 finisce anche nella RAM sottostante)
    private byte ReadByte(ushort addr)
    {
        if (addr <= 0x0001) return ReadPort(addr);
        return Bus?.Read(addr) ?? 0xFF;
    }

    private void WriteByte(ushort addr, byte val)
    {
        if (addr <= 0x0001) WritePort(addr, val);
        Bus?.Write(addr, val);
    }

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

    private void Branch(bool condition)
    {
        if (condition)
        {
            sbyte offset = (sbyte)ReadByte(PC++);
            ushort target = (ushort)(PC + offset);
            pageCrossed = (PC & 0xFF00) != (target & 0xFF00);
            PC = target;
        }
        else
        {
            PC++;
        }
    }

    private void Push(byte val)
    {
        WriteByte((ushort) (0x0100 + SP--), val);
    }

    private byte Pop()
    {
        return ReadByte((ushort)(0x0100 + ++SP));
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