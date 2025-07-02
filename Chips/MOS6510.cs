using System;
using System.Collections.Generic;

namespace Chips;

public partial class MOS6510(Board board)
{
    object bus_lock = new();
    byte[] bytes = new byte[0x0002];

    public byte Address(ushort addr)
    {
        lock (bus_lock)
        {
            return bytes[addr];
        }
    }

    public void Address(ushort addr, byte value)
    {
        lock (bus_lock)
        {
            bytes[addr] = value;
        }
    }

    byte A, X, Y, SP;
    ushort PC;
    byte Status;

    const byte FLAG_C = 1 << 0;
    const byte FLAG_Z = 1 << 1;
    const byte FLAG_I = 1 << 2;
    const byte FLAG_D = 1 << 3;
    const byte FLAG_B = 1 << 4;
    const byte FLAG_U = 1 << 5;
    const byte FLAG_V = 1 << 6;
    const byte FLAG_N = 1 << 7;

    byte ioRegister0 = 0x2F;
    byte ioRegister1 = 0x37;

    public void Reset()
    {
        PC = ReadWord(0xFFFC);
        SP = 0xFD;
        Status = 0x20;
    }

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