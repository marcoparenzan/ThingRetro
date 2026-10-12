using System;
using System.Collections.Generic;

namespace Fake64.Mos;

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

    // Decimal mode del 6510 NMOS (stesso algoritmo di VICE): Z viene dal risultato binario,
    // N e V dal valore intermedio prima della correzione della cifra alta
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

    // In SBC decimale tutti i flag vengono dal risultato binario, solo A è corretto in BCD
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


    private void JSR(ushort addr)
    {
        // PC punta al primo byte dell'operando: si salva l'indirizzo dell'ultimo byte di JSR
        // (indirizzo di ritorno - 1), come fa il 6502 reale
        ushort returnAddress = (ushort)(PC + 1);
        Push((byte)(returnAddress >> 8)); // High byte
        Push((byte)returnAddress);        // Low byte
        PC = addr;
    }

    private ushort RTS()
    {
        // Pull the return address from the stack: RTS salta all'indirizzo letto + 1
        byte low = Pop();
        byte high = Pop();
        return (ushort)(((high << 8) | low) + 1);
    }

    private void RTI()
    {
        Status = (byte)((Pop() & ~FLAG_B) | FLAG_U);
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
        // BRK occupa due byte (opcode + byte di padding): si salva l'indirizzo dopo il padding
        PC++;
        Push((byte)(PC >> 8)); // Push high byte of PC
        Push((byte)PC);        // Push low byte of PC
        Push((byte)(Status | FLAG_B | FLAG_U)); // Push status register with Break flag set
        Status |= FLAG_I;      // Set Interrupt Disable flag
        PC = ReadWord(0xFFFE); // Jump to interrupt vector
    }
    /// <summary>
    /// RRA - Illegal opcode that performs ROR followed by ADC on the same memory location
    /// </summary>
    /// <param name="address">The memory address to operate on</param>
    private void RRA(ushort address)
    {
        // Read the value from memory
        byte value = ReadByte(address);

        // Perform ROR (Rotate Right)
        byte rotatedValue = ROR(value);

        // Write the rotated value back to memory
        WriteByte(address, rotatedValue);

        // Perform ADC with the rotated value
        A = ADC(rotatedValue);
    }

    /// <summary>
    /// LAX - Illegal opcode that loads a value into both the Accumulator and X register
    /// </summary>
    /// <param name="value">The value to load</param>
    /// <returns>The loaded value</returns>
    private byte LAX(byte value)
    {
        // Set the zero and negative flags based on the value
        SetZN(value);

        // Return the value (which will be assigned to both A and X)
        return value;
    }
    /// <summary>
    /// DCP - Illegal opcode that performs DEC followed by CMP at the same memory location
    /// </summary>
    /// <param name="address">The memory address to operate on</param>
    private void DCP(ushort address)
    {
        // Read the value from memory
        byte value = ReadByte(address);

        // Decrement the value
        value--;

        // Write the decremented value back to memory
        WriteByte(address, value);

        // Compare the accumulator with the decremented value
        Compare(A, value);
    }
}