namespace ThingMOS;

// ALU: operations on values and flags, without bus accesses
public partial class MOS6502
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
        if ((Status & FLAG_D) != 0) return ADCDecimal(val);

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
        if ((Status & FLAG_D) != 0) return SBCDecimal(val);

        return ADC((byte)(~val));
    }

    // Decimal mode of the NMOS 6510 (same algorithm as VICE): Z comes from the binary result,
    // N and V from the intermediate value before the high digit is corrected
    private byte ADCDecimal(byte val)
    {
        int carry = (Status & FLAG_C) != 0 ? 1 : 0;
        int tmp = (A & 0x0F) + (val & 0x0F) + carry;
        if (tmp > 0x09) tmp += 0x06;
        tmp = tmp <= 0x0F
            ? (tmp & 0x0F) + (A & 0xF0) + (val & 0xF0)
            : (tmp & 0x0F) + (A & 0xF0) + (val & 0xF0) + 0x10;

        byte flags = (byte)(Status & ~(FLAG_C | FLAG_Z | FLAG_V | FLAG_N));
        if (((A + val + carry) & 0xFF) == 0) flags |= FLAG_Z;
        if ((tmp & 0x80) != 0) flags |= FLAG_N;
        if (((A ^ tmp) & 0x80) != 0 && ((A ^ val) & 0x80) == 0) flags |= FLAG_V;
        if ((tmp & 0x1F0) > 0x90) tmp += 0x60;
        if ((tmp & 0xFF0) > 0xF0) flags |= FLAG_C;

        Status = flags;
        A = (byte)tmp;
        return A;
    }

    // In decimal SBC all flags come from the binary result, only A is BCD-corrected
    private byte SBCDecimal(byte val)
    {
        int borrow = (Status & FLAG_C) != 0 ? 0 : 1;
        int binary = A - val - borrow;

        int tmp = (A & 0x0F) - (val & 0x0F) - borrow;
        tmp = (tmp & 0x10) != 0
            ? ((tmp - 6) & 0x0F) | ((A & 0xF0) - (val & 0xF0) - 0x10)
            : (tmp & 0x0F) | ((A & 0xF0) - (val & 0xF0));
        if ((tmp & 0x100) != 0) tmp -= 0x60;

        byte flags = (byte)(Status & ~(FLAG_C | FLAG_Z | FLAG_V | FLAG_N));
        if (binary >= 0) flags |= FLAG_C;
        if ((binary & 0xFF) == 0) flags |= FLAG_Z;
        if ((binary & 0x80) != 0) flags |= FLAG_N;
        if (((A ^ binary) & 0x80) != 0 && ((A ^ val) & 0x80) != 0) flags |= FLAG_V;

        Status = flags;
        A = (byte)tmp;
        return A;
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
}
