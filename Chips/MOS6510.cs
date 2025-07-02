namespace Chips;

public partial class MOS6510
{
    Board board;

    public MOS6510(Board board)
    {
        this.board = board;
        Reset();
    }
    public void Reset()
    {
        PC = ReadWord(0xFFFC);
        SP = 0xFD;
        Status = 0x34; // or 0x20?
        ioRegister0 = 0x2F;
        ioRegister1 = 0x37;
    }

    public byte Address(ushort addr)
    {
        switch(addr)
        {
            case 0x0000: return ioRegister0;
            case 0x0001: return ioRegister1;
            default:
                throw new InvalidOperationException();
        }
    }

    public void Address(ushort addr, byte value)
    {
        switch (addr)
        {
            case 0x0000: 
                ioRegister0 = value;
                break;
            case 0x0001: 
                ioRegister1 = value;
                break;
            default:
                throw new InvalidOperationException();
        }
    }

    byte A, X, Y, SP;
    ushort PC;
    byte Status;
    byte ioRegister0;
    byte ioRegister1;

    const byte FLAG_C = 1 << 0;
    const byte FLAG_Z = 1 << 1;
    const byte FLAG_I = 1 << 2;
    const byte FLAG_D = 1 << 3;
    const byte FLAG_B = 1 << 4;
    const byte FLAG_U = 1 << 5; // UNUSED
    const byte FLAG_V = 1 << 6;
    const byte FLAG_N = 1 << 7;

    private byte ReadByte(ushort addr)
    {
        return board.Address(addr);
    }

    private void WriteByte(ushort addr, byte val)
    {
        board.Address(addr, val);
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
            PC = (ushort)(PC + offset);
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