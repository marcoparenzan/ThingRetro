namespace Chips;

public partial class MOS6510
{
    long cycleCount = 0; // Contatore totale dei cicli

    public void Clock()
    {
        // Gestione degli interrupt
        if (irqPending)
        {
            HandleIRQ();
            irqPending = false; // Resetta il flag IRQ
            cycleCount += 7; // Interrupt handling cycles
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
        bool branchTaken;

        try
        {
            switch (opcode)
            {
                // 0x00: BRK (Force Interrupt)
                case 0x00: BRK(); cycleCount += 7; break;

                // 0x01: ORA (Indirect,X) (Logical Inclusive OR)
                case 0x01: addr = IndexedIndirect(ReadByte(PC++)); A = ORA(ReadByte(addr)); cycleCount += 6; break;

                // 0x02: KIL/JAM (Illegal - Stops the processor)
                case 0x02: /* Processor Jam */ cycleCount += 2; break;

                // 0x03: SLO (Indirect,X) (ASL + ORA, illegal)
                case 0x03: addr = IndexedIndirect(ReadByte(PC++)); SLO(addr); cycleCount += 8; break;

                // 0x04: NOP zp (Illegal - No Operation)
                case 0x04: PC++; cycleCount += 3; break;

                // 0x05: ORA zp (Logical Inclusive OR)
                case 0x05: addr = ReadByte(PC++); A = ORA(ReadByte(addr)); cycleCount += 3; break;

                // 0x06: ASL zp (Arithmetic Shift Left)
                case 0x06: addr = ReadByte(PC++); val = ReadByte(addr); WriteByte(addr, ASL(val)); cycleCount += 5; break;

                // 0x07: SLO zp (ASL + ORA, illegal)
                case 0x07: addr = ReadByte(PC++); SLO(addr); cycleCount += 5; break;

                // 0x08: PHP (Push Processor Status)
                case 0x08: Push((byte)(Status | FLAG_B | FLAG_U)); cycleCount += 3; break;

                // 0x09: ORA imm (Logical Inclusive OR, Immediate)
                case 0x09: A = ORA(ReadByte(PC++)); cycleCount += 2; break;

                // 0x0A: ASL A (Arithmetic Shift Left, Accumulator)
                case 0x0A: A = ASL(A); cycleCount += 2; break;

                // 0x0B: ANC imm (AND + set C as bit 7, illegal)
                case 0x0B: A = AND(ReadByte(PC++)); if ((A & 0x80) != 0) Status |= FLAG_C; else Status &= unchecked((byte)~FLAG_C); cycleCount += 2; break;

                // 0x0C: NOP abs (Illegal - No Operation)
                case 0x0C: PC += 2; cycleCount += 4; break;

                // 0x0D: ORA abs (Logical Inclusive OR)
                case 0x0D: addr = ReadWord(PC); PC += 2; A = ORA(ReadByte(addr)); cycleCount += 4; break;

                // 0x0E: ASL abs (Arithmetic Shift Left)
                case 0x0E: addr = ReadWord(PC); PC += 2; val = ReadByte(addr); WriteByte(addr, ASL(val)); cycleCount += 6; break;

                // 0x0F: SLO abs (ASL + ORA, illegal)
                case 0x0F: addr = ReadWord(PC); PC += 2; SLO(addr); cycleCount += 6; break;

                // 0x10: BPL (Branch if Positive)
                case 0x10: 
                    branchTaken = (Status & FLAG_N) == 0;
                    Branch(branchTaken); 
                    cycleCount += branchTaken ? 3 : 2; // +1 cycle if branch taken
                    break;

                // 0x11: ORA (Indirect),Y (Logical Inclusive OR)
                case 0x11: addr = IndirectIndexed(ReadByte(PC++)); A = ORA(ReadByte(addr)); cycleCount += 5; break;

                // 0x12: KIL/JAM (Illegal - Stops the processor)
                case 0x12: /* Processor Jam */ cycleCount += 2; break;

                // 0x13: SLO (Indirect),Y (ASL + ORA, illegal)
                case 0x13: addr = IndirectIndexed(ReadByte(PC++)); SLO(addr); cycleCount += 8; break;

                // 0x14: NOP zp,X (Illegal - No Operation)
                case 0x14: PC++; cycleCount += 4; break;

                // 0x15: ORA zp,X (Logical Inclusive OR)
                case 0x15: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); A = ORA(ReadByte(addr)); cycleCount += 4; break;

                // 0x16: ASL zp,X (Arithmetic Shift Left)
                case 0x16: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); val = ReadByte(addr); WriteByte(addr, ASL(val)); cycleCount += 6; break;

                // 0x17: SLO zp,X (ASL + ORA, illegal)
                case 0x17: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); SLO(addr); cycleCount += 6; break;

                // 0x18: CLC (Clear Carry Flag)
                case 0x18: Status &= unchecked((byte)~FLAG_C); cycleCount += 2; break;

                // 0x19: ORA abs,Y (Logical Inclusive OR)
                case 0x19: addr = (ushort)(ReadWord(PC) + Y); PC += 2; A = ORA(ReadByte(addr)); cycleCount += 4; break;

                // 0x1A: NOP (Illegal - No Operation)
                case 0x1A: cycleCount += 2; break;

                // 0x1B: SLO abs,Y (ASL + ORA, illegal)
                case 0x1B: addr = (ushort)(ReadWord(PC) + Y); PC += 2; SLO(addr); cycleCount += 7; break;

                // 0x1C: NOP abs,X (Illegal - No Operation)
                case 0x1C: PC += 2; cycleCount += 4; break;

                // 0x1D: ORA abs,X (Logical Inclusive OR)
                case 0x1D: addr = (ushort)(ReadWord(PC) + X); PC += 2; A = ORA(ReadByte(addr)); cycleCount += 4; break;

                // 0x1E: ASL abs,X (Arithmetic Shift Left)
                case 0x1E: addr = (ushort)(ReadWord(PC) + X); PC += 2; val = ReadByte(addr); WriteByte(addr, ASL(val)); cycleCount += 7; break;

                // 0x1F: SLO abs,X (ASL + ORA, illegal)
                case 0x1F: addr = (ushort)(ReadWord(PC) + X); PC += 2; SLO(addr); cycleCount += 7; break;

                // 0x20: JSR abs (Jump to Subroutine)
                case 0x20: JSR(ReadWord(PC)); cycleCount += 6; break;

                // 0x21: AND (Indirect,X) (Logical AND)
                case 0x21: addr = IndexedIndirect(ReadByte(PC++)); A = AND(ReadByte(addr)); cycleCount += 6; break;

                // 0x22: KIL/JAM (Illegal - Stops the processor)
                case 0x22: /* Processor Jam */ cycleCount += 2; break;

                // 0x23: RLA (Indirect,X) (ROL + AND, illegal)
                case 0x23: addr = IndexedIndirect(ReadByte(PC++)); RLA(addr); cycleCount += 8; break;

                // 0x24: BIT zp (Test Bits in Memory, Zero Page)
                case 0x24: addr = ReadByte(PC++); BIT(ReadByte(addr)); cycleCount += 3; break;

                // 0x25: AND zp (Logical AND)
                case 0x25: addr = ReadByte(PC++); A = AND(ReadByte(addr)); cycleCount += 3; break;

                // 0x26: ROL zp (Rotate Left)
                case 0x26: addr = ReadByte(PC++); val = ReadByte(addr); WriteByte(addr, ROL(val)); cycleCount += 5; break;

                // 0x27: RLA zp (ROL + AND, illegal)
                case 0x27: addr = ReadByte(PC++); RLA(addr); cycleCount += 5; break;

                // 0x28: PLP (Pull Processor Status)
                case 0x28: Status = (byte)((Pop() & ~FLAG_B) | FLAG_U); cycleCount += 4; break;

                // 0x29: AND imm (Logical AND, Immediate)
                case 0x29: A = AND(ReadByte(PC++)); cycleCount += 2; break;

                // 0x2A: ROL A (Rotate Left, Accumulator)
                case 0x2A: A = ROL(A); cycleCount += 2; break;

                // 0x2B: ANC imm (AND + set C as bit 7, illegal)
                case 0x2B: A = AND(ReadByte(PC++)); if ((A & 0x80) != 0) Status |= FLAG_C; else Status &= unchecked((byte)~FLAG_C); cycleCount += 2; break;

                // 0x2C: BIT abs (Test Bits in Memory, Absolute)
                case 0x2C: addr = ReadWord(PC); PC += 2; BIT(ReadByte(addr)); cycleCount += 4; break;

                // 0x2D: AND abs (Logical AND)
                case 0x2D: addr = ReadWord(PC); PC += 2; A = AND(ReadByte(addr)); cycleCount += 4; break;

                // 0x2E: ROL abs (Rotate Left)
                case 0x2E: addr = ReadWord(PC); PC += 2; val = ReadByte(addr); WriteByte(addr, ROL(val)); cycleCount += 6; break;

                // 0x2F: RLA abs (ROL + AND, illegal)
                case 0x2F: addr = ReadWord(PC); PC += 2; RLA(addr); cycleCount += 6; break;

                // 0x30: BMI (Branch if Negative)
                case 0x30: 
                    branchTaken = (Status & FLAG_N) != 0;
                    Branch(branchTaken);
                    cycleCount += branchTaken ? 3 : 2; // +1 cycle if branch taken
                    break;

                // 0x31: AND (Indirect),Y (Logical AND)
                case 0x31: addr = IndirectIndexed(ReadByte(PC++)); A = AND(ReadByte(addr)); cycleCount += 5; break;

                // 0x32: KIL/JAM (Illegal - Stops the processor)
                case 0x32: /* Processor Jam */ cycleCount += 2; break;

                // 0x33: RLA (Indirect),Y (ROL + AND, illegal)
                case 0x33: addr = IndirectIndexed(ReadByte(PC++)); RLA(addr); cycleCount += 8; break;

                // 0x34: NOP zp,X (Illegal - No Operation)
                case 0x34: PC++; cycleCount += 4; break;

                // 0x35: AND zp,X (Logical AND)
                case 0x35: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); A = AND(ReadByte(addr)); cycleCount += 4; break;

                // 0x36: ROL zp,X (Rotate Left)
                case 0x36: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); val = ReadByte(addr); WriteByte(addr, ROL(val)); cycleCount += 6; break;

                // 0x37: RLA zp,X (ROL + AND, illegal)
                case 0x37: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); RLA(addr); cycleCount += 6; break;

                // 0x38: SEC (Set Carry Flag)
                case 0x38: Status |= FLAG_C; cycleCount += 2; break;

                // 0x39: AND abs,Y (Logical AND)
                case 0x39: addr = (ushort)(ReadWord(PC) + Y); PC += 2; A = AND(ReadByte(addr)); cycleCount += 4; break;

                // 0x3A: NOP (Illegal - No Operation)
                case 0x3A: cycleCount += 2; break;

                // 0x3B: RLA abs,Y (ROL + AND, illegal)
                case 0x3B: addr = (ushort)(ReadWord(PC) + Y); PC += 2; RLA(addr); cycleCount += 7; break;

                // 0x3C: NOP abs,X (Illegal - No Operation)
                case 0x3C: PC += 2; cycleCount += 4; break;

                // 0x3D: AND abs,X (Logical AND)
                case 0x3D: addr = (ushort)(ReadWord(PC) + X); PC += 2; A = AND(ReadByte(addr)); cycleCount += 4; break;

                // 0x3E: ROL abs,X (Rotate Left)
                case 0x3E: addr = (ushort)(ReadWord(PC) + X); PC += 2; val = ReadByte(addr); WriteByte(addr, ROL(val)); cycleCount += 7; break;

                // 0x3F: RLA abs,X (ROL + AND, illegal)
                case 0x3F: addr = (ushort)(ReadWord(PC) + X); PC += 2; RLA(addr); cycleCount += 7; break;

                // 0x40: RTI (Return from Interrupt)
                case 0x40: RTI(); cycleCount += 6; break;

                // 0x41: EOR (Indirect,X) (Exclusive OR)
                case 0x41: addr = IndexedIndirect(ReadByte(PC++)); A = EOR(ReadByte(addr)); cycleCount += 6; break;

                // 0x42: KIL/JAM (Illegal - Stops the processor)
                case 0x42: /* Processor Jam */ cycleCount += 2; break;

                // 0x43: SRE (Indirect,X) (LSR + EOR, illegal)
                case 0x43: addr = IndexedIndirect(ReadByte(PC++)); SRE(addr); cycleCount += 8; break;

                // 0x44: NOP zp (Illegal - No Operation)
                case 0x44: PC++; cycleCount += 3; break;

                // 0x45: EOR zp (Exclusive OR)
                case 0x45: addr = ReadByte(PC++); A = EOR(ReadByte(addr)); cycleCount += 3; break;

                // 0x46: LSR zp (Logical Shift Right)
                case 0x46: addr = ReadByte(PC++); val = ReadByte(addr); WriteByte(addr, LSR(val)); cycleCount += 5; break;

                // 0x47: SRE zp (LSR + EOR, illegal)
                case 0x47: addr = ReadByte(PC++); SRE(addr); cycleCount += 5; break;

                // 0x48: PHA (Push Accumulator)
                case 0x48: Push(A); cycleCount += 3; break;

                // 0x49: EOR imm (Exclusive OR, Immediate)
                case 0x49: A = EOR(ReadByte(PC++)); cycleCount += 2; break;

                // 0x4A: LSR A (Logical Shift Right, Accumulator)
                case 0x4A: A = LSR(A); cycleCount += 2; break;

                // 0x4B: ALR imm (AND + LSR, illegal)
                case 0x4B: A = AND(ReadByte(PC++)); A = LSR(A); cycleCount += 2; break;

                // 0x4C: JMP abs (Jump Absolute)
                case 0x4C: PC = ReadWord(PC); cycleCount += 3; break;

                // 0x4D: EOR abs (Exclusive OR)
                case 0x4D: addr = ReadWord(PC); PC += 2; A = EOR(ReadByte(addr)); cycleCount += 4; break;

                // 0x4E: LSR abs (Logical Shift Right)
                case 0x4E: addr = ReadWord(PC); PC += 2; val = ReadByte(addr); WriteByte(addr, LSR(val)); cycleCount += 6; break;

                // 0x4F: SRE abs (LSR + EOR, illegal)
                case 0x4F: addr = ReadWord(PC); PC += 2; SRE(addr); cycleCount += 6; break;

                // 0x50: BVC (Branch if Overflow Clear)
                case 0x50: 
                    branchTaken = (Status & FLAG_V) == 0;
                    Branch(branchTaken);
                    cycleCount += branchTaken ? 3 : 2; // +1 cycle if branch taken
                    break;

                // 0x51: EOR (Indirect),Y (Exclusive OR)
                case 0x51: addr = IndirectIndexed(ReadByte(PC++)); A = EOR(ReadByte(addr)); cycleCount += 5; break;

                // 0x52: KIL/JAM (Illegal - Stops the processor)
                case 0x52: /* Processor Jam */ cycleCount += 2; break;

                // 0x53: SRE (Indirect),Y (LSR + EOR, illegal)
                case 0x53: addr = IndirectIndexed(ReadByte(PC++)); SRE(addr); cycleCount += 8; break;

                // 0x54: NOP zp,X (Illegal - No Operation)
                case 0x54: PC++; cycleCount += 4; break;

                // 0x55: EOR zp,X (Exclusive OR)
                case 0x55: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); A = EOR(ReadByte(addr)); cycleCount += 4; break;

                // 0x56: LSR zp,X (Logical Shift Right)
                case 0x56: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); val = ReadByte(addr); WriteByte(addr, LSR(val)); cycleCount += 6; break;

                // 0x57: SRE zp,X (LSR + EOR, illegal)
                case 0x57: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); SRE(addr); cycleCount += 6; break;

                // 0x58: CLI (Clear Interrupt Disable)
                case 0x58: Status &= unchecked((byte)~FLAG_I); cycleCount += 2; break;

                // 0x59: EOR abs,Y (Exclusive OR)
                case 0x59: addr = (ushort)(ReadWord(PC) + Y); PC += 2; A = EOR(ReadByte(addr)); cycleCount += 4; break;

                // 0x5A: NOP (Illegal - No Operation)
                case 0x5A: cycleCount += 2; break;

                // 0x5B: SRE abs,Y (LSR + EOR, illegal)
                case 0x5B: addr = (ushort)(ReadWord(PC) + Y); PC += 2; SRE(addr); cycleCount += 7; break;

                // 0x5C: NOP abs,X (Illegal - No Operation)
                case 0x5C: PC += 2; cycleCount += 4; break;

                // 0x5D: EOR abs,X (Exclusive OR)
                case 0x5D: addr = (ushort)(ReadWord(PC) + X); PC += 2; A = EOR(ReadByte(addr)); cycleCount += 4; break;

                // 0x5E: LSR abs,X (Logical Shift Right)
                case 0x5E: addr = (ushort)(ReadWord(PC) + X); PC += 2; val = ReadByte(addr); WriteByte(addr, LSR(val)); cycleCount += 7; break;

                // 0x5F: SRE abs,X (LSR + EOR, illegal)
                case 0x5F: addr = (ushort)(ReadWord(PC) + X); PC += 2; SRE(addr); cycleCount += 7; break;

                // 0x60: RTS (Return from Subroutine)
                case 0x60: PC = RTS(); cycleCount += 6; break;

                // 0x61: ADC (Indirect,X) (Add with Carry)
                case 0x61: addr = IndexedIndirect(ReadByte(PC++)); A = ADC(ReadByte(addr)); cycleCount += 6; break;

                // 0x62: KIL/JAM (Illegal - Stops the processor)
                case 0x62: /* Processor Jam */ cycleCount += 2; break;

                // 0x63: RRA (Indirect,X) (ROR + ADC, illegal)
                case 0x63: addr = IndexedIndirect(ReadByte(PC++)); RRA(addr); cycleCount += 8; break;

                // 0x64: NOP zp (Illegal - No Operation)
                case 0x64: PC++; cycleCount += 3; break;

                // 0x65: ADC zp (Add with Carry)
                case 0x65: addr = ReadByte(PC++); A = ADC(ReadByte(addr)); cycleCount += 3; break;

                // 0x66: ROR zp (Rotate Right)
                case 0x66: addr = ReadByte(PC++); val = ReadByte(addr); WriteByte(addr, ROR(val)); cycleCount += 5; break;

                // 0x67: RRA zp (ROR + ADC, illegal)
                case 0x67: addr = ReadByte(PC++); RRA(addr); cycleCount += 5; break;

                // 0x68: PLA (Pull Accumulator)
                case 0x68: A = Pop(); SetZN(A); cycleCount += 4; break;

                // 0x69: ADC imm (Add with Carry, Immediate)
                case 0x69: A = ADC(ReadByte(PC++)); cycleCount += 2; break;

                // 0x6A: ROR A (Rotate Right, Accumulator)
                case 0x6A: A = ROR(A); cycleCount += 2; break;

                // 0x6B: ARR imm (AND + ROR, illegal)
                case 0x6B: A = AND(ReadByte(PC++)); A = ROR(A); cycleCount += 2; break;

                // 0x6C: JMP (ind) (Jump Indirect)
                case 0x6C: addr = ReadWord(PC); PC = ReadWord(addr); cycleCount += 5; break;

                // 0x6D: ADC abs (Add with Carry)
                case 0x6D: addr = ReadWord(PC); PC += 2; A = ADC(ReadByte(addr)); cycleCount += 4; break;

                // 0x6E: ROR abs (Rotate Right)
                case 0x6E: addr = ReadWord(PC); PC += 2; val = ReadByte(addr); WriteByte(addr, ROR(val)); cycleCount += 6; break;

                // 0x6F: RRA abs (ROR + ADC, illegal)
                case 0x6F: addr = ReadWord(PC); PC += 2; RRA(addr); cycleCount += 6; break;

                // 0x70: BVS (Branch if Overflow Set)
                case 0x70: 
                    branchTaken = (Status & FLAG_V) != 0;
                    Branch(branchTaken);
                    cycleCount += branchTaken ? 3 : 2; // +1 cycle if branch taken
                    break;

                // 0x71: ADC (Indirect),Y (Add with Carry)
                case 0x71: addr = IndirectIndexed(ReadByte(PC++)); A = ADC(ReadByte(addr)); cycleCount += 5; break;

                // 0x72: KIL/JAM (Illegal - Stops the processor)
                case 0x72: /* Processor Jam */ cycleCount += 2; break;

                // 0x73: RRA (Indirect),Y (ROR + ADC, illegal)
                case 0x73: addr = IndirectIndexed(ReadByte(PC++)); RRA(addr); cycleCount += 8; break;

                // 0x74: NOP zp,X (Illegal - No Operation)
                case 0x74: PC++; cycleCount += 4; break;

                // 0x75: ADC zp,X (Add with Carry)
                case 0x75: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); A = ADC(ReadByte(addr)); cycleCount += 4; break;

                // 0x76: ROR zp,X (Rotate Right)
                case 0x76: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); val = ReadByte(addr); WriteByte(addr, ROR(val)); cycleCount += 6; break;

                // 0x77: RRA zp,X (ROR + ADC, illegal)
                case 0x77: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); RRA(addr); cycleCount += 6; break;

                // 0x78: SEI (Set Interrupt Disable)
                case 0x78: Status |= FLAG_I; cycleCount += 2; break;

                // 0x79: ADC abs,Y (Add with Carry)
                case 0x79: addr = (ushort)(ReadWord(PC) + Y); PC += 2; A = ADC(ReadByte(addr)); cycleCount += 4; break;

                // 0x7A: NOP (Illegal - No Operation)
                case 0x7A: cycleCount += 2; break;

                // 0x7B: RRA abs,Y (ROR + ADC, illegal)
                case 0x7B: addr = (ushort)(ReadWord(PC) + Y); PC += 2; RRA(addr); cycleCount += 7; break;

                // 0x7C: NOP abs,X (Illegal - No Operation)
                case 0x7C: PC += 2; cycleCount += 4; break;

                // 0x7D: ADC abs,X (Add with Carry)
                case 0x7D: addr = (ushort)(ReadWord(PC) + X); PC += 2; A = ADC(ReadByte(addr)); cycleCount += 4; break;

                // 0x7E: ROR abs,X (Rotate Right)
                case 0x7E: addr = (ushort)(ReadWord(PC) + X); PC += 2; val = ReadByte(addr); WriteByte(addr, ROR(val)); cycleCount += 7; break;

                // 0x7F: RRA abs,X (ROR + ADC, illegal)
                case 0x7F: addr = (ushort)(ReadWord(PC) + X); PC += 2; RRA(addr); cycleCount += 7; break;

                // 0x80: NOP imm (Illegal - No Operation)
                case 0x80: PC++; cycleCount += 2; break;

                // 0x81: STA (Indirect,X) (Store Accumulator)
                case 0x81: addr = IndexedIndirect(ReadByte(PC++)); WriteByte(addr, A); cycleCount += 6; break;

                // 0x82: NOP imm (Illegal - No Operation)
                case 0x82: PC++; cycleCount += 2; break;

                // 0x83: SAX (Indirect,X) (Store A & X, illegal)
                case 0x83: addr = IndexedIndirect(ReadByte(PC++)); WriteByte(addr, (byte)(A & X)); cycleCount += 6; break;

                // 0x84: STY zp (Store Y Register)
                case 0x84: addr = ReadByte(PC++); WriteByte(addr, Y); cycleCount += 3; break;

                // 0x85: STA zp (Store Accumulator)
                case 0x85: addr = ReadByte(PC++); WriteByte(addr, A); cycleCount += 3; break;

                // 0x86: STX zp (Store X Register)
                case 0x86: addr = ReadByte(PC++); WriteByte(addr, X); cycleCount += 3; break;

                // 0x87: SAX zp (Store A & X, illegal)
                case 0x87: addr = ReadByte(PC++); WriteByte(addr, (byte)(A & X)); cycleCount += 3; break;

                // 0x88: DEY (Decrement Y Register)
                case 0x88: Y--; SetZN(Y); cycleCount += 2; break;

                // 0x89: NOP imm (Illegal - No Operation)
                case 0x89: PC++; cycleCount += 2; break;

                // 0x8A: TXA (Transfer X to Accumulator)
                case 0x8A: A = X; SetZN(A); cycleCount += 2; break;

                // 0x8B: XAA imm (TXA + AND, illegal/unstable)
                case 0x8B: A = (byte)(X & ReadByte(PC++)); SetZN(A); cycleCount += 2; break;

                // 0x8C: STY abs (Store Y Register, Absolute)
                case 0x8C: addr = ReadWord(PC); PC += 2; WriteByte(addr, Y); cycleCount += 4; break;

                // 0x8D: STA abs (Store Accumulator, Absolute)
                case 0x8D: addr = ReadWord(PC); PC += 2; WriteByte(addr, A); cycleCount += 4; break;

                // 0x8E: STX abs (Store X Register, Absolute)
                case 0x8E: addr = ReadWord(PC); PC += 2; WriteByte(addr, X); cycleCount += 4; break;

                // 0x8F: SAX abs (Store A & X, illegal)
                case 0x8F: addr = ReadWord(PC); PC += 2; WriteByte(addr, (byte)(A & X)); cycleCount += 4; break;

                // 0x90: BCC (Branch if Carry Clear)
                case 0x90: 
                    branchTaken = (Status & FLAG_C) == 0;
                    Branch(branchTaken);
                    cycleCount += branchTaken ? 3 : 2; // +1 cycle if branch taken
                    break;

                // 0x91: STA (Indirect),Y (Store Accumulator)
                case 0x91: addr = IndirectIndexed(ReadByte(PC++)); WriteByte(addr, A); cycleCount += 6; break;

                // 0x92: KIL/JAM (Illegal - Stops the processor)
                case 0x92: /* Processor Jam */ cycleCount += 2; break;

                // 0x93: AHX (Indirect),Y (Store A & X & H, illegal/unstable)
                case 0x93: addr = IndirectIndexed(ReadByte(PC++)); WriteByte(addr, (byte)(A & X & ((addr >> 8) + 1))); cycleCount += 6; break;

                // 0x94: STY zp,X (Store Y Register)
                case 0x94: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); WriteByte(addr, Y); cycleCount += 4; break;

                // 0x95: STA zp,X (Store Accumulator)
                case 0x95: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); WriteByte(addr, A); cycleCount += 4; break;

                // 0x96: STX zp,Y (Store X Register)
                case 0x96: addr = (ushort)((ReadByte(PC++) + Y) & 0xFF); WriteByte(addr, X); cycleCount += 4; break;

                // 0x97: SAX zp,Y (Store A & X, illegal)
                case 0x97: addr = (ushort)((ReadByte(PC++) + Y) & 0xFF); WriteByte(addr, (byte)(A & X)); cycleCount += 4; break;

                // 0x98: TYA (Transfer Y to Accumulator)
                case 0x98: A = Y; SetZN(A); cycleCount += 2; break;

                // 0x99: STA abs,Y (Store Accumulator)
                case 0x99: addr = (ushort)(ReadWord(PC) + Y); PC += 2; WriteByte(addr, A); cycleCount += 5; break;

                // 0x9A: TXS (Transfer X to Stack Pointer)
                case 0x9A: SP = X; cycleCount += 2; break;

                // 0x9B: TAS abs,Y (TXS + STA, illegal/unstable)
                case 0x9B: addr = (ushort)(ReadWord(PC) + Y); PC += 2; SP = (byte)(A & X); WriteByte(addr, (byte)(SP & ((addr >> 8) + 1))); cycleCount += 5; break;

                // 0x9C: SHY abs,X (Store Y & H, illegal/unstable)
                case 0x9C: addr = (ushort)(ReadWord(PC) + X); PC += 2; WriteByte(addr, (byte)(Y & ((addr >> 8) + 1))); cycleCount += 5; break;

                // 0x9D: STA abs,X (Store Accumulator)
                case 0x9D: addr = (ushort)(ReadWord(PC) + X); PC += 2; WriteByte(addr, A); cycleCount += 5; break;

                // 0x9E: SHX abs,Y (Store X & H, illegal/unstable)
                case 0x9E: addr = (ushort)(ReadWord(PC) + Y); PC += 2; WriteByte(addr, (byte)(X & ((addr >> 8) + 1))); cycleCount += 5; break;

                // 0x9F: AHX abs,Y (Store A & X & H, illegal/unstable)
                case 0x9F: addr = (ushort)(ReadWord(PC) + Y); PC += 2; WriteByte(addr, (byte)(A & X & ((addr >> 8) + 1))); cycleCount += 5; break;

                // 0xA0: LDY imm (Load Y Register, Immediate)
                case 0xA0: Y = LDY(ReadByte(PC++)); cycleCount += 2; break;

                // 0xA1: LDA (Indirect,X) (Load Accumulator)
                case 0xA1: addr = IndexedIndirect(ReadByte(PC++)); A = LDA(ReadByte(addr)); cycleCount += 6; break;

                // 0xA2: LDX imm (Load X Register, Immediate)
                case 0xA2: X = LDX(ReadByte(PC++)); cycleCount += 2; break;

                // 0xA3: LAX (Indirect,X) (LDA + LDX, illegal)
                case 0xA3: addr = IndexedIndirect(ReadByte(PC++)); A = X = LAX(ReadByte(addr)); cycleCount += 6; break;

                // 0xA4: LDY zp (Load Y Register, Zero Page)
                case 0xA4: addr = ReadByte(PC++); Y = LDY(ReadByte(addr)); cycleCount += 3; break;

                // 0xA5: LDA zp (Load Accumulator, Zero Page)
                case 0xA5: addr = ReadByte(PC++); A = LDA(ReadByte(addr)); cycleCount += 3; break;

                // 0xA6: LDX zp (Load X Register, Zero Page)
                case 0xA6: addr = ReadByte(PC++); X = LDX(ReadByte(addr)); cycleCount += 3; break;

                // 0xA7: LAX zp (LDA + LDX, illegal)
                case 0xA7: addr = ReadByte(PC++); A = X = LAX(ReadByte(addr)); cycleCount += 3; break;

                // 0xA8: TAY (Transfer Accumulator to Y)
                case 0xA8: Y = A; SetZN(Y); cycleCount += 2; break;

                // 0xA9: LDA imm (Load Accumulator, Immediate)
                case 0xA9: A = LDA(ReadByte(PC++)); cycleCount += 2; break;

                // 0xAA: TAX (Transfer Accumulator to X)
                case 0xAA: X = A; SetZN(X); cycleCount += 2; break;

                // 0xAB: LAX imm (LDA + LDX, illegal)
                case 0xAB: A = X = LAX(ReadByte(PC++)); cycleCount += 2; break;

                // 0xAC: LDY abs (Load Y Register, Absolute)
                case 0xAC: addr = ReadWord(PC); PC += 2; Y = LDY(ReadByte(addr)); cycleCount += 4; break;

                // 0xAD: LDA abs (Load Accumulator, Absolute)
                case 0xAD: addr = ReadWord(PC); PC += 2; A = LDA(ReadByte(addr)); cycleCount += 4; break;

                // 0xAE: LDX abs (Load X Register, Absolute)
                case 0xAE: addr = ReadWord(PC); PC += 2; X = LDX(ReadByte(addr)); cycleCount += 4; break;

                // 0xAF: LAX abs (LDA + LDX, illegal)
                case 0xAF: addr = ReadWord(PC); PC += 2; A = X = LAX(ReadByte(addr)); cycleCount += 4; break;

                // 0xB0: BCS (Branch if Carry Set)
                case 0xB0: 
                    branchTaken = (Status & FLAG_C) != 0;
                    Branch(branchTaken);
                    cycleCount += branchTaken ? 3 : 2; // +1 cycle if branch taken
                    break;

                // 0xB1: LDA (Indirect),Y (Load Accumulator)
                case 0xB1: addr = IndirectIndexed(ReadByte(PC++)); A = LDA(ReadByte(addr)); cycleCount += 5; break;

                // 0xB2: KIL/JAM (Illegal - Stops the processor)
                case 0xB2: /* Processor Jam */ cycleCount += 2; break;

                // 0xB3: LAX (Indirect),Y (LDA + LDX, illegal)
                case 0xB3: addr = IndirectIndexed(ReadByte(PC++)); A = X = LAX(ReadByte(addr)); cycleCount += 5; break;

                // 0xB4: LDY zp,X (Load Y Register, Zero Page,X)
                case 0xB4: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); Y = LDY(ReadByte(addr)); cycleCount += 4; break;

                // 0xB5: LDA zp,X (Load Accumulator, Zero Page,X)
                case 0xB5: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); A = LDA(ReadByte(addr)); cycleCount += 4; break;

                // 0xB6: LDX zp,Y (Load X Register, Zero Page,Y)
                case 0xB6: addr = (ushort)((ReadByte(PC++) + Y) & 0xFF); X = LDX(ReadByte(addr)); cycleCount += 4; break;

                // 0xB7: LAX zp,Y (LDA + LDX, illegal)
                case 0xB7: addr = (ushort)((ReadByte(PC++) + Y) & 0xFF); A = X = LAX(ReadByte(addr)); cycleCount += 4; break;

                // 0xB8: CLV (Clear Overflow Flag)
                case 0xB8: Status &= unchecked((byte)~FLAG_V); cycleCount += 2; break;

                // 0xB9: LDA abs,Y (Load Accumulator, Absolute,Y)
                case 0xB9: addr = (ushort)(ReadWord(PC) + Y); PC += 2; A = LDA(ReadByte(addr)); cycleCount += 4; break;

                // 0xBA: TSX (Transfer Stack Pointer to X)
                case 0xBA: X = SP; SetZN(X); cycleCount += 2; break;

                // 0xBB: LAS abs,Y (LDA/TSX hybrid, illegal)
                case 0xBB: addr = (ushort)(ReadWord(PC) + Y); PC += 2; A = X = SP = (byte)(ReadByte(addr) & SP); SetZN(A); cycleCount += 4; break;

                // 0xBC: LDY abs,X (Load Y Register, Absolute,X)
                case 0xBC: addr = (ushort)(ReadWord(PC) + X); PC += 2; Y = LDY(ReadByte(addr)); cycleCount += 4; break;

                // 0xBD: LDA abs,X (Load Accumulator, Absolute,X)
                case 0xBD: addr = (ushort)(ReadWord(PC) + X); PC += 2; A = LDA(ReadByte(addr)); cycleCount += 4; break;

                // 0xBE: LDX abs,Y (Load X Register, Absolute,Y)
                case 0xBE: addr = (ushort)(ReadWord(PC) + Y); PC += 2; X = LDX(ReadByte(addr)); cycleCount += 4; break;

                // 0xBF: LAX abs,Y (LDA + LDX, illegal)
                case 0xBF: addr = (ushort)(ReadWord(PC) + Y); PC += 2; A = X = LAX(ReadByte(addr)); cycleCount += 4; break;

                // 0xC0: CPY imm (Compare Y Register, Immediate)
                case 0xC0: Compare(Y, ReadByte(PC++)); cycleCount += 2; break;

                // 0xC1: CMP (Indirect,X) (Compare Accumulator)
                case 0xC1: addr = IndexedIndirect(ReadByte(PC++)); Compare(A, ReadByte(addr)); cycleCount += 6; break;

                // 0xC2: NOP imm (Illegal - No Operation)
                case 0xC2: PC++; cycleCount += 2; break;

                // 0xC3: DCP (Indirect,X) (DEC + CMP, illegal)
                case 0xC3: addr = IndexedIndirect(ReadByte(PC++)); DCP(addr); cycleCount += 8; break;

                // 0xC4: CPY zp (Compare Y Register, Zero Page)
                case 0xC4: addr = ReadByte(PC++); Compare(Y, ReadByte(addr)); cycleCount += 3; break;

                // 0xC5: CMP zp (Compare Accumulator, Zero Page)
                case 0xC5: addr = ReadByte(PC++); Compare(A, ReadByte(addr)); cycleCount += 3; break;

                // 0xC6: DEC zp (Decrement Memory, Zero Page)
                case 0xC6: addr = ReadByte(PC++); val = ReadByte(addr); WriteByte(addr, DEC(val)); cycleCount += 5; break;

                // 0xC7: DCP zp (DEC + CMP, illegal)
                case 0xC7: addr = ReadByte(PC++); DCP(addr); cycleCount += 5; break;

                // 0xC8: INY (Increment Y Register)
                case 0xC8: Y++; SetZN(Y); cycleCount += 2; break;

                // 0xC9: CMP imm (Compare Accumulator, Immediate)
                case 0xC9: Compare(A, ReadByte(PC++)); cycleCount += 2; break;

                // 0xCA: DEX (Decrement X Register)
                case 0xCA: X--; SetZN(X); cycleCount += 2; break;

                // 0xCB: AXS imm (CMP + DEX, illegal)
                case 0xCB: X = (byte)((A & X) - ReadByte(PC++)); if (X <= (A & X)) Status |= FLAG_C; else Status &= unchecked((byte)~FLAG_C); SetZN(X); cycleCount += 2; break;

                // 0xCC: CPY abs (Compare Y Register, Absolute)
                case 0xCC: addr = ReadWord(PC); PC += 2; Compare(Y, ReadByte(addr)); cycleCount += 4; break;

                // 0xCD: CMP abs (Compare Accumulator, Absolute)
                case 0xCD: addr = ReadWord(PC); PC += 2; Compare(A, ReadByte(addr)); cycleCount += 4; break;

                // 0xCE: DEC abs (Decrement Memory, Absolute)
                case 0xCE: addr = ReadWord(PC); PC += 2; val = ReadByte(addr); WriteByte(addr, DEC(val)); cycleCount += 6; break;

                // 0xCF: DCP abs (DEC + CMP, illegal)
                case 0xCF: addr = ReadWord(PC); PC += 2; DCP(addr); cycleCount += 6; break;

                // 0xD0: BNE (Branch if Not Equal)
                case 0xD0: 
                    branchTaken = (Status & FLAG_Z) == 0;
                    Branch(branchTaken);
                    cycleCount += branchTaken ? 3 : 2; // +1 cycle if branch taken
                    break;

                // 0xD1: CMP (Indirect),Y (Compare Accumulator)
                case 0xD1: addr = IndirectIndexed(ReadByte(PC++)); Compare(A, ReadByte(addr)); cycleCount += 5; break;

                // 0xD2: KIL/JAM (Illegal - Stops the processor)
                case 0xD2: /* Processor Jam */ cycleCount += 2; break;

                // 0xD3: DCP (Indirect),Y (DEC + CMP, illegal)
                case 0xD3: addr = IndirectIndexed(ReadByte(PC++)); DCP(addr); cycleCount += 8; break;

                // 0xD4: NOP zp,X (Illegal - No Operation)
                case 0xD4: PC++; cycleCount += 4; break;

                // 0xD5: CMP zp,X (Compare Accumulator, Zero Page,X)
                case 0xD5: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); Compare(A, ReadByte(addr)); cycleCount += 4; break;

                // 0xD6: DEC zp,X (Decrement Memory, Zero Page,X)
                case 0xD6: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); val = ReadByte(addr); WriteByte(addr, DEC(val)); cycleCount += 6; break;

                // 0xD7: DCP zp,X (DEC + CMP, illegal)
                case 0xD7: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); DCP(addr); cycleCount += 6; break;

                // 0xD8: CLD (Clear Decimal Mode)
                case 0xD8: Status &= unchecked((byte)~FLAG_D); cycleCount += 2; break;

                // 0xD9: CMP abs,Y (Compare Accumulator, Absolute,Y)
                case 0xD9: addr = (ushort)(ReadWord(PC) + Y); PC += 2; Compare(A, ReadByte(addr)); cycleCount += 4; break;

                // 0xDA: NOP (Illegal - No Operation)
                case 0xDA: cycleCount += 2; break;

                // 0xDB: DCP abs,Y (DEC + CMP, illegal)
                case 0xDB: addr = (ushort)(ReadWord(PC) + Y); PC += 2; DCP(addr); cycleCount += 7; break;

                // 0xDC: NOP abs,X (Illegal - No Operation)
                case 0xDC: PC += 2; cycleCount += 4; break;

                // 0xDD: CMP abs,X (Compare Accumulator, Absolute,X)
                case 0xDD: addr = (ushort)(ReadWord(PC) + X); PC += 2; Compare(A, ReadByte(addr)); cycleCount += 4; break;

                // 0xDE: DEC abs,X (Decrement Memory, Absolute,X)
                case 0xDE: addr = (ushort)(ReadWord(PC) + X); PC += 2; val = ReadByte(addr); WriteByte(addr, DEC(val)); cycleCount += 7; break;

                // 0xDF: DCP abs,X (DEC + CMP, illegal)
                case 0xDF: addr = (ushort)(ReadWord(PC) + X); PC += 2; DCP(addr); cycleCount += 7; break;

                // 0xE0: CPX imm (Compare X Register, Immediate)
                case 0xE0: Compare(X, ReadByte(PC++)); cycleCount += 2; break;

                // 0xE1: SBC (Indirect,X) (Subtract with Carry)
                case 0xE1: addr = IndexedIndirect(ReadByte(PC++)); A = SBC(ReadByte(addr)); cycleCount += 6; break;

                // 0xE2: NOP imm (Illegal - No Operation)
                case 0xE2: PC++; cycleCount += 2; break;

                // 0xE3: ISC (Indirect,X) (INC + SBC, illegal)
                case 0xE3: addr = IndexedIndirect(ReadByte(PC++)); ISC(addr); cycleCount += 8; break;

                // 0xE4: CPX zp (Compare X Register, Zero Page)
                case 0xE4: addr = ReadByte(PC++); Compare(X, ReadByte(addr)); cycleCount += 3; break;

                // 0xE5: SBC zp (Subtract with Carry)
                case 0xE5: addr = ReadByte(PC++); A = SBC(ReadByte(addr)); cycleCount += 3; break;

                // 0xE6: INC zp (Increment Memory, Zero Page)
                case 0xE6: addr = ReadByte(PC++); val = ReadByte(addr); WriteByte(addr, INC(val)); cycleCount += 5; break;

                // 0xE7: ISC zp (INC + SBC, illegal)
                case 0xE7: addr = ReadByte(PC++); ISC(addr); cycleCount += 5; break;

                // 0xE8: INX (Increment X Register)
                case 0xE8: X++; SetZN(X); cycleCount += 2; break;

                // 0xE9: SBC imm (Subtract with Carry, Immediate)
                case 0xE9: A = SBC(ReadByte(PC++)); cycleCount += 2; break;

                // 0xEA: NOP (No Operation)
                case 0xEA: cycleCount += 2; break;

                // 0xEB: SBC imm (Subtract with Carry, Immediate) - Illegal duplicate
                case 0xEB: A = SBC(ReadByte(PC++)); cycleCount += 2; break;

                // 0xEC: CPX abs (Compare X Register, Absolute)
                case 0xEC: addr = ReadWord(PC); PC += 2; Compare(X, ReadByte(addr)); cycleCount += 4; break;

                // 0xED: SBC abs (Subtract with Carry)
                case 0xED: addr = ReadWord(PC); PC += 2; A = SBC(ReadByte(addr)); cycleCount += 4; break;

                // 0xEE: INC abs (Increment Memory, Absolute)
                case 0xEE: addr = ReadWord(PC); PC += 2; val = ReadByte(addr); WriteByte(addr, INC(val)); cycleCount += 6; break;

                // 0xEF: ISC abs (INC + SBC, illegal)
                case 0xEF: addr = ReadWord(PC); PC += 2; ISC(addr); cycleCount += 6; break;

                // 0xF0: BEQ (Branch if Equal)
                case 0xF0: 
                    branchTaken = (Status & FLAG_Z) != 0;
                    Branch(branchTaken);
                    cycleCount += branchTaken ? 3 : 2; // +1 cycle if branch taken
                    break;

                // 0xF1: SBC (Indirect),Y (Subtract with Carry)
                case 0xF1: addr = IndirectIndexed(ReadByte(PC++)); A = SBC(ReadByte(addr)); cycleCount += 5; break;

                // 0xF2: KIL/JAM (Illegal - Stops the processor)
                case 0xF2: /* Processor Jam */ cycleCount += 2; break;

                // 0xF3: ISC (Indirect),Y (INC + SBC, illegal)
                case 0xF3: addr = IndirectIndexed(ReadByte(PC++)); ISC(addr); cycleCount += 8; break;

                // 0xF4: NOP zp,X (Illegal - No Operation)
                case 0xF4: PC++; cycleCount += 4; break;

                // 0xF5: SBC zp,X (Subtract with Carry)
                case 0xF5: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); A = SBC(ReadByte(addr)); cycleCount += 4; break;

                // 0xF6: INC zp,X (Increment Memory, Zero Page,X)
                case 0xF6: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); val = ReadByte(addr); WriteByte(addr, INC(val)); cycleCount += 6; break;

                // 0xF7: ISC zp,X (INC + SBC, illegal)
                case 0xF7: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); ISC(addr); cycleCount += 6; break;

                // 0xF8: SED (Set Decimal Mode)
                case 0xF8: Status |= FLAG_D; cycleCount += 2; break;

                // 0xF9: SBC abs,Y (Subtract with Carry)
                case 0xF9: addr = (ushort)(ReadWord(PC) + Y); PC += 2; A = SBC(ReadByte(addr)); cycleCount += 4; break;

                // 0xFA: NOP (Illegal - No Operation)
                case 0xFA: cycleCount += 2; break;

                // 0xFB: ISC abs,Y (INC + SBC, illegal)
                case 0xFB: addr = (ushort)(ReadWord(PC) + Y); PC += 2; ISC(addr); cycleCount += 7; break;

                // 0xFC: NOP abs,X (Illegal - No Operation)
                case 0xFC: PC += 2; cycleCount += 4; break;

                // 0xFD: SBC abs,X (Subtract with Carry)
                case 0xFD: addr = (ushort)(ReadWord(PC) + X); PC += 2; A = SBC(ReadByte(addr)); cycleCount += 4; break;

                // 0xFE: INC abs,X (Increment Memory, Absolute,X)
                case 0xFE: addr = (ushort)(ReadWord(PC) + X); PC += 2; val = ReadByte(addr); WriteByte(addr, INC(val)); cycleCount += 7; break;

                // 0xFF: ISC abs,X (INC + SBC, illegal)
                case 0xFF: addr = (ushort)(ReadWord(PC) + X); PC += 2; ISC(addr); cycleCount += 7; break;

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