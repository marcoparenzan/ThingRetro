using System;
using System.Collections.Generic;

namespace Chips;

public partial class MOS6510
{
    private byte ASL(byte val)
    {
        Status = (byte)((Status & ~FLAG_C) | ((val & 0x80) >> 7));
        val <<= 1;
        SetZeroNegativeFlags(val);
        return val;
    }

    private byte LSR(byte val)
    {
        Status = (byte)((Status & ~FLAG_C) | (val & 0x01));
        val >>= 1;
        SetZeroNegativeFlags(val);
        return val;
    }

    private byte ROL(byte val)
    {
        bool carryIn = (Status & FLAG_C) != 0;
        bool carryOut = (val & 0x80) != 0;
        val = (byte)((val << 1) | (carryIn ? 1 : 0));
        Status = (byte)((Status & ~FLAG_C) | (carryOut ? FLAG_C : 0));
        SetZeroNegativeFlags(val);
        return val;
    }

    private byte ROR(byte val)
    {
        bool carryIn = (Status & FLAG_C) != 0;
        bool carryOut = (val & 0x01) != 0;
        val = (byte)((val >> 1) | (carryIn ? 0x80 : 0));
        Status = (byte)((Status & ~FLAG_C) | (carryOut ? FLAG_C : 0));
        SetZeroNegativeFlags(val);
        return val;
    }

    private byte INC(byte val)
    {
        val++;
        SetZeroNegativeFlags(val);
        return val;
    }

    private byte DEC(byte val)
    {
        val--;
        SetZeroNegativeFlags(val);
        return val;
    }

    private byte ADC(byte val)
    {
        int result = A + val + ((Status & FLAG_C) != 0 ? 1 : 0);
        Status = (byte)((Status & ~(FLAG_C | FLAG_V)) |
                        (result > 0xFF ? FLAG_C : 0) |
                        (((A ^ result) & (val ^ result) & 0x80) != 0 ? FLAG_V : 0));
        A = (byte)result;
        SetZeroNegativeFlags(A);
        return A;
    }

    private byte SBC(byte val)
    {
        return ADC((byte)(~val));
    }

    private byte AND(byte val)
    {
        A &= val;
        SetZeroNegativeFlags(A);
        return A;
    }

    private byte EOR(byte val)
    {
        A ^= val;
        SetZeroNegativeFlags(A);
        return A;
    }

    private byte ORA(byte val)
    {
        A |= val;
        SetZeroNegativeFlags(A);
        return A;
    }

    private byte LDA(byte val)
    {
        A = val;
        SetZeroNegativeFlags(A);
        return A;
    }

    private byte LDX(byte val)
    {
        X = val;
        SetZeroNegativeFlags(X);
        return X;
    }

    private byte LDY(byte val)
    {
        Y = val;
        SetZeroNegativeFlags(Y);
        return Y;
    }

    private void BIT(byte val)
    {
        Status = (byte)((Status & ~(FLAG_Z | FLAG_N | FLAG_V)) |
                        (val & FLAG_N) |
                        (val & FLAG_V) |
                        ((A & val) == 0 ? FLAG_Z : 0));
    }


    private void JSR(ushort addr)
    {
        // Push the return address (PC - 1) onto the stack
        Push((byte)((PC + 1) >> 8)); // High byte
        Push((byte)(PC + 1));        // Low byte
        PC = addr;
    }

    private ushort RTS()
    {
        // Pull the return address from the stack
        byte low = Pop();
        byte high = Pop();
        return (ushort)((high << 8) | low);
    }

    private void RTI()
    {
        Status = (byte)(Pop() & ~FLAG_B);
        byte low = Pop();
        byte high = Pop();
        PC = (ushort)((high << 8) | low);
    }


    private byte SLO(ushort addr)
    {
        byte value = ASL(ReadByte(addr));
        WriteByte(addr, value);
        A = ORA(value);
        return A;
    }

    private byte RLA(ushort addr)
    {
        byte value = ROL(ReadByte(addr));
        WriteByte(addr, value);
        A = AND(value);
        return A;
    }

    private byte SRE(ushort addr)
    {
        byte value = LSR(ReadByte(addr));
        WriteByte(addr, value);
        A = EOR(value);
        return A;
    }

    private byte ISC(ushort addr)
    {
        byte value = INC(ReadByte(addr));
        WriteByte(addr, value);
        A = SBC(value);
        return A;
    }

    private void BRK()
    {
        // Simulate BRK instruction behavior
        Push((byte)(PC >> 8)); // Push high byte of PC
        Push((byte)PC);        // Push low byte of PC
        Push((byte)(Status | FLAG_B)); // Push status register with Break flag set
        Status |= FLAG_I;      // Set Interrupt Disable flag
        PC = ReadWord(0xFFFE); // Jump to interrupt vector
    }
}