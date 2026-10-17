namespace Thing65.Mos;

// What each operation does to the data, once the cycle engine has done the bus accesses
public partial class MOS4510
{
    // Instructions without a memory operand (also those on the accumulator: ASL A, INC A...)
    void ExecuteImplied(Op op)
    {
        switch (op)
        {
            case Op.CLC: Status &= unchecked((byte)~FLAG_C); break;
            case Op.SEC: Status |= FLAG_C; break;
            case Op.CLI: Status &= unchecked((byte)~FLAG_I); break;
            case Op.SEI: Status |= FLAG_I; break;
            case Op.CLV: Status &= unchecked((byte)~FLAG_V); break;
            case Op.CLD: Status &= unchecked((byte)~FLAG_D); break;
            case Op.SED: Status |= FLAG_D; break;
            case Op.CLE: Status &= unchecked((byte)~FLAG_E); break; // 16-bit stack pointer
            case Op.SEE: Status |= FLAG_E; break;                   // 8-bit stack pointer
            case Op.TAX: X = A; SetZN(X); break;
            case Op.TAY: Y = A; SetZN(Y); break;
            case Op.TAZ: Z = A; SetZN(Z); break;
            case Op.TXA: A = X; SetZN(A); break;
            case Op.TYA: A = Y; SetZN(A); break;
            case Op.TZA: A = Z; SetZN(A); break;
            case Op.TAB: B = A; break;
            case Op.TBA: A = B; SetZN(A); break;
            case Op.TSX: X = SPL; SetZN(X); break;
            case Op.TSY: Y = SPH; SetZN(Y); break;
            case Op.TXS: SPL = X; break;
            case Op.TYS: SPH = Y; break;
            case Op.INX: X++; SetZN(X); break;
            case Op.INY: Y++; SetZN(Y); break;
            case Op.INZ: Z++; SetZN(Z); break;
            case Op.DEX: X--; SetZN(X); break;
            case Op.DEY: Y--; SetZN(Y); break;
            case Op.DEZ: Z--; SetZN(Z); break;
            case Op.NEG: A = (byte)-A; SetZN(A); break;
            case Op.ASL or Op.LSR or Op.ROL or Op.ROR or Op.ASR or Op.INC or Op.DEC:
                A = ModifyByte(op, A);
                break;
            case Op.NOP: mapInhibit = false; break; // EOM: ends the mapping sequence started by MAP
            case Op.MAP: Map(); break;
            default: throw new InvalidOperationException($"{op} is not an implied instruction");
        }
    }

    // Instructions that read a value (immediate or from memory)
    void ExecuteRead(Op op, byte value)
    {
        switch (op)
        {
            case Op.LDA: A = value; SetZN(A); break;
            case Op.LDX: X = value; SetZN(X); break;
            case Op.LDY: Y = value; SetZN(Y); break;
            case Op.LDZ: Z = value; SetZN(Z); break;
            case Op.ADC: ADC(value); break;
            case Op.SBC: SBC(value); break;
            case Op.AND: A &= value; SetZN(A); break;
            case Op.ORA: A |= value; SetZN(A); break;
            case Op.EOR: A ^= value; SetZN(A); break;
            case Op.CMP: Compare(A, value); break;
            case Op.CPX: Compare(X, value); break;
            case Op.CPY: Compare(Y, value); break;
            case Op.CPZ: Compare(Z, value); break;
            case Op.BIT:
                if (instruction.Mode == Mode.Imm) SetFlag(FLAG_Z, (A & value) == 0); // BIT #imm only touches Z
                else
                    Status = (byte)((Status & ~(FLAG_Z | FLAG_N | FLAG_V)) | (value & (FLAG_N | FLAG_V)) |
                                    ((A & value) == 0 ? FLAG_Z : 0));
                break;
            default: throw new InvalidOperationException($"{op} is not a read instruction");
        }
    }

    // Value a write instruction puts on the bus (STZ stores the Z register, not zero)
    byte WriteValue(Op op) => op switch
    {
        Op.STA => A,
        Op.STX => X,
        Op.STY => Y,
        Op.STZ => Z,
        _ => throw new InvalidOperationException($"{op} is not a write instruction"),
    };

    // Instructions that read, modify and write back a byte; returns the value to write.
    // The 65CE02 writes it once (no double write as in the NMOS 6502)
    byte ModifyByte(Op op, byte value)
    {
        switch (op)
        {
            case Op.ASL:
                SetFlag(FLAG_C, (value & 0x80) != 0);
                value <<= 1;
                SetZN(value);
                return value;
            case Op.LSR:
                SetFlag(FLAG_C, (value & 0x01) != 0);
                value >>= 1;
                SetZN(value);
                return value;
            case Op.ASR: // arithmetic: bit 7 stays
                SetFlag(FLAG_C, (value & 0x01) != 0);
                value = (byte)((value >> 1) | (value & 0x80));
                SetZN(value);
                return value;
            case Op.ROL:
                {
                    int carryIn = (Status & FLAG_C) != 0 ? 1 : 0;
                    SetFlag(FLAG_C, (value & 0x80) != 0);
                    value = (byte)((value << 1) | carryIn);
                    SetZN(value);
                    return value;
                }
            case Op.ROR:
                {
                    int carryIn = (Status & FLAG_C) != 0 ? 0x80 : 0;
                    SetFlag(FLAG_C, (value & 0x01) != 0);
                    value = (byte)((value >> 1) | carryIn);
                    SetZN(value);
                    return value;
                }
            case Op.INC: value++; SetZN(value); return value;
            case Op.DEC: value--; SetZN(value); return value;
            case Op.TSB: SetFlag(FLAG_Z, (A & value) == 0); return (byte)(value | A);
            case Op.TRB: SetFlag(FLAG_Z, (A & value) == 0); return (byte)(value & ~A);
            case Op.RMB: return (byte)(value & ~(1 << ((opcode >> 4) & 7)));
            case Op.SMB: return (byte)(value | (1 << ((opcode >> 4) & 7)));
            default: throw new InvalidOperationException($"{op} is not a read-modify-write instruction");
        }
    }

    // Instructions that read, modify and write back a 16-bit word (low byte first)
    ushort ModifyWord(Op op, ushort value)
    {
        int result;
        switch (op)
        {
            case Op.INW: result = value + 1; break;
            case Op.DEW: result = value - 1; break;
            case Op.ASW:
                SetFlag(FLAG_C, (value & 0x8000) != 0);
                result = value << 1;
                break;
            case Op.ROW:
                {
                    int carryIn = (Status & FLAG_C) != 0 ? 1 : 0;
                    SetFlag(FLAG_C, (value & 0x8000) != 0);
                    result = (value << 1) | carryIn;
                    break;
                }
            default: throw new InvalidOperationException($"{op} is not a word read-modify-write instruction");
        }
        SetZN16(result);
        return (ushort)result;
    }

    bool BranchTaken(Op op) => op switch
    {
        Op.BPL => (Status & FLAG_N) == 0,
        Op.BMI => (Status & FLAG_N) != 0,
        Op.BVC => (Status & FLAG_V) == 0,
        Op.BVS => (Status & FLAG_V) != 0,
        Op.BCC => (Status & FLAG_C) == 0,
        Op.BCS => (Status & FLAG_C) != 0,
        Op.BNE => (Status & FLAG_Z) == 0,
        Op.BEQ => (Status & FLAG_Z) != 0,
        Op.BRA or Op.BSR => true,
        _ => throw new InvalidOperationException($"{op} is not a branch"),
    };

    // C is set if reg >= value; N and Z come from the subtraction. V is not touched
    void Compare(byte reg, byte value)
    {
        int result = reg - value;
        Status = (byte)((Status & ~(FLAG_C | FLAG_Z | FLAG_N)) |
                        (reg >= value ? FLAG_C : 0) |
                        ((byte)result == 0 ? FLAG_Z : 0) |
                        (result & FLAG_N));
    }

    void ADC(byte value)
    {
        int carry = (Status & FLAG_C) != 0 ? 1 : 0;
        if ((Status & FLAG_D) != 0) { ADCDecimal(value, carry); return; }

        int result = A + value + carry;
        Status = (byte)((Status & ~(FLAG_C | FLAG_V)) |
                        (result > 0xFF ? FLAG_C : 0) |
                        (((A ^ result) & (value ^ result) & 0x80) != 0 ? FLAG_V : 0));
        A = (byte)result;
        SetZN(A);
    }

    void SBC(byte value)
    {
        int carry = (Status & FLAG_C) != 0 ? 1 : 0;
        if ((Status & FLAG_D) != 0) { SBCDecimal(value, carry); return; }

        int result = A - value - (1 - carry);
        Status = (byte)((Status & ~(FLAG_C | FLAG_V)) |
                        (result >= 0 ? FLAG_C : 0) |
                        (((A ^ value) & (A ^ result) & 0x80) != 0 ? FLAG_V : 0));
        A = (byte)result;
        SetZN(A);
    }

    // Decimal mode of the CMOS 6502 family (the algorithm of Bruce Clark's "Decimal Mode" for the 65C02):
    // N and Z describe the BCD result, V comes from the signed sum before the high digit is corrected
    void ADCDecimal(byte value, int carry)
    {
        int low = (A & 0x0F) + (value & 0x0F) + carry;
        if (low >= 0x0A) low = ((low + 0x06) & 0x0F) + 0x10;

        int signedSum = (sbyte)(A & 0xF0) + (sbyte)(value & 0xF0) + low;
        int sum = (A & 0xF0) + (value & 0xF0) + low;
        bool overflow = signedSum < -128 || signedSum > 127;
        if (sum >= 0xA0) sum += 0x60;

        A = (byte)sum;
        Status = (byte)((Status & ~(FLAG_C | FLAG_V)) | (sum >= 0x100 ? FLAG_C : 0) | (overflow ? FLAG_V : 0));
        SetZN(A);
    }

    // In decimal SBC the carry and V are those of the binary subtraction; N and Z describe the BCD result
    void SBCDecimal(byte value, int carry)
    {
        int binary = A - value - (1 - carry);
        bool overflow = ((A ^ value) & (A ^ binary) & 0x80) != 0;

        int low = (A & 0x0F) - (value & 0x0F) + carry - 1;
        if (low < 0) low = ((low - 0x06) & 0x0F) - 0x10;
        int result = (A & 0xF0) - (value & 0xF0) + low;
        if (result < 0) result -= 0x60;

        A = (byte)result;
        Status = (byte)((Status & ~(FLAG_C | FLAG_V)) | (binary >= 0 ? FLAG_C : 0) | (overflow ? FLAG_V : 0));
        SetZN(A);
    }
}
