using System;
using System.Collections.Generic;

namespace Chips;

public partial class MOS6510
{

    ulong cycleCount = 0; // Contatore totale dei cicli

    private static readonly byte[] opcodeCycles = [
        // Cicli per ogni opcode (valori di esempio, da verificare con la documentazione ufficiale del 6510)
        7, 6, 0, 0, 0, 3, 3, 0, 3, 2, 2, 0, 0, 4, 4, 0, // 0x00-0x0F
        2, 5, 0, 0, 0, 4, 4, 0, 6, 4, 4, 0, 0, 6, 6, 0, // 0x10-0x1F
        // ... (completa con i cicli per tutti gli opcode)
    ];

    public void Step()
    {
        // Gestione degli interrupt
        if (irqPending)
        {
            HandleIRQ();
            irqPending = false; // Resetta il flag IRQ
        }

        if (breakpoints.Contains(PC))
        {
            Console.WriteLine($"[BREAKPOINT] PC={PC:X4}");
            breakpointHit = true;
            return; // Interrompe l'esecuzione
        }

        breakpointHit = false; // Resetta il flag se non ci sono breakpoint
        byte opcode = ReadByte(PC++);
        ushort addr;
        byte val;

        try
        {
            // Incrementa il contatore di cicli
            cycleCount += opcodeCycles[opcode];

            switch (opcode)
            {
                // 0x00: BRK (Force Interrupt)
                case 0x00: BRK(); break;

                // 0x07: SLO zp (ASL + ORA, illegale)
                case 0x07: addr = ReadByte(PC++); SLO(addr); break;

                // 0x09: ORA imm (Logical Inclusive OR, Immediate)
                case 0x09: A = ORA(ReadByte(PC++)); break;

                // 0x0A: ASL A (Arithmetic Shift Left, Accumulator)
                case 0x0A: A = ASL(A); break;

                // 0x0F: SLO abs (ASL + ORA, illegale)
                case 0x0F: addr = ReadWord(PC); PC += 2; SLO(addr); break;

                // 0x10: BPL (Branch if Positive)
                case 0x10: Branch((Status & FLAG_N) == 0); break;

                // 0x17: SLO zp,X (ASL + ORA, illegale)
                case 0x17: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); SLO(addr); break;

                // 0x18: CLC (Clear Carry Flag)
                case 0x18: Status &= unchecked((byte)~FLAG_C); break;

                // 0x1F: SLO abs,X (ASL + ORA, illegale)
                case 0x1F: addr = (ushort)(ReadWord(PC) + X); PC += 2; SLO(addr); break;

                // 0x20: JSR abs (Jump to Subroutine)
                case 0x20: JSR(ReadWord(PC)); break;

                // 0x24: BIT zp (Test Bits in Memory, Zero Page)
                case 0x24: addr = ReadByte(PC++); BIT(ReadByte(addr)); break;

                // 0x27: RLA zp (ROL + AND, illegale)
                case 0x27: addr = ReadByte(PC++); RLA(addr); break;

                // 0x29: AND imm (Logical AND, Immediate)
                case 0x29: A = AND(ReadByte(PC++)); break;

                // 0x2A: ROL A (Rotate Left, Accumulator)
                case 0x2A: A = ROL(A); break;

                // 0x2C: BIT abs (Test Bits in Memory, Absolute)
                case 0x2C: addr = ReadWord(PC); PC += 2; BIT(ReadByte(addr)); break;

                // 0x2F: RLA abs (ROL + AND, illegale)
                case 0x2F: addr = ReadWord(PC); PC += 2; RLA(addr); break;

                // 0x30: BMI (Branch if Negative)
                case 0x30: Branch((Status & FLAG_N) != 0); break;

                // 0x37: RLA zp,X (ROL + AND, illegale)
                case 0x37: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); RLA(addr); break;

                // 0x38: SEC (Set Carry Flag)
                case 0x38: Status |= FLAG_C; break;

                // 0x3F: RLA abs,X (ROL + AND, illegale)
                case 0x3F: addr = (ushort)(ReadWord(PC) + X); PC += 2; RLA(addr); break;

                // 0x40: RTI (Return from Interrupt)
                case 0x40: RTI(); break;

                // 0x47: SRE zp (LSR + EOR, illegale)
                case 0x47: addr = ReadByte(PC++); SRE(addr); break;

                // 0x49: EOR imm (Exclusive OR, Immediate)
                case 0x49: A = EOR(ReadByte(PC++)); break;

                // 0x4A: LSR A (Logical Shift Right, Accumulator)
                case 0x4A: A = LSR(A); break;

                // 0x4F: SRE abs (LSR + EOR, illegale)
                case 0x4F: addr = ReadWord(PC); PC += 2; SRE(addr); break;

                // 0x50: BVC (Branch if Overflow Clear)
                case 0x50: Branch((Status & FLAG_V) == 0); break;

                // 0x57: SRE zp,X (LSR + EOR, illegale)
                case 0x57: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); SRE(addr); break;

                // 0x58: CLI (Clear Interrupt Disable)
                case 0x58: Status &= unchecked((byte)~FLAG_I); break;

                // 0x5F: SRE abs,X (LSR + EOR, illegale)
                case 0x5F: addr = (ushort)(ReadWord(PC) + X); PC += 2; SRE(addr); break;

                // 0x60: RTS (Return from Subroutine)
                case 0x60: PC = RTS(); break;

                // 0x69: ADC imm (Add with Carry, Immediate)
                case 0x69: A = ADC(ReadByte(PC++)); break;

                // 0x6A: ROR A (Rotate Right, Accumulator)
                case 0x6A: A = ROR(A); break;

                // 0x71: ADC ((Indirect),Y) (Add with Carry, Indirect Indexed)
                case 0x71: addr = IndirectIndexed(ReadByte(PC++)); A = ADC(ReadByte(addr)); break;

                // 0x81: STA ((Indirect,X)) (Store Accumulator, Indexed Indirect)
                case 0x81: addr = IndexedIndirect(ReadByte(PC++)); WriteByte(addr, A); break;

                // 0x91: STA ((Indirect),Y) (Store Accumulator, Indirect Indexed)
                case 0x91: addr = IndirectIndexed(ReadByte(PC++)); WriteByte(addr, A); break;

                // 0x8D: STA abs (Store Accumulator, Absolute)
                case 0x8D: addr = ReadWord(PC); PC += 2; WriteByte(addr, A); break;

                // 0x8E: STX abs (Store X Register, Absolute)
                case 0x8E: addr = ReadWord(PC); PC += 2; WriteByte(addr, X); break;

                // 0x8C: STY abs (Store Y Register, Absolute)
                case 0x8C: addr = ReadWord(PC); PC += 2; WriteByte(addr, Y); break;

                // 0xA2: LDX imm (Load X Register, Immediate)
                case 0xA2: X = LDX(ReadByte(PC++)); break;

                // 0xA6: LDX zp (Load X Register, Zero Page)
                case 0xA6: addr = ReadByte(PC++); X = LDX(ReadByte(addr)); break;

                // 0xAE: LDX abs (Load X Register, Absolute)
                case 0xAE: addr = ReadWord(PC); PC += 2; X = LDX(ReadByte(addr)); break;

                // 0xA9: LDA imm (Load Accumulator, Immediate)
                case 0xA9: A = LDA(ReadByte(PC++)); break;

                // 0xC9: CMP imm (Compare Accumulator, Immediate)
                case 0xC9: Compare(A, ReadByte(PC++)); break;

                // 0xE9: SBC imm (Subtract with Carry, Immediate)
                case 0xE9: A = SBC(ReadByte(PC++)); break;

                // 0xEA: NOP (No Operation)
                case 0xEA: break;

                // 0xF0: BEQ (Branch if Equal)
                case 0xF0: Branch((Status & FLAG_Z) != 0); break;

                // 0xF8: SED (Set Decimal Mode)
                case 0xF8: Status |= FLAG_D; break;

                // 0xFF: ISC abs,X (INC + SBC, illegale)
                case 0xFF: addr = (ushort)(ReadWord(PC) + X); PC += 2; ISC(addr); break;

                default:
                    // Istruzione sconosciuta
                    throw new UnknownOpcodeException(opcode, (ushort)(PC - 1));
            }

            if (loggingEnabled)
            {
                LogState(opcode);
            }
        }
        catch (UnknownOpcodeException ex)
        {
            // Log dettagliato per il debug
            Console.WriteLine($"[ERROR] {ex.Message}");
            Console.WriteLine($"CPU State: A={A:X2}, X={X:X2}, Y={Y:X2}, SP={SP:X2}, PC={PC:X4}, Status={Status:X2}");
            throw; // Rilancia l'eccezione per ulteriori gestioni
        }
    }

}