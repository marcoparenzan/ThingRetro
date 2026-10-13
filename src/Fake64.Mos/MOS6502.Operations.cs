namespace Fake64.Mos;

// What each operation does to the data, once the cycle engine has done the bus accesses
public partial class MOS6502
{
    // Instructions without a memory operand (also those on the accumulator: ASL A, ROR A...)
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
            case Op.TAX: X = A; SetZN(X); break;
            case Op.TAY: Y = A; SetZN(Y); break;
            case Op.TXA: A = X; SetZN(A); break;
            case Op.TYA: A = Y; SetZN(A); break;
            case Op.TSX: X = SP; SetZN(X); break;
            case Op.TXS: SP = X; break;
            case Op.INX: X++; SetZN(X); break;
            case Op.INY: Y++; SetZN(Y); break;
            case Op.DEX: X--; SetZN(X); break;
            case Op.DEY: Y--; SetZN(Y); break;
            case Op.ASL: A = ASL(A); break;
            case Op.LSR: A = LSR(A); break;
            case Op.ROL: A = ROL(A); break;
            case Op.ROR: A = ROR(A); break;
            case Op.NOP: break;
            default: throw new InvalidOperationException($"{op} is not an implied instruction");
        }
    }

    // Instructions that read a value (immediate or from memory)
    void ExecuteRead(Op op, byte value)
    {
        switch (op)
        {
            case Op.LDA: LDA(value); break;
            case Op.LDX: LDX(value); break;
            case Op.LDY: LDY(value); break;
            case Op.ADC: ADC(value); break;
            case Op.SBC: SBC(value); break;
            case Op.AND: AND(value); break;
            case Op.ORA: ORA(value); break;
            case Op.EOR: EOR(value); break;
            case Op.CMP: Compare(A, value); break;
            case Op.CPX: Compare(X, value); break;
            case Op.CPY: Compare(Y, value); break;
            case Op.BIT: BIT(value); break;
            case Op.NOP: break;
            case Op.LAX: A = X = value; SetZN(value); break;
            case Op.LAS: A = X = SP = (byte)(value & SP); SetZN(A); break;
            case Op.ANC:
                AND(value);
                Status = (byte)((Status & ~FLAG_C) | ((A & 0x80) != 0 ? FLAG_C : 0));
                break;
            case Op.ALR:
                AND(value);
                A = LSR(A);
                break;
            case Op.ARR:
                if ((Status & FLAG_D) != 0) ArrDecimal(value);
                else
                {
                    // AND then ROR, with C = bit 6 and V = bit 6 xor bit 5 of the result
                    A = (byte)(((A & value) >> 1) | ((Status & FLAG_C) != 0 ? 0x80 : 0));
                    SetZN(A);
                    Status = (byte)((Status & ~(FLAG_C | FLAG_V)) |
                                    ((A & 0x40) != 0 ? FLAG_C : 0) |
                                    (((A >> 6) ^ (A >> 5)) & 1) * FLAG_V);
                }
                break;
            case Op.AXS:
                {
                    int result = (A & X) - value;
                    X = (byte)result;
                    Status = (byte)((Status & ~FLAG_C) | (result >= 0 ? FLAG_C : 0));
                    SetZN(X);
                    break;
                }
            case Op.XAA: A = (byte)((A | 0xEE) & X & value); SetZN(A); break; // unstable: constant $EE
            case Op.LXA: A = X = (byte)((A | 0xEE) & value); SetZN(A); break;  // unstable: constant $EE
            default: throw new InvalidOperationException($"{op} is not a read instruction");
        }
    }

    // ARR in decimal mode (NMOS 6502, as in VICE): N is the previous carry, Z and V come from the result
    // of the rotation, then each digit is BCD-corrected; C tells whether the high digit was corrected
    void ArrDecimal(byte value)
    {
        int and = A & value;
        bool carryIn = (Status & FLAG_C) != 0;
        int result = (and | (carryIn ? 0x100 : 0)) >> 1;

        byte flags = (byte)(Status & ~(FLAG_N | FLAG_Z | FLAG_V | FLAG_C));
        if (carryIn) flags |= FLAG_N;
        if (result == 0) flags |= FLAG_Z;
        if (((result ^ and) & 0x40) != 0) flags |= FLAG_V;

        if ((and & 0x0F) + (and & 0x01) > 0x05)
            result = (result & 0xF0) | ((result + 0x06) & 0x0F);
        if ((and & 0xF0) + (and & 0x10) > 0x50)
        {
            result = (result & 0x0F) | ((result + 0x60) & 0xF0);
            flags |= FLAG_C;
        }

        Status = flags;
        A = (byte)result;
    }

    // Value a write instruction puts on the bus. For SHA/SHX/SHY/TAS it is ANDed with the high byte of the
    // base address + 1 (unstable behaviour of the chip, reproduced in its most common form)
    byte WriteValue(Op op)
    {
        byte high = (byte)((baseAddress >> 8) + 1);
        return op switch
        {
            Op.STA => A,
            Op.STX => X,
            Op.STY => Y,
            Op.SAX => (byte)(A & X),
            Op.SHA => (byte)(A & X & high),
            Op.SHX => (byte)(X & high),
            Op.SHY => (byte)(Y & high),
            Op.TAS => (byte)((SP = (byte)(A & X)) & high),
            _ => throw new InvalidOperationException($"{op} is not a write instruction"),
        };
    }

    // Instructions that read, modify and write back memory; returns the value to write
    byte ExecuteReadModifyWrite(Op op, byte value)
    {
        switch (op)
        {
            case Op.ASL: return ASL(value);
            case Op.LSR: return LSR(value);
            case Op.ROL: return ROL(value);
            case Op.ROR: return ROR(value);
            case Op.INC: return INC(value);
            case Op.DEC: return DEC(value);
            case Op.SLO: value = ASL(value); ORA(value); return value;
            case Op.RLA: value = ROL(value); AND(value); return value;
            case Op.SRE: value = LSR(value); EOR(value); return value;
            case Op.RRA: value = ROR(value); ADC(value); return value;
            case Op.DCP: value--; Compare(A, value); return value;
            case Op.ISC: value++; SBC(value); return value;
            default: throw new InvalidOperationException($"{op} is not a read-modify-write instruction");
        }
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
        _ => throw new InvalidOperationException($"{op} is not a branch"),
    };
}
