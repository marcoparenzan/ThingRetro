namespace Fake64;

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
        if (loggingEnabled)
        {
            //LogState(opcode);
        }

        ushort addr;
        byte val;
        bool branchTaken;

        try
        {
            switch (opcode)
            {
                // *** IMPLICITO / ACCUMULATORE ***
                // Istruzioni che non richiedono operandi o usano l'accumulatore
                case 0x0A: A = ASL(A); cycleCount += 2; break;                    // ASL A
                case 0x18: Status &= unchecked((byte)~FLAG_C); cycleCount += 2; break; // CLC
                case 0x1A: cycleCount += 2; break;                                // NOP (Illegal)
                case 0x2A: A = ROL(A); cycleCount += 2; break;                    // ROL A
                case 0x38: Status |= FLAG_C; cycleCount += 2; break;              // SEC
                case 0x3A: cycleCount += 2; break;                                // NOP (Illegal)
                case 0x4A: A = LSR(A); cycleCount += 2; break;                    // LSR A
                case 0x58: Status &= unchecked((byte)~FLAG_I); cycleCount += 2; break; // CLI
                case 0x5A: cycleCount += 2; break;                                // NOP (Illegal)
                case 0x6A: A = ROR(A); cycleCount += 2; break;                    // ROR A
                case 0x78: Status |= FLAG_I; cycleCount += 2; break;              // SEI
                case 0x7A: cycleCount += 2; break;                                // NOP (Illegal)
                case 0x88: Y--; SetZN(Y); cycleCount += 2; break;                 // DEY
                case 0x8A: A = X; SetZN(A); cycleCount += 2; break;               // TXA
                case 0x98: A = Y; SetZN(A); cycleCount += 2; break;               // TYA
                case 0x9A: SP = X; cycleCount += 2; break;                        // TXS
                case 0xA8: Y = A; SetZN(Y); cycleCount += 2; break;               // TAY
                case 0xAA: X = A; SetZN(X); cycleCount += 2; break;               // TAX
                case 0xB8: Status &= unchecked((byte)~FLAG_V); cycleCount += 2; break; // CLV
                case 0xBA: X = SP; SetZN(X); cycleCount += 2; break;              // TSX
                case 0xC8: Y++; SetZN(Y); cycleCount += 2; break;                 // INY
                case 0xCA: X--; SetZN(X); cycleCount += 2; break;                 // DEX
                case 0xD8: Status &= unchecked((byte)~FLAG_D); cycleCount += 2; break; // CLD
                case 0xDA: cycleCount += 2; break;                                // NOP (Illegal)
                case 0xE8: X++; SetZN(X); cycleCount += 2; break;                 // INX
                case 0xEA: cycleCount += 2; break;                                // NOP
                case 0xF8: Status |= FLAG_D; cycleCount += 2; break;              // SED
                case 0xFA: cycleCount += 2; break;                                // NOP (Illegal)

                // *** STACK OPERATIONS ***
                case 0x00: BRK(); cycleCount += 7; break;                         // BRK
                case 0x08: Push((byte)(Status | FLAG_B | FLAG_U)); cycleCount += 3; break; // PHP
                case 0x28: Status = (byte)((Pop() & ~FLAG_B) | FLAG_U); cycleCount += 4; break; // PLP
                case 0x40: RTI(); cycleCount += 6; break;                         // RTI
                case 0x48: Push(A); cycleCount += 3; break;                       // PHA
                case 0x60: PC = RTS(); cycleCount += 6; break;                    // RTS
                case 0x68: A = Pop(); SetZN(A); cycleCount += 4; break;           // PLA

                // *** IMMEDIATE ***
                // Istruzioni che utilizzano il valore immediato
                case 0x09: A = ORA(ReadByte(PC++)); cycleCount += 2; break;       // ORA #imm
                case 0x0B: A = AND(ReadByte(PC++)); if ((A & 0x80) != 0) Status |= FLAG_C; else Status &= unchecked((byte)~FLAG_C); cycleCount += 2; break; // ANC #imm (Illegal)
                case 0x29: A = AND(ReadByte(PC++)); cycleCount += 2; break;       // AND #imm
                case 0x2B: A = AND(ReadByte(PC++)); if ((A & 0x80) != 0) Status |= FLAG_C; else Status &= unchecked((byte)~FLAG_C); cycleCount += 2; break; // ANC #imm (Illegal)
                case 0x49: A = EOR(ReadByte(PC++)); cycleCount += 2; break;       // EOR #imm
                case 0x4B: A = AND(ReadByte(PC++)); A = LSR(A); cycleCount += 2; break; // ALR #imm (Illegal)
                case 0x69: A = ADC(ReadByte(PC++)); cycleCount += 2; break;       // ADC #imm
                case 0x6B: A = AND(ReadByte(PC++)); A = ROR(A); cycleCount += 2; break; // ARR #imm (Illegal)
                case 0x80: PC++; cycleCount += 2; break;                          // NOP #imm (Illegal)
                case 0x82: PC++; cycleCount += 2; break;                          // NOP #imm (Illegal)
                case 0x89: PC++; cycleCount += 2; break;                          // NOP #imm (Illegal)
                case 0x8B: A = (byte)(X & ReadByte(PC++)); SetZN(A); cycleCount += 2; break; // XAA #imm (Illegal)
                case 0xA0: Y = LDY(ReadByte(PC++)); cycleCount += 2; break;       // LDY #imm
                case 0xA2: X = LDX(ReadByte(PC++)); cycleCount += 2; break;       // LDX #imm
                case 0xA9: A = LDA(ReadByte(PC++)); cycleCount += 2; break;       // LDA #imm
                case 0xAB: A = X = LAX(ReadByte(PC++)); cycleCount += 2; break;   // LAX #imm (Illegal)
                case 0xC0: Compare(Y, ReadByte(PC++)); cycleCount += 2; break;    // CPY #imm
                case 0xC2: PC++; cycleCount += 2; break;                          // NOP #imm (Illegal)
                case 0xC9: Compare(A, ReadByte(PC++)); cycleCount += 2; break;    // CMP #imm
                case 0xCB: X = (byte)((A & X) - ReadByte(PC++)); if (X <= (A & X)) Status |= FLAG_C; else Status &= unchecked((byte)~FLAG_C); SetZN(X); cycleCount += 2; break; // AXS #imm (Illegal)
                case 0xE0: Compare(X, ReadByte(PC++)); cycleCount += 2; break;    // CPX #imm
                case 0xE2: PC++; cycleCount += 2; break;                          // NOP #imm (Illegal)
                case 0xE9: A = SBC(ReadByte(PC++)); cycleCount += 2; break;       // SBC #imm
                case 0xEB: A = SBC(ReadByte(PC++)); cycleCount += 2; break;       // SBC #imm (Illegal duplicate)

                // *** ZERO PAGE ***
                // Istruzioni che usano indirizzamento a pagina zero
                case 0x04: PC++; cycleCount += 3; break;                          // NOP zp (Illegal)
                case 0x05: addr = ReadByte(PC++); A = ORA(ReadByte(addr)); cycleCount += 3; break; // ORA zp
                case 0x06: addr = ReadByte(PC++); val = ReadByte(addr); WriteByte(addr, ASL(val)); cycleCount += 5; break; // ASL zp
                case 0x07: addr = ReadByte(PC++); SLO(addr); cycleCount += 5; break; // SLO zp (Illegal)
                case 0x24: addr = ReadByte(PC++); BIT(ReadByte(addr)); cycleCount += 3; break; // BIT zp
                case 0x25: addr = ReadByte(PC++); A = AND(ReadByte(addr)); cycleCount += 3; break; // AND zp
                case 0x26: addr = ReadByte(PC++); val = ReadByte(addr); WriteByte(addr, ROL(val)); cycleCount += 5; break; // ROL zp
                case 0x27: addr = ReadByte(PC++); RLA(addr); cycleCount += 5; break; // RLA zp (Illegal)
                case 0x44: PC++; cycleCount += 3; break;                          // NOP zp (Illegal)
                case 0x45: addr = ReadByte(PC++); A = EOR(ReadByte(addr)); cycleCount += 3; break; // EOR zp
                case 0x46: addr = ReadByte(PC++); val = ReadByte(addr); WriteByte(addr, LSR(val)); cycleCount += 5; break; // LSR zp
                case 0x47: addr = ReadByte(PC++); SRE(addr); cycleCount += 5; break; // SRE zp (Illegal)
                case 0x64: PC++; cycleCount += 3; break;                          // NOP zp (Illegal)
                case 0x65: addr = ReadByte(PC++); A = ADC(ReadByte(addr)); cycleCount += 3; break; // ADC zp
                case 0x66: addr = ReadByte(PC++); val = ReadByte(addr); WriteByte(addr, ROR(val)); cycleCount += 5; break; // ROR zp
                case 0x67: addr = ReadByte(PC++); RRA(addr); cycleCount += 5; break; // RRA zp (Illegal)
                case 0x84: addr = ReadByte(PC++); WriteByte(addr, Y); cycleCount += 3; break; // STY zp
                case 0x85: addr = ReadByte(PC++); WriteByte(addr, A); cycleCount += 3; break; // STA zp
                case 0x86: addr = ReadByte(PC++); WriteByte(addr, X); cycleCount += 3; break; // STX zp
                case 0x87: addr = ReadByte(PC++); WriteByte(addr, (byte)(A & X)); cycleCount += 3; break; // SAX zp (Illegal)
                case 0xA4: addr = ReadByte(PC++); Y = LDY(ReadByte(addr)); cycleCount += 3; break; // LDY zp
                case 0xA5: addr = ReadByte(PC++); A = LDA(ReadByte(addr)); cycleCount += 3; break; // LDA zp
                case 0xA6: addr = ReadByte(PC++); X = LDX(ReadByte(addr)); cycleCount += 3; break; // LDX zp
                case 0xA7: addr = ReadByte(PC++); A = X = LAX(ReadByte(addr)); cycleCount += 3; break; // LAX zp (Illegal)
                case 0xC4: addr = ReadByte(PC++); Compare(Y, ReadByte(addr)); cycleCount += 3; break; // CPY zp
                case 0xC5: addr = ReadByte(PC++); Compare(A, ReadByte(addr)); cycleCount += 3; break; // CMP zp
                case 0xC6: addr = ReadByte(PC++); val = ReadByte(addr); WriteByte(addr, DEC(val)); cycleCount += 5; break; // DEC zp
                case 0xC7: addr = ReadByte(PC++); DCP(addr); cycleCount += 5; break; // DCP zp (Illegal)
                case 0xE4: addr = ReadByte(PC++); Compare(X, ReadByte(addr)); cycleCount += 3; break; // CPX zp
                case 0xE5: addr = ReadByte(PC++); A = SBC(ReadByte(addr)); cycleCount += 3; break; // SBC zp
                case 0xE6: addr = ReadByte(PC++); val = ReadByte(addr); WriteByte(addr, INC(val)); cycleCount += 5; break; // INC zp
                case 0xE7: addr = ReadByte(PC++); ISC(addr); cycleCount += 5; break; // ISC zp (Illegal)

                // *** ZERO PAGE INDEXED (X) ***
                // Istruzioni che usano indirizzamento a pagina zero indicizzato con X
                case 0x14: PC++; cycleCount += 4; break;                          // NOP zp,X (Illegal)
                case 0x15: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); A = ORA(ReadByte(addr)); cycleCount += 4; break; // ORA zp,X
                case 0x16: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); val = ReadByte(addr); WriteByte(addr, ASL(val)); cycleCount += 6; break; // ASL zp,X
                case 0x17: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); SLO(addr); cycleCount += 6; break; // SLO zp,X (Illegal)
                case 0x34: PC++; cycleCount += 4; break;                          // NOP zp,X (Illegal)
                case 0x35: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); A = AND(ReadByte(addr)); cycleCount += 4; break; // AND zp,X
                case 0x36: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); val = ReadByte(addr); WriteByte(addr, ROL(val)); cycleCount += 6; break; // ROL zp,X
                case 0x37: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); RLA(addr); cycleCount += 6; break; // RLA zp,X (Illegal)
                case 0x54: PC++; cycleCount += 4; break;                          // NOP zp,X (Illegal)
                case 0x55: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); A = EOR(ReadByte(addr)); cycleCount += 4; break; // EOR zp,X
                case 0x56: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); val = ReadByte(addr); WriteByte(addr, LSR(val)); cycleCount += 6; break; // LSR zp,X
                case 0x57: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); SRE(addr); cycleCount += 6; break; // SRE zp,X (Illegal)
                case 0x74: PC++; cycleCount += 4; break;                          // NOP zp,X (Illegal)
                case 0x75: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); A = ADC(ReadByte(addr)); cycleCount += 4; break; // ADC zp,X
                case 0x76: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); val = ReadByte(addr); WriteByte(addr, ROR(val)); cycleCount += 6; break; // ROR zp,X
                case 0x77: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); RRA(addr); cycleCount += 6; break; // RRA zp,X (Illegal)
                case 0x94: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); WriteByte(addr, Y); cycleCount += 4; break; // STY zp,X
                case 0x95: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); WriteByte(addr, A); cycleCount += 4; break; // STA zp,X
                case 0xB4: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); Y = LDY(ReadByte(addr)); cycleCount += 4; break; // LDY zp,X
                case 0xB5: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); A = LDA(ReadByte(addr)); cycleCount += 4; break; // LDA zp,X
                case 0xD4: PC++; cycleCount += 4; break;                          // NOP zp,X (Illegal)
                case 0xD5: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); Compare(A, ReadByte(addr)); cycleCount += 4; break; // CMP zp,X
                case 0xD6: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); val = ReadByte(addr); WriteByte(addr, DEC(val)); cycleCount += 6; break; // DEC zp,X
                case 0xD7: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); DCP(addr); cycleCount += 6; break; // DCP zp,X (Illegal)
                case 0xF4: PC++; cycleCount += 4; break;                          // NOP zp,X (Illegal)
                case 0xF5: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); A = SBC(ReadByte(addr)); cycleCount += 4; break; // SBC zp,X
                case 0xF6: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); val = ReadByte(addr); WriteByte(addr, INC(val)); cycleCount += 6; break; // INC zp,X
                case 0xF7: addr = (ushort)((ReadByte(PC++) + X) & 0xFF); ISC(addr); cycleCount += 6; break; // ISC zp,X (Illegal)

                // *** ZERO PAGE INDEXED (Y) ***
                // Istruzioni che usano indirizzamento a pagina zero indicizzato con Y
                case 0x96: addr = (ushort)((ReadByte(PC++) + Y) & 0xFF); WriteByte(addr, X); cycleCount += 4; break; // STX zp,Y
                case 0x97: addr = (ushort)((ReadByte(PC++) + Y) & 0xFF); WriteByte(addr, (byte)(A & X)); cycleCount += 4; break; // SAX zp,Y (Illegal)
                case 0xB6: addr = (ushort)((ReadByte(PC++) + Y) & 0xFF); X = LDX(ReadByte(addr)); cycleCount += 4; break; // LDX zp,Y
                case 0xB7: addr = (ushort)((ReadByte(PC++) + Y) & 0xFF); A = X = LAX(ReadByte(addr)); cycleCount += 4; break; // LAX zp,Y (Illegal)

                // *** ABSOLUTE ***
                // Istruzioni che usano indirizzamento assoluto
                case 0x0C: PC += 2; cycleCount += 4; break;                       // NOP abs (Illegal)
                case 0x0D: addr = ReadWord(PC); PC += 2; A = ORA(ReadByte(addr)); cycleCount += 4; break; // ORA abs
                case 0x0E: addr = ReadWord(PC); PC += 2; val = ReadByte(addr); WriteByte(addr, ASL(val)); cycleCount += 6; break; // ASL abs
                case 0x0F: addr = ReadWord(PC); PC += 2; SLO(addr); cycleCount += 6; break; // SLO abs (Illegal)
                case 0x20: JSR(ReadWord(PC)); cycleCount += 6; break;             // JSR abs
                case 0x2C: addr = ReadWord(PC); PC += 2; BIT(ReadByte(addr)); cycleCount += 4; break; // BIT abs
                case 0x2D: addr = ReadWord(PC); PC += 2; A = AND(ReadByte(addr)); cycleCount += 4; break; // AND abs
                case 0x2E: addr = ReadWord(PC); PC += 2; val = ReadByte(addr); WriteByte(addr, ROL(val)); cycleCount += 6; break; // ROL abs
                case 0x2F: addr = ReadWord(PC); PC += 2; RLA(addr); cycleCount += 6; break; // RLA abs (Illegal)
                case 0x4C: PC = ReadWord(PC); cycleCount += 3; break;             // JMP abs
                case 0x4D: addr = ReadWord(PC); PC += 2; A = EOR(ReadByte(addr)); cycleCount += 4; break; // EOR abs
                case 0x4E: addr = ReadWord(PC); PC += 2; val = ReadByte(addr); WriteByte(addr, LSR(val)); cycleCount += 6; break; // LSR abs
                case 0x4F: addr = ReadWord(PC); PC += 2; SRE(addr); cycleCount += 6; break; // SRE abs (Illegal)
                case 0x6C: addr = ReadWord(PC); PC = ReadWord(addr); cycleCount += 5; break; // JMP (ind)
                case 0x6D: addr = ReadWord(PC); PC += 2; A = ADC(ReadByte(addr)); cycleCount += 4; break; // ADC abs
                case 0x6E: addr = ReadWord(PC); PC += 2; val = ReadByte(addr); WriteByte(addr, ROR(val)); cycleCount += 6; break; // ROR abs
                case 0x6F: addr = ReadWord(PC); PC += 2; RRA(addr); cycleCount += 6; break; // RRA abs (Illegal)
                case 0x8C: addr = ReadWord(PC); PC += 2; WriteByte(addr, Y); cycleCount += 4; break; // STY abs
                case 0x8D: addr = ReadWord(PC); PC += 2; WriteByte(addr, A); cycleCount += 4; break; // STA abs
                case 0x8E: addr = ReadWord(PC); PC += 2; WriteByte(addr, X); cycleCount += 4; break; // STX abs
                case 0x8F: addr = ReadWord(PC); PC += 2; WriteByte(addr, (byte)(A & X)); cycleCount += 4; break; // SAX abs (Illegal)
                case 0xAC: addr = ReadWord(PC); PC += 2; Y = LDY(ReadByte(addr)); cycleCount += 4; break; // LDY abs
                case 0xAD: addr = ReadWord(PC); PC += 2; A = LDA(ReadByte(addr)); cycleCount += 4; break; // LDA abs
                case 0xAE: addr = ReadWord(PC); PC += 2; X = LDX(ReadByte(addr)); cycleCount += 4; break; // LDX abs
                case 0xAF: addr = ReadWord(PC); PC += 2; A = X = LAX(ReadByte(addr)); cycleCount += 4; break; // LAX abs (Illegal)
                case 0xCC: addr = ReadWord(PC); PC += 2; Compare(Y, ReadByte(addr)); cycleCount += 4; break; // CPY abs
                case 0xCD: addr = ReadWord(PC); PC += 2; Compare(A, ReadByte(addr)); cycleCount += 4; break; // CMP abs
                case 0xCE: addr = ReadWord(PC); PC += 2; val = ReadByte(addr); WriteByte(addr, DEC(val)); cycleCount += 6; break; // DEC abs
                case 0xCF: addr = ReadWord(PC); PC += 2; DCP(addr); cycleCount += 6; break; // DCP abs (Illegal)
                case 0xEC: addr = ReadWord(PC); PC += 2; Compare(X, ReadByte(addr)); cycleCount += 4; break; // CPX abs
                case 0xED: addr = ReadWord(PC); PC += 2; A = SBC(ReadByte(addr)); cycleCount += 4; break; // SBC abs
                case 0xEE: addr = ReadWord(PC); PC += 2; val = ReadByte(addr); WriteByte(addr, INC(val)); cycleCount += 6; break; // INC abs
                case 0xEF: addr = ReadWord(PC); PC += 2; ISC(addr); cycleCount += 6; break; // ISC abs (Illegal)

                // *** ABSOLUTE INDEXED (X) ***
                // Istruzioni che usano indirizzamento assoluto indicizzato con X
                case 0x1C: PC += 2; cycleCount += 4; break;                       // NOP abs,X (Illegal)
                case 0x1D: addr = (ushort)(ReadWord(PC) + X); PC += 2; A = ORA(ReadByte(addr)); cycleCount += 4; break; // ORA abs,X
                case 0x1E: addr = (ushort)(ReadWord(PC) + X); PC += 2; val = ReadByte(addr); WriteByte(addr, ASL(val)); cycleCount += 7; break; // ASL abs,X
                case 0x1F: addr = (ushort)(ReadWord(PC) + X); PC += 2; SLO(addr); cycleCount += 7; break; // SLO abs,X (Illegal)
                case 0x3C: PC += 2; cycleCount += 4; break;                       // NOP abs,X (Illegal)
                case 0x3D: addr = (ushort)(ReadWord(PC) + X); PC += 2; A = AND(ReadByte(addr)); cycleCount += 4; break; // AND abs,X
                case 0x3E: addr = (ushort)(ReadWord(PC) + X); PC += 2; val = ReadByte(addr); WriteByte(addr, ROL(val)); cycleCount += 7; break; // ROL abs,X
                case 0x3F: addr = (ushort)(ReadWord(PC) + X); PC += 2; RLA(addr); cycleCount += 7; break; // RLA abs,X (Illegal)
                case 0x5C: PC += 2; cycleCount += 4; break;                       // NOP abs,X (Illegal)
                case 0x5D: addr = (ushort)(ReadWord(PC) + X); PC += 2; A = EOR(ReadByte(addr)); cycleCount += 4; break; // EOR abs,X
                case 0x5E: addr = (ushort)(ReadWord(PC) + X); PC += 2; val = ReadByte(addr); WriteByte(addr, LSR(val)); cycleCount += 7; break; // LSR abs,X
                case 0x5F: addr = (ushort)(ReadWord(PC) + X); PC += 2; SRE(addr); cycleCount += 7; break; // SRE abs,X (Illegal)
                case 0x7C: PC += 2; cycleCount += 4; break;                       // NOP abs,X (Illegal)
                case 0x7D: addr = (ushort)(ReadWord(PC) + X); PC += 2; A = ADC(ReadByte(addr)); cycleCount += 4; break; // ADC abs,X
                case 0x7E: addr = (ushort)(ReadWord(PC) + X); PC += 2; val = ReadByte(addr); WriteByte(addr, ROR(val)); cycleCount += 7; break; // ROR abs,X
                case 0x7F: addr = (ushort)(ReadWord(PC) + X); PC += 2; RRA(addr); cycleCount += 7; break; // RRA abs,X (Illegal)
                case 0x9C: addr = (ushort)(ReadWord(PC) + X); PC += 2; WriteByte(addr, (byte)(Y & ((addr >> 8) + 1))); cycleCount += 5; break; // SHY abs,X (Illegal)
                case 0x9D: addr = (ushort)(ReadWord(PC) + X); PC += 2; WriteByte(addr, A); cycleCount += 5; break; // STA abs,X
                case 0xBC: addr = (ushort)(ReadWord(PC) + X); PC += 2; Y = LDY(ReadByte(addr)); cycleCount += 4; break; // LDY abs,X
                case 0xBD: addr = (ushort)(ReadWord(PC) + X); PC += 2; A = LDA(ReadByte(addr)); cycleCount += 4; break; // LDA abs,X
                case 0xDC: PC += 2; cycleCount += 4; break;                       // NOP abs,X (Illegal)
                case 0xDD: addr = (ushort)(ReadWord(PC) + X); PC += 2; Compare(A, ReadByte(addr)); cycleCount += 4; break; // CMP abs,X
                case 0xDE: addr = (ushort)(ReadWord(PC) + X); PC += 2; val = ReadByte(addr); WriteByte(addr, DEC(val)); cycleCount += 7; break; // DEC abs,X
                case 0xDF: addr = (ushort)(ReadWord(PC) + X); PC += 2; DCP(addr); cycleCount += 7; break; // DCP abs,X (Illegal)
                case 0xFC: PC += 2; cycleCount += 4; break;                       // NOP abs,X (Illegal)
                case 0xFD: addr = (ushort)(ReadWord(PC) + X); PC += 2; A = SBC(ReadByte(addr)); cycleCount += 4; break; // SBC abs,X
                case 0xFE: addr = (ushort)(ReadWord(PC) + X); PC += 2; val = ReadByte(addr); WriteByte(addr, INC(val)); cycleCount += 7; break; // INC abs,X
                case 0xFF: addr = (ushort)(ReadWord(PC) + X); PC += 2; ISC(addr); cycleCount += 7; break; // ISC abs,X (Illegal)

                // *** ABSOLUTE INDEXED (Y) ***
                // Istruzioni che usano indirizzamento assoluto indicizzato con Y
                case 0x19: addr = (ushort)(ReadWord(PC) + Y); PC += 2; A = ORA(ReadByte(addr)); cycleCount += 4; break; // ORA abs,Y
                case 0x1B: addr = (ushort)(ReadWord(PC) + Y); PC += 2; SLO(addr); cycleCount += 7; break; // SLO abs,Y (Illegal)
                case 0x39: addr = (ushort)(ReadWord(PC) + Y); PC += 2; A = AND(ReadByte(addr)); cycleCount += 4; break; // AND abs,Y
                case 0x3B: addr = (ushort)(ReadWord(PC) + Y); PC += 2; RLA(addr); cycleCount += 7; break; // RLA abs,Y (Illegal)
                case 0x59: addr = (ushort)(ReadWord(PC) + Y); PC += 2; A = EOR(ReadByte(addr)); cycleCount += 4; break; // EOR abs,Y
                case 0x5B: addr = (ushort)(ReadWord(PC) + Y); PC += 2; SRE(addr); cycleCount += 7; break; // SRE abs,Y (Illegal)
                case 0x79: addr = (ushort)(ReadWord(PC) + Y); PC += 2; A = ADC(ReadByte(addr)); cycleCount += 4; break; // ADC abs,Y
                case 0x7B: addr = (ushort)(ReadWord(PC) + Y); PC += 2; RRA(addr); cycleCount += 7; break; // RRA abs,Y (Illegal)
                case 0x99: addr = (ushort)(ReadWord(PC) + Y); PC += 2; WriteByte(addr, A); cycleCount += 5; break; // STA abs,Y
                case 0x9B: addr = (ushort)(ReadWord(PC) + Y); PC += 2; SP = (byte)(A & X); WriteByte(addr, (byte)(SP & ((addr >> 8) + 1))); cycleCount += 5; break; // TAS abs,Y (Illegal)
                case 0x9E: addr = (ushort)(ReadWord(PC) + Y); PC += 2; WriteByte(addr, (byte)(X & ((addr >> 8) + 1))); cycleCount += 5; break; // SHX abs,Y (Illegal)
                case 0x9F: addr = (ushort)(ReadWord(PC) + Y); PC += 2; WriteByte(addr, (byte)(A & X & ((addr >> 8) + 1))); cycleCount += 5; break; // AHX abs,Y (Illegal)
                case 0xB9: addr = (ushort)(ReadWord(PC) + Y); PC += 2; A = LDA(ReadByte(addr)); cycleCount += 4; break; // LDA abs,Y
                case 0xBB: addr = (ushort)(ReadWord(PC) + Y); PC += 2; A = X = SP = (byte)(ReadByte(addr) & SP); SetZN(A); cycleCount += 4; break; // LAS abs,Y (Illegal)
                case 0xBE: addr = (ushort)(ReadWord(PC) + Y); PC += 2; X = LDX(ReadByte(addr)); cycleCount += 4; break; // LDX abs,Y
                case 0xBF: addr = (ushort)(ReadWord(PC) + Y); PC += 2; A = X = LAX(ReadByte(addr)); cycleCount += 4; break; // LAX abs,Y (Illegal)
                case 0xD9: addr = (ushort)(ReadWord(PC) + Y); PC += 2; Compare(A, ReadByte(addr)); cycleCount += 4; break; // CMP abs,Y
                case 0xDB: addr = (ushort)(ReadWord(PC) + Y); PC += 2; DCP(addr); cycleCount += 7; break; // DCP abs,Y (Illegal)
                case 0xF9: addr = (ushort)(ReadWord(PC) + Y); PC += 2; A = SBC(ReadByte(addr)); cycleCount += 4; break; // SBC abs,Y
                case 0xFB: addr = (ushort)(ReadWord(PC) + Y); PC += 2; ISC(addr); cycleCount += 7; break; // ISC abs,Y (Illegal)

                // *** INDIRECT INDEXED (X) ***
                // Istruzioni che usano indirizzamento indiretto indicizzato con X
                case 0x01: addr = IndexedIndirectX(ReadByte(PC++)); A = ORA(ReadByte(addr)); cycleCount += 6; break; // ORA (Indirect,X)
                case 0x03: addr = IndexedIndirectX(ReadByte(PC++)); SLO(addr); cycleCount += 8; break; // SLO (Indirect,X) (Illegal)
                case 0x21: addr = IndexedIndirectX(ReadByte(PC++)); A = AND(ReadByte(addr)); cycleCount += 6; break; // AND (Indirect,X)
                case 0x23: addr = IndexedIndirectX(ReadByte(PC++)); RLA(addr); cycleCount += 8; break; // RLA (Indirect,X) (Illegal)
                case 0x41: addr = IndexedIndirectX(ReadByte(PC++)); A = EOR(ReadByte(addr)); cycleCount += 6; break; // EOR (Indirect,X)
                case 0x43: addr = IndexedIndirectX(ReadByte(PC++)); SRE(addr); cycleCount += 8; break; // SRE (Indirect,X) (Illegal)
                case 0x61: addr = IndexedIndirectX(ReadByte(PC++)); A = ADC(ReadByte(addr)); cycleCount += 6; break; // ADC (Indirect,X)
                case 0x63: addr = IndexedIndirectX(ReadByte(PC++)); RRA(addr); cycleCount += 8; break; // RRA (Indirect,X) (Illegal)
                case 0x81: addr = IndexedIndirectX(ReadByte(PC++)); WriteByte(addr, A); cycleCount += 6; break; // STA (Indirect,X)
                case 0x83: addr = IndexedIndirectX(ReadByte(PC++)); WriteByte(addr, (byte)(A & X)); cycleCount += 6; break; // SAX (Indirect,X) (Illegal)
                case 0xA1: addr = IndexedIndirectX(ReadByte(PC++)); A = LDA(ReadByte(addr)); cycleCount += 6; break; // LDA (Indirect,X)
                case 0xA3: addr = IndexedIndirectX(ReadByte(PC++)); A = X = LAX(ReadByte(addr)); cycleCount += 6; break; // LAX (Indirect,X) (Illegal)
                case 0xC1: addr = IndexedIndirectX(ReadByte(PC++)); Compare(A, ReadByte(addr)); cycleCount += 6; break; // CMP (Indirect,X)
                case 0xC3: addr = IndexedIndirectX(ReadByte(PC++)); DCP(addr); cycleCount += 8; break; // DCP (Indirect,X) (Illegal)
                case 0xE1: addr = IndexedIndirectX(ReadByte(PC++)); A = SBC(ReadByte(addr)); cycleCount += 6; break; // SBC (Indirect,X)
                case 0xE3: addr = IndexedIndirectX(ReadByte(PC++)); ISC(addr); cycleCount += 8; break; // ISC (Indirect,X) (Illegal)

                // *** INDIRECT INDEXED (Y) ***
                // Istruzioni che usano indirizzamento indiretto indicizzato con Y
                case 0x11: addr = IndirectIndexedY(ReadByte(PC++)); A = ORA(ReadByte(addr)); cycleCount += 5; break; // ORA (Indirect),Y
                case 0x13: addr = IndirectIndexedY(ReadByte(PC++)); SLO(addr); cycleCount += 8; break; // SLO (Indirect),Y (Illegal)
                case 0x31: addr = IndirectIndexedY(ReadByte(PC++)); A = AND(ReadByte(addr)); cycleCount += 5; break; // AND (Indirect),Y
                case 0x33: addr = IndirectIndexedY(ReadByte(PC++)); RLA(addr); cycleCount += 8; break; // RLA (Indirect),Y (Illegal)
                case 0x51: addr = IndirectIndexedY(ReadByte(PC++)); A = EOR(ReadByte(addr)); cycleCount += 5; break; // EOR (Indirect),Y
                case 0x53: addr = IndirectIndexedY(ReadByte(PC++)); SRE(addr); cycleCount += 8; break; // SRE (Indirect),Y (Illegal)
                case 0x71: addr = IndirectIndexedY(ReadByte(PC++)); A = ADC(ReadByte(addr)); cycleCount += 5; break; // ADC (Indirect),Y
                case 0x73: addr = IndirectIndexedY(ReadByte(PC++)); RRA(addr); cycleCount += 8; break; // RRA (Indirect),Y (Illegal)
                case 0x91: addr = IndirectIndexedY(ReadByte(PC++)); WriteByte(addr, A); cycleCount += 6; break; // STA (Indirect),Y
                case 0x93: addr = IndirectIndexedY(ReadByte(PC++)); WriteByte(addr, (byte)(A & X & ((addr >> 8) + 1))); cycleCount += 6; break; // AHX (Indirect),Y (Illegal)
                case 0xB1: addr = IndirectIndexedY(ReadByte(PC++)); A = LDA(ReadByte(addr)); cycleCount += 5; break; // LDA (Indirect),Y
                case 0xB3: addr = IndirectIndexedY(ReadByte(PC++)); A = X = LAX(ReadByte(addr)); cycleCount += 5; break; // LAX (Indirect),Y (Illegal)
                case 0xD1: addr = IndirectIndexedY(ReadByte(PC++)); Compare(A, ReadByte(addr)); cycleCount += 5; break; // CMP (Indirect),Y
                case 0xD3: addr = IndirectIndexedY(ReadByte(PC++)); DCP(addr); cycleCount += 8; break; // DCP (Indirect),Y (Illegal)
                case 0xF1: addr = IndirectIndexedY(ReadByte(PC++)); A = SBC(ReadByte(addr)); cycleCount += 5; break; // SBC (Indirect),Y
                case 0xF3: addr = IndirectIndexedY(ReadByte(PC++)); ISC(addr); cycleCount += 8; break; // ISC (Indirect),Y (Illegal)

                // *** BRANCH OPERATIONS ***
                // Istruzioni di salto condizionale
                case 0x10: branchTaken = (Status & FLAG_N) == 0; Branch(branchTaken); cycleCount += branchTaken ? 3 : 2; break; // BPL
                case 0x30: branchTaken = (Status & FLAG_N) != 0; Branch(branchTaken); cycleCount += branchTaken ? 3 : 2; break; // BMI
                case 0x50: branchTaken = (Status & FLAG_V) == 0; Branch(branchTaken); cycleCount += branchTaken ? 3 : 2; break; // BVC
                case 0x70: branchTaken = (Status & FLAG_V) != 0; Branch(branchTaken); cycleCount += branchTaken ? 3 : 2; break; // BVS
                case 0x90: branchTaken = (Status & FLAG_C) == 0; Branch(branchTaken); cycleCount += branchTaken ? 3 : 2; break; // BCC
                case 0xB0: branchTaken = (Status & FLAG_C) != 0; Branch(branchTaken); cycleCount += branchTaken ? 3 : 2; break; // BCS
                case 0xD0: branchTaken = (Status & FLAG_Z) == 0; Branch(branchTaken); cycleCount += branchTaken ? 3 : 2; break; // BNE
                case 0xF0: branchTaken = (Status & FLAG_Z) != 0; Branch(branchTaken); cycleCount += branchTaken ? 3 : 2; break; // BEQ

                // *** JAM/KIL (ILLEGAL) ***
                // Opcode che bloccano il processore
                case 0x02: /* Processor Jam */ cycleCount += 2; break;
                case 0x12: /* Processor Jam */ cycleCount += 2; break;
                case 0x22: /* Processor Jam */ cycleCount += 2; break;
                case 0x32: /* Processor Jam */ cycleCount += 2; break;
                case 0x42: /* Processor Jam */ cycleCount += 2; break;
                case 0x52: /* Processor Jam */ cycleCount += 2; break;
                case 0x62: /* Processor Jam */ cycleCount += 2; break;
                case 0x72: /* Processor Jam */ cycleCount += 2; break;
                case 0x92: /* Processor Jam */ cycleCount += 2; break;
                case 0xB2: /* Processor Jam */ cycleCount += 2; break;
                case 0xD2: /* Processor Jam */ cycleCount += 2; break;
                case 0xF2: /* Processor Jam */ cycleCount += 2; break;

                default:
                    // Istruzione sconosciuta
                    throw new UnknownOpcodeException(opcode, (ushort)(PC - 1));
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