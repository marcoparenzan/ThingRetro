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

        ushort addr;
        byte val;
        bool branchTaken;
        ushort instruction_PC = PC;
        string instruction_text = ""; // Variabile per la rappresentazione testuale dell'istruzione

        breakpointHit = false; // Resetta il flag se non ci sono breakpoint
        byte opcode = ReadByte(PC++);

        try
        {
            switch (opcode)
            {
                // *** IMPLICITO / ACCUMULATORE ***
                // Istruzioni che non richiedono operandi o usano l'accumulatore
                case 0x0A: A = ASL(A); instruction_text = "ASL A"; cycleCount += 2; break;
                case 0x18: Status &= unchecked((byte)~FLAG_C); instruction_text = "CLC"; cycleCount += 2; break;
                case 0x1A: instruction_text = "NOP"; cycleCount += 2; break;
                case 0x2A: A = ROL(A); instruction_text = "ROL A"; cycleCount += 2; break;
                case 0x38: Status |= FLAG_C; instruction_text = "SEC"; cycleCount += 2; break;
                case 0x3A: instruction_text = "NOP"; cycleCount += 2; break;
                case 0x4A: A = LSR(A); instruction_text = "LSR A"; cycleCount += 2; break;
                case 0x58: Status &= unchecked((byte)~FLAG_I); instruction_text = "CLI"; cycleCount += 2; break;
                case 0x5A: instruction_text = "NOP"; cycleCount += 2; break;
                case 0x6A: A = ROR(A); instruction_text = "ROR A"; cycleCount += 2; break;
                case 0x78: Status |= FLAG_I; instruction_text = "SEI"; cycleCount += 2; break;
                case 0x7A: instruction_text = "NOP"; cycleCount += 2; break;
                case 0x88: Y--; SetZN(Y); instruction_text = "DEY"; cycleCount += 2; break;
                case 0x8A: A = X; SetZN(A); instruction_text = "TXA"; cycleCount += 2; break;
                case 0x98: A = Y; SetZN(A); instruction_text = "TYA"; cycleCount += 2; break;
                case 0x9A: SP = X; instruction_text = "TXS"; cycleCount += 2; break;
                case 0xA8: Y = A; SetZN(Y); instruction_text = "TAY"; cycleCount += 2; break;
                case 0xAA: X = A; SetZN(X); instruction_text = "TAX"; cycleCount += 2; break;
                case 0xB8: Status &= unchecked((byte)~FLAG_V); instruction_text = "CLV"; cycleCount += 2; break;
                case 0xBA: X = SP; SetZN(X); instruction_text = "TSX"; cycleCount += 2; break;
                case 0xC8: Y++; SetZN(Y); instruction_text = "INY"; cycleCount += 2; break;
                case 0xCA: X--; SetZN(X); instruction_text = "DEX"; cycleCount += 2; break;
                case 0xD8: Status &= unchecked((byte)~FLAG_D); instruction_text = "CLD"; cycleCount += 2; break;
                case 0xDA: instruction_text = "NOP"; cycleCount += 2; break;
                case 0xE8: X++; SetZN(X); instruction_text = "INX"; cycleCount += 2; break;
                case 0xEA: instruction_text = "NOP"; cycleCount += 2; break;
                case 0xF8: Status |= FLAG_D; instruction_text = "SED"; cycleCount += 2; break;
                case 0xFA: instruction_text = "NOP"; cycleCount += 2; break;

                // *** STACK OPERATIONS ***
                case 0x00: BRK(); instruction_text = "BRK"; cycleCount += 7; break;
                case 0x08: Push((byte)(Status | FLAG_B | FLAG_U)); instruction_text = "PHP"; cycleCount += 3; break;
                case 0x28: Status = (byte)((Pop() & ~FLAG_B) | FLAG_U); instruction_text = "PLP"; cycleCount += 4; break;
                case 0x40: RTI(); instruction_text = "RTI"; cycleCount += 6; break;
                case 0x48: Push(A); instruction_text = "PHA"; cycleCount += 3; break;
                case 0x60: PC = RTS(); instruction_text = "RTS"; cycleCount += 6; break;
                case 0x68: A = Pop(); SetZN(A); instruction_text = "PLA"; cycleCount += 4; break;

                // *** IMMEDIATE ***
                // Istruzioni che utilizzano il valore immediato
                case 0x09: A = ORA(ReadByte(PC)); instruction_text = $"ORA #${ReadByte(PC):X2}"; PC++; cycleCount += 2; break;
                case 0x0B: A = AND(ReadByte(PC)); if ((A & 0x80) != 0) Status |= FLAG_C; else Status &= unchecked((byte)~FLAG_C); instruction_text = $"ANC #${ReadByte(PC):X2}"; PC++; cycleCount += 2; break;
                case 0x29: A = AND(ReadByte(PC)); instruction_text = $"AND #${ReadByte(PC):X2}"; PC++; cycleCount += 2; break;
                case 0x2B: A = AND(ReadByte(PC)); if ((A & 0x80) != 0) Status |= FLAG_C; else Status &= unchecked((byte)~FLAG_C); instruction_text = $"ANC #${ReadByte(PC):X2}"; PC++; cycleCount += 2; break;
                case 0x49: A = EOR(ReadByte(PC)); instruction_text = $"EOR #${ReadByte(PC):X2}"; PC++; cycleCount += 2; break;
                case 0x4B: A = AND(ReadByte(PC)); A = LSR(A); instruction_text = $"ALR #${ReadByte(PC):X2}"; PC++; cycleCount += 2; break;
                case 0x69: A = ADC(ReadByte(PC)); instruction_text = $"ADC #${ReadByte(PC):X2}"; PC++; cycleCount += 2; break;
                case 0x6B: A = AND(ReadByte(PC)); A = ROR(A); instruction_text = $"ARR #${ReadByte(PC):X2}"; PC++; cycleCount += 2; break;
                case 0x80: instruction_text = $"NOP #${ReadByte(PC):X2}"; PC++; cycleCount += 2; break;
                case 0x82: instruction_text = $"NOP #${ReadByte(PC):X2}"; PC++; cycleCount += 2; break;
                case 0x89: instruction_text = $"NOP #${ReadByte(PC):X2}"; PC++; cycleCount += 2; break;
                case 0x8B: A = (byte)(X & ReadByte(PC)); SetZN(A); instruction_text = $"XAA #${ReadByte(PC):X2}"; PC++; cycleCount += 2; break;
                case 0xA0: Y = LDY(ReadByte(PC)); instruction_text = $"LDY #${ReadByte(PC):X2}"; PC++; cycleCount += 2; break;
                case 0xA2: X = LDX(ReadByte(PC)); instruction_text = $"LDX #${ReadByte(PC):X2}"; PC++; cycleCount += 2; break;
                case 0xA9: A = LDA(ReadByte(PC)); instruction_text = $"LDA #${ReadByte(PC):X2}"; PC++; cycleCount += 2; break;
                case 0xAB: A = X = LAX(ReadByte(PC)); instruction_text = $"LAX #${ReadByte(PC):X2}"; PC++; cycleCount += 2; break;
                case 0xC0: Compare(Y, ReadByte(PC)); instruction_text = $"CPY #${ReadByte(PC):X2}"; PC++; cycleCount += 2; break;
                case 0xC2: instruction_text = $"NOP #${ReadByte(PC):X2}"; PC++; cycleCount += 2; break;
                case 0xC9: Compare(A, ReadByte(PC)); instruction_text = $"CMP #${ReadByte(PC):X2}"; PC++; cycleCount += 2; break;
                case 0xCB: X = (byte)((A & X) - ReadByte(PC)); if (X <= (A & X)) Status |= FLAG_C; else Status &= unchecked((byte)~FLAG_C); SetZN(X); instruction_text = $"AXS #${ReadByte(PC):X2}"; PC++; cycleCount += 2; break;
                case 0xE0: Compare(X, ReadByte(PC)); instruction_text = $"CPX #${ReadByte(PC):X2}"; PC++; cycleCount += 2; break;
                case 0xE2: instruction_text = $"NOP #${ReadByte(PC):X2}"; PC++; cycleCount += 2; break;
                case 0xE9: A = SBC(ReadByte(PC)); instruction_text = $"SBC #${ReadByte(PC):X2}"; PC++; cycleCount += 2; break;
                case 0xEB: A = SBC(ReadByte(PC)); instruction_text = $"SBC #${ReadByte(PC):X2}"; PC++; cycleCount += 2; break;

                // *** ZERO PAGE ***
                // Istruzioni che usano indirizzamento a pagina zero
                case 0x04: instruction_text = $"NOP ${ReadByte(PC):X2}"; PC++; cycleCount += 3; break;
                case 0x05: addr = ReadByte(PC); A = ORA(ReadByte(addr)); instruction_text = $"ORA ${addr:X2}"; PC++; cycleCount += 3; break;
                case 0x06: addr = ReadByte(PC); val = ReadByte(addr); WriteByte(addr, ASL(val)); instruction_text = $"ASL ${addr:X2}"; PC++; cycleCount += 5; break;
                case 0x07: addr = ReadByte(PC); SLO(addr); instruction_text = $"SLO ${addr:X2}"; PC++; cycleCount += 5; break;
                case 0x24: addr = ReadByte(PC); BIT(ReadByte(addr)); instruction_text = $"BIT ${addr:X2}"; PC++; cycleCount += 3; break;
                case 0x25: addr = ReadByte(PC); A = AND(ReadByte(addr)); instruction_text = $"AND ${addr:X2}"; PC++; cycleCount += 3; break;
                case 0x26: addr = ReadByte(PC); val = ReadByte(addr); WriteByte(addr, ROL(val)); instruction_text = $"ROL ${addr:X2}"; PC++; cycleCount += 5; break;
                case 0x27: addr = ReadByte(PC); RLA(addr); instruction_text = $"RLA ${addr:X2}"; PC++; cycleCount += 5; break;
                case 0x44: instruction_text = $"NOP ${ReadByte(PC):X2}"; PC++; cycleCount += 3; break;
                case 0x45: addr = ReadByte(PC); A = EOR(ReadByte(addr)); instruction_text = $"EOR ${addr:X2}"; PC++; cycleCount += 3; break;
                case 0x46: addr = ReadByte(PC); val = ReadByte(addr); WriteByte(addr, LSR(val)); instruction_text = $"LSR ${addr:X2}"; PC++; cycleCount += 5; break;
                case 0x47: addr = ReadByte(PC); SRE(addr); instruction_text = $"SRE ${addr:X2}"; PC++; cycleCount += 5; break;
                case 0x64: instruction_text = $"NOP ${ReadByte(PC):X2}"; PC++; cycleCount += 3; break;
                case 0x65: addr = ReadByte(PC); A = ADC(ReadByte(addr)); instruction_text = $"ADC ${addr:X2}"; PC++; cycleCount += 3; break;
                case 0x66: addr = ReadByte(PC); val = ReadByte(addr); WriteByte(addr, ROR(val)); instruction_text = $"ROR ${addr:X2}"; PC++; cycleCount += 5; break;
                case 0x67: addr = ReadByte(PC); RRA(addr); instruction_text = $"RRA ${addr:X2}"; PC++; cycleCount += 5; break;
                case 0x84: addr = ReadByte(PC); WriteByte(addr, Y); instruction_text = $"STY ${addr:X2}"; PC++; cycleCount += 3; break;
                case 0x85: addr = ReadByte(PC); WriteByte(addr, A); instruction_text = $"STA ${addr:X2}"; PC++; cycleCount += 3; break;
                case 0x86: addr = ReadByte(PC); WriteByte(addr, X); instruction_text = $"STX ${addr:X2}"; PC++; cycleCount += 3; break;
                case 0x87: addr = ReadByte(PC); WriteByte(addr, (byte)(A & X)); instruction_text = $"SAX ${addr:X2}"; PC++; cycleCount += 3; break;
                case 0xA4: addr = ReadByte(PC); Y = LDY(ReadByte(addr)); instruction_text = $"LDY ${addr:X2}"; PC++; cycleCount += 3; break;
                case 0xA5: addr = ReadByte(PC); A = LDA(ReadByte(addr)); instruction_text = $"LDA ${addr:X2}"; PC++; cycleCount += 3; break;
                case 0xA6: addr = ReadByte(PC); X = LDX(ReadByte(addr)); instruction_text = $"LDX ${addr:X2}"; PC++; cycleCount += 3; break;
                case 0xA7: addr = ReadByte(PC); A = X = LAX(ReadByte(addr)); instruction_text = $"LAX ${addr:X2}"; PC++; cycleCount += 3; break;
                case 0xC4: addr = ReadByte(PC); Compare(Y, ReadByte(addr)); instruction_text = $"CPY ${addr:X2}"; PC++; cycleCount += 3; break;
                case 0xC5: addr = ReadByte(PC); Compare(A, ReadByte(addr)); instruction_text = $"CMP ${addr:X2}"; PC++; cycleCount += 3; break;
                case 0xC6: addr = ReadByte(PC); val = ReadByte(addr); WriteByte(addr, DEC(val)); instruction_text = $"DEC ${addr:X2}"; PC++; cycleCount += 5; break;
                case 0xC7: addr = ReadByte(PC); DCP(addr); instruction_text = $"DCP ${addr:X2}"; PC++; cycleCount += 5; break;
                case 0xE4: addr = ReadByte(PC); Compare(X, ReadByte(addr)); instruction_text = $"CPX ${addr:X2}"; PC++; cycleCount += 3; break;
                case 0xE5: addr = ReadByte(PC); A = SBC(ReadByte(addr)); instruction_text = $"SBC ${addr:X2}"; PC++; cycleCount += 3; break;
                case 0xE6: addr = ReadByte(PC); val = ReadByte(addr); WriteByte(addr, INC(val)); instruction_text = $"INC ${addr:X2}"; PC++; cycleCount += 5; break;
                case 0xE7: addr = ReadByte(PC); ISC(addr); instruction_text = $"ISC ${addr:X2}"; PC++; cycleCount += 5; break;

                // *** ZERO PAGE INDEXED (X) ***
                // Istruzioni che usano indirizzamento a pagina zero indicizzato con X
                case 0x14: instruction_text = $"NOP ${ReadByte(PC):X2},X"; PC++; cycleCount += 4; break;
                case 0x15: addr = (ushort)((ReadByte(PC) + X) & 0xFF); A = ORA(ReadByte(addr)); instruction_text = $"ORA ${ReadByte(PC):X2},X"; PC++; cycleCount += 4; break;
                case 0x16: addr = (ushort)((ReadByte(PC) + X) & 0xFF); val = ReadByte(addr); WriteByte(addr, ASL(val)); instruction_text = $"ASL ${ReadByte(PC):X2},X"; PC++; cycleCount += 6; break;
                case 0x17: addr = (ushort)((ReadByte(PC) + X) & 0xFF); SLO(addr); instruction_text = $"SLO ${ReadByte(PC):X2},X"; PC++; cycleCount += 6; break;
                case 0x34: instruction_text = $"NOP ${ReadByte(PC):X2},X"; PC++; cycleCount += 4; break;
                case 0x35: addr = (ushort)((ReadByte(PC) + X) & 0xFF); A = AND(ReadByte(addr)); instruction_text = $"AND ${ReadByte(PC):X2},X"; PC++; cycleCount += 4; break;
                case 0x36: addr = (ushort)((ReadByte(PC) + X) & 0xFF); val = ReadByte(addr); WriteByte(addr, ROL(val)); instruction_text = $"ROL ${ReadByte(PC):X2},X"; PC++; cycleCount += 6; break;
                case 0x37: addr = (ushort)((ReadByte(PC) + X) & 0xFF); RLA(addr); instruction_text = $"RLA ${ReadByte(PC):X2},X"; PC++; cycleCount += 6; break;
                case 0x54: instruction_text = $"NOP ${ReadByte(PC):X2},X"; PC++; cycleCount += 4; break;
                case 0x55: addr = (ushort)((ReadByte(PC) + X) & 0xFF); A = EOR(ReadByte(addr)); instruction_text = $"EOR ${ReadByte(PC):X2},X"; PC++; cycleCount += 4; break;
                case 0x56: addr = (ushort)((ReadByte(PC) + X) & 0xFF); val = ReadByte(addr); WriteByte(addr, LSR(val)); instruction_text = $"LSR ${ReadByte(PC):X2},X"; PC++; cycleCount += 6; break;
                case 0x57: addr = (ushort)((ReadByte(PC) + X) & 0xFF); SRE(addr); instruction_text = $"SRE ${ReadByte(PC):X2},X"; PC++; cycleCount += 6; break;
                case 0x74: instruction_text = $"NOP ${ReadByte(PC):X2},X"; PC++; cycleCount += 4; break;
                case 0x75: addr = (ushort)((ReadByte(PC) + X) & 0xFF); A = ADC(ReadByte(addr)); instruction_text = $"ADC ${ReadByte(PC):X2},X"; PC++; cycleCount += 4; break;
                case 0x76: addr = (ushort)((ReadByte(PC) + X) & 0xFF); val = ReadByte(addr); WriteByte(addr, ROR(val)); instruction_text = $"ROR ${ReadByte(PC):X2},X"; PC++; cycleCount += 6; break;
                case 0x77: addr = (ushort)((ReadByte(PC) + X) & 0xFF); RRA(addr); instruction_text = $"RRA ${ReadByte(PC):X2},X"; PC++; cycleCount += 6; break;
                case 0x94: addr = (ushort)((ReadByte(PC) + X) & 0xFF); WriteByte(addr, Y); instruction_text = $"STY ${ReadByte(PC):X2},X"; PC++; cycleCount += 4; break;
                case 0x95: addr = (ushort)((ReadByte(PC) + X) & 0xFF); WriteByte(addr, A); instruction_text = $"STA ${ReadByte(PC):X2},X"; PC++; cycleCount += 4; break;
                case 0xB4: addr = (ushort)((ReadByte(PC) + X) & 0xFF); Y = LDY(ReadByte(addr)); instruction_text = $"LDY ${ReadByte(PC):X2},X"; PC++; cycleCount += 4; break;
                case 0xB5: addr = (ushort)((ReadByte(PC) + X) & 0xFF); A = LDA(ReadByte(addr)); instruction_text = $"LDA ${ReadByte(PC):X2},X"; PC++; cycleCount += 4; break;
                case 0xD4: instruction_text = $"NOP ${ReadByte(PC):X2},X"; PC++; cycleCount += 4; break;
                case 0xD5: addr = (ushort)((ReadByte(PC) + X) & 0xFF); Compare(A, ReadByte(addr)); instruction_text = $"CMP ${ReadByte(PC):X2},X"; PC++; cycleCount += 4; break;
                case 0xD6: addr = (ushort)((ReadByte(PC) + X) & 0xFF); val = ReadByte(addr); WriteByte(addr, DEC(val)); instruction_text = $"DEC ${ReadByte(PC):X2},X"; PC++; cycleCount += 6; break;
                case 0xD7: addr = (ushort)((ReadByte(PC) + X) & 0xFF); DCP(addr); instruction_text = $"DCP ${ReadByte(PC):X2},X"; PC++; cycleCount += 6; break;
                case 0xF4: instruction_text = $"NOP ${ReadByte(PC):X2},X"; PC++; cycleCount += 4; break;
                case 0xF5: addr = (ushort)((ReadByte(PC) + X) & 0xFF); A = SBC(ReadByte(addr)); instruction_text = $"SBC ${ReadByte(PC):X2},X"; PC++; cycleCount += 4; break;
                case 0xF6: addr = (ushort)((ReadByte(PC) + X) & 0xFF); val = ReadByte(addr); WriteByte(addr, INC(val)); instruction_text = $"INC ${ReadByte(PC):X2},X"; PC++; cycleCount += 6; break;
                case 0xF7: addr = (ushort)((ReadByte(PC) + X) & 0xFF); ISC(addr); instruction_text = $"ISC ${ReadByte(PC):X2},X"; PC++; cycleCount += 6; break;
             
                    // *** ABSOLUTE ***
                // Istruzioni che usano indirizzamento assoluto
                case 0x0C: PC += 2; instruction_text = $"NOP ${ReadWord(PC):X4}"; cycleCount += 4; break;
                case 0x0D: addr = ReadWord(PC); PC += 2; A = ORA(ReadByte(addr)); instruction_text = $"ORA ${addr:X4}"; cycleCount += 4; break;
                case 0x0E: addr = ReadWord(PC); PC += 2; val = ReadByte(addr); WriteByte(addr, ASL(val)); instruction_text = $"ASL ${addr:X4}"; cycleCount += 6; break;
                case 0x0F: addr = ReadWord(PC); PC += 2; SLO(addr); instruction_text = $"SLO ${addr:X4}"; cycleCount += 6; break;
                case 0x20: addr = ReadWord(PC); JSR(addr); instruction_text = $"JSR ${addr:X4}"; cycleCount += 6; break;
                case 0x2C: addr = ReadWord(PC); PC += 2; BIT(ReadByte(addr)); instruction_text = $"BIT ${addr:X4}"; cycleCount += 4; break;
                case 0x2D: addr = ReadWord(PC); PC += 2; A = AND(ReadByte(addr)); instruction_text = $"AND ${addr:X4}"; cycleCount += 4; break;
                case 0x2E: addr = ReadWord(PC); PC += 2; val = ReadByte(addr); WriteByte(addr, ROL(val)); instruction_text = $"ROL ${addr:X4}"; cycleCount += 6; break;
                case 0x2F: addr = ReadWord(PC); PC += 2; RLA(addr); instruction_text = $"RLA ${addr:X4}"; cycleCount += 6; break;
                case 0x4C: addr = ReadWord(PC); PC = addr; instruction_text = $"JMP ${addr:X4}"; cycleCount += 3; break;
                case 0x4D: addr = ReadWord(PC); PC += 2; A = EOR(ReadByte(addr)); instruction_text = $"EOR ${addr:X4}"; cycleCount += 4; break;
                case 0x4E: addr = ReadWord(PC); PC += 2; val = ReadByte(addr); WriteByte(addr, LSR(val)); instruction_text = $"LSR ${addr:X4}"; cycleCount += 6; break;
                case 0x4F: addr = ReadWord(PC); PC += 2; SRE(addr); instruction_text = $"SRE ${addr:X4}"; cycleCount += 6; break;
                case 0x6C: addr = ReadWord(PC); PC = ReadWord(addr); instruction_text = $"JMP (${addr:X4})"; cycleCount += 5; break;
                case 0x6D: addr = ReadWord(PC); PC += 2; A = ADC(ReadByte(addr)); instruction_text = $"ADC ${addr:X4}"; cycleCount += 4; break;
                case 0x6E: addr = ReadWord(PC); PC += 2; val = ReadByte(addr); WriteByte(addr, ROR(val)); instruction_text = $"ROR ${addr:X4}"; cycleCount += 6; break;
                case 0x6F: addr = ReadWord(PC); PC += 2; RRA(addr); instruction_text = $"RRA ${addr:X4}"; cycleCount += 6; break;
                case 0x8C: addr = ReadWord(PC); PC += 2; WriteByte(addr, Y); instruction_text = $"STY ${addr:X4}"; cycleCount += 4; break;
                case 0x8D: addr = ReadWord(PC); PC += 2; WriteByte(addr, A); instruction_text = $"STA ${addr:X4}"; cycleCount += 4; break;
                case 0x8E: addr = ReadWord(PC); PC += 2; WriteByte(addr, X); instruction_text = $"STX ${addr:X4}"; cycleCount += 4; break;
                case 0x8F: addr = ReadWord(PC); PC += 2; WriteByte(addr, (byte)(A & X)); instruction_text = $"SAX ${addr:X4}"; cycleCount += 4; break;
                case 0xAC: addr = ReadWord(PC); PC += 2; Y = LDY(ReadByte(addr)); instruction_text = $"LDY ${addr:X4}"; cycleCount += 4; break;
                case 0xAD: addr = ReadWord(PC); PC += 2; A = LDA(ReadByte(addr)); instruction_text = $"LDA ${addr:X4}"; cycleCount += 4; break;
                case 0xAE: addr = ReadWord(PC); PC += 2; X = LDX(ReadByte(addr)); instruction_text = $"LDX ${addr:X4}"; cycleCount += 4; break;
                case 0xAF: addr = ReadWord(PC); PC += 2; A = X = LAX(ReadByte(addr)); instruction_text = $"LAX ${addr:X4}"; cycleCount += 4; break;
                case 0xCC: addr = ReadWord(PC); PC += 2; Compare(Y, ReadByte(addr)); instruction_text = $"CPY ${addr:X4}"; cycleCount += 4; break;
                case 0xCD: addr = ReadWord(PC); PC += 2; Compare(A, ReadByte(addr)); instruction_text = $"CMP ${addr:X4}"; cycleCount += 4; break;
                case 0xCE: addr = ReadWord(PC); PC += 2; val = ReadByte(addr); WriteByte(addr, DEC(val)); instruction_text = $"DEC ${addr:X4}"; cycleCount += 6; break;
                case 0xCF: addr = ReadWord(PC); PC += 2; DCP(addr); instruction_text = $"DCP ${addr:X4}"; cycleCount += 6; break;
                case 0xEC: addr = ReadWord(PC); PC += 2; Compare(X, ReadByte(addr)); instruction_text = $"CPX ${addr:X4}"; cycleCount += 4; break;
                case 0xED: addr = ReadWord(PC); PC += 2; A = SBC(ReadByte(addr)); instruction_text = $"SBC ${addr:X4}"; cycleCount += 4; break;
                case 0xEE: addr = ReadWord(PC); PC += 2; val = ReadByte(addr); WriteByte(addr, INC(val)); instruction_text = $"INC ${addr:X4}"; cycleCount += 6; break;
                case 0xEF: addr = ReadWord(PC); PC += 2; ISC(addr); instruction_text = $"ISC ${addr:X4}"; cycleCount += 6; break;

                // *** ABSOLUTE INDEXED (X) ***
                // Istruzioni che usano indirizzamento assoluto indicizzato con X
                case 0x1C: PC += 2; instruction_text = $"NOP ${ReadWord(PC):X4},X"; cycleCount += 4; break;
                case 0x1D: addr = (ushort)(ReadWord(PC) + X); PC += 2; A = ORA(ReadByte(addr)); instruction_text = $"ORA ${ReadWord(PC):X4},X"; cycleCount += 4; break;
                case 0x1E: addr = (ushort)(ReadWord(PC) + X); PC += 2; val = ReadByte(addr); WriteByte(addr, ASL(val)); instruction_text = $"ASL ${ReadWord(PC):X4},X"; cycleCount += 7; break;
                case 0x1F: addr = (ushort)(ReadWord(PC) + X); PC += 2; SLO(addr); instruction_text = $"SLO ${ReadWord(PC):X4},X"; cycleCount += 7; break;
                case 0x3C: PC += 2; instruction_text = $"NOP ${ReadWord(PC):X4},X"; cycleCount += 4; break;
                case 0x3D: addr = (ushort)(ReadWord(PC) + X); PC += 2; A = AND(ReadByte(addr)); instruction_text = $"AND ${ReadWord(PC):X4},X"; cycleCount += 4; break;
                case 0x3E: addr = (ushort)(ReadWord(PC) + X); PC += 2; val = ReadByte(addr); WriteByte(addr, ROL(val)); instruction_text = $"ROL ${ReadWord(PC):X4},X"; cycleCount += 7; break;
                case 0x3F: addr = (ushort)(ReadWord(PC) + X); PC += 2; RLA(addr); instruction_text = $"RLA ${ReadWord(PC):X4},X"; cycleCount += 7; break;
                case 0x5C: PC += 2; instruction_text = $"NOP ${ReadWord(PC):X4},X"; cycleCount += 4; break;
                case 0x5D: addr = (ushort)(ReadWord(PC) + X); PC += 2; A = EOR(ReadByte(addr)); instruction_text = $"EOR ${ReadWord(PC):X4},X"; cycleCount += 4; break;
                case 0x5E: addr = (ushort)(ReadWord(PC) + X); PC += 2; val = ReadByte(addr); WriteByte(addr, LSR(val)); instruction_text = $"LSR ${ReadWord(PC):X4},X"; cycleCount += 7; break;
                case 0x5F: addr = (ushort)(ReadWord(PC) + X); PC += 2; SRE(addr); instruction_text = $"SRE ${ReadWord(PC):X4},X"; cycleCount += 7; break;
                case 0x7C: PC += 2; instruction_text = $"NOP ${ReadWord(PC):X4},X"; cycleCount += 4; break;
                case 0x7D: addr = (ushort)(ReadWord(PC) + X); PC += 2; A = ADC(ReadByte(addr)); instruction_text = $"ADC ${ReadWord(PC):X4},X"; cycleCount += 4; break;
                case 0x7E: addr = (ushort)(ReadWord(PC) + X); PC += 2; val = ReadByte(addr); WriteByte(addr, ROR(val)); instruction_text = $"ROR ${ReadWord(PC):X4},X"; cycleCount += 7; break;
                case 0x7F: addr = (ushort)(ReadWord(PC) + X); PC += 2; RRA(addr); instruction_text = $"RRA ${ReadWord(PC):X4},X"; cycleCount += 7; break;
                case 0x9C: addr = (ushort)(ReadWord(PC) + X); PC += 2; WriteByte(addr, (byte)(Y & ((addr >> 8) + 1))); instruction_text = $"SHY ${ReadWord(PC):X4},X"; cycleCount += 5; break;
                case 0x9D: addr = (ushort)(ReadWord(PC) + X); PC += 2; WriteByte(addr, A); instruction_text = $"STA ${ReadWord(PC):X4},X"; cycleCount += 5; break;
                case 0xBC: addr = (ushort)(ReadWord(PC) + X); PC += 2; Y = LDY(ReadByte(addr)); instruction_text = $"LDY ${ReadWord(PC):X4},X"; cycleCount += 4; break;
                case 0xBD: addr = (ushort)(ReadWord(PC) + X); PC += 2; A = LDA(ReadByte(addr)); instruction_text = $"LDA ${ReadWord(PC):X4},X"; cycleCount += 4; break;
                case 0xDC: PC += 2; instruction_text = $"NOP ${ReadWord(PC):X4},X"; cycleCount += 4; break;
                case 0xDD: addr = (ushort)(ReadWord(PC) + X); PC += 2; Compare(A, ReadByte(addr)); instruction_text = $"CMP ${ReadWord(PC):X4},X"; cycleCount += 4; break;
                case 0xDE: addr = (ushort)(ReadWord(PC) + X); PC += 2; val = ReadByte(addr); WriteByte(addr, DEC(val)); instruction_text = $"DEC ${ReadWord(PC):X4},X"; cycleCount += 7; break;
                case 0xDF: addr = (ushort)(ReadWord(PC) + X); PC += 2; DCP(addr); instruction_text = $"DCP ${ReadWord(PC):X4},X"; cycleCount += 7; break;
                case 0xFC: PC += 2; instruction_text = $"NOP ${ReadWord(PC):X4},X"; cycleCount += 4; break;
                case 0xFD: addr = (ushort)(ReadWord(PC) + X); PC += 2; A = SBC(ReadByte(addr)); instruction_text = $"SBC ${ReadWord(PC):X4},X"; cycleCount += 4; break;
                case 0xFE: addr = (ushort)(ReadWord(PC) + X); PC += 2; val = ReadByte(addr); WriteByte(addr, INC(val)); instruction_text = $"INC ${ReadWord(PC):X4},X"; cycleCount += 7; break;
                case 0xFF: addr = (ushort)(ReadWord(PC) + X); PC += 2; ISC(addr); instruction_text = $"ISC ${ReadWord(PC):X4},X"; cycleCount += 7; break;

                // *** ABSOLUTE INDEXED (Y) ***
                // Istruzioni che usano indirizzamento assoluto indicizzato con Y
                case 0x19: addr = (ushort)(ReadWord(PC) + Y); PC += 2; A = ORA(ReadByte(addr)); instruction_text = $"ORA ${ReadWord(PC):X4},Y"; cycleCount += 4; break;
                case 0x1B: addr = (ushort)(ReadWord(PC) + Y); PC += 2; SLO(addr); instruction_text = $"SLO ${ReadWord(PC):X4},Y"; cycleCount += 7; break;
                case 0x39: addr = (ushort)(ReadWord(PC) + Y); PC += 2; A = AND(ReadByte(addr)); instruction_text = $"AND ${ReadWord(PC):X4},Y"; cycleCount += 4; break;
                case 0x3B: addr = (ushort)(ReadWord(PC) + Y); PC += 2; RLA(addr); instruction_text = $"RLA ${ReadWord(PC):X4},Y"; cycleCount += 7; break;
                case 0x59: addr = (ushort)(ReadWord(PC) + Y); PC += 2; A = EOR(ReadByte(addr)); instruction_text = $"EOR ${ReadWord(PC):X4},Y"; cycleCount += 4; break;
                case 0x5B: addr = (ushort)(ReadWord(PC) + Y); PC += 2; SRE(addr); instruction_text = $"SRE ${ReadWord(PC):X4},Y"; cycleCount += 7; break;
                case 0x79: addr = (ushort)(ReadWord(PC) + Y); PC += 2; A = ADC(ReadByte(addr)); instruction_text = $"ADC ${ReadWord(PC):X4},Y"; cycleCount += 4; break;
                case 0x7B: addr = (ushort)(ReadWord(PC) + Y); PC += 2; RRA(addr); instruction_text = $"RRA ${ReadWord(PC):X4},Y"; cycleCount += 7; break;
                case 0x99: addr = (ushort)(ReadWord(PC) + Y); PC += 2; WriteByte(addr, A); instruction_text = $"STA ${ReadWord(PC):X4},Y"; cycleCount += 5; break;
                case 0x9B: addr = (ushort)(ReadWord(PC) + Y); PC += 2; SP = (byte)(A & X); WriteByte(addr, (byte)(SP & ((addr >> 8) + 1))); instruction_text = $"TAS ${ReadWord(PC):X4},Y"; cycleCount += 5; break;
                case 0x9E: addr = (ushort)(ReadWord(PC) + Y); PC += 2; WriteByte(addr, (byte)(X & ((addr >> 8) + 1))); instruction_text = $"SHX ${ReadWord(PC):X4},Y"; cycleCount += 5; break;
                case 0x9F: addr = (ushort)(ReadWord(PC) + Y); PC += 2; WriteByte(addr, (byte)(A & X & ((addr >> 8) + 1))); instruction_text = $"AHX ${ReadWord(PC):X4},Y"; cycleCount += 5; break;
                case 0xB9: addr = (ushort)(ReadWord(PC) + Y); PC += 2; A = LDA(ReadByte(addr)); instruction_text = $"LDA ${ReadWord(PC):X4},Y"; cycleCount += 4; break;
                case 0xBB: addr = (ushort)(ReadWord(PC) + Y); PC += 2; A = X = SP = (byte)(ReadByte(addr) & SP); SetZN(A); instruction_text = $"LAS ${ReadWord(PC):X4},Y"; cycleCount += 4; break;
                case 0xBE: addr = (ushort)(ReadWord(PC) + Y); PC += 2; X = LDX(ReadByte(addr)); instruction_text = $"LDX ${ReadWord(PC):X4},Y"; cycleCount += 4; break;
                case 0xBF: addr = (ushort)(ReadWord(PC) + Y); PC += 2; A = X = LAX(ReadByte(addr)); instruction_text = $"LAX ${ReadWord(PC):X4},Y"; cycleCount += 4; break;
                case 0xD9: addr = (ushort)(ReadWord(PC) + Y); PC += 2; Compare(A, ReadByte(addr)); instruction_text = $"CMP ${ReadWord(PC):X4},Y"; cycleCount += 4; break;
                case 0xDB: addr = (ushort)(ReadWord(PC) + Y); PC += 2; DCP(addr); instruction_text = $"DCP ${ReadWord(PC):X4},Y"; cycleCount += 7; break;
                case 0xF9: addr = (ushort)(ReadWord(PC) + Y); PC += 2; A = SBC(ReadByte(addr)); instruction_text = $"SBC ${ReadWord(PC):X4},Y"; cycleCount += 4; break;
                case 0xFB: addr = (ushort)(ReadWord(PC) + Y); PC += 2; ISC(addr); instruction_text = $"ISC ${ReadWord(PC):X4},Y"; cycleCount += 7; break;

                // *** INDIRECT INDEXED (X) ***
                // Istruzioni che usano indirizzamento indiretto indicizzato con X
                case 0x01: addr = IndexedIndirectX(ReadByte(PC)); A = ORA(ReadByte(addr)); instruction_text = $"ORA (${ReadByte(PC):X2},X)"; PC++; cycleCount += 6; break;
                case 0x03: addr = IndexedIndirectX(ReadByte(PC)); SLO(addr); instruction_text = $"SLO (${ReadByte(PC):X2},X)"; PC++; cycleCount += 8; break;
                case 0x21: addr = IndexedIndirectX(ReadByte(PC)); A = AND(ReadByte(addr)); instruction_text = $"AND (${ReadByte(PC):X2},X)"; PC++; cycleCount += 6; break;
                case 0x23: addr = IndexedIndirectX(ReadByte(PC)); RLA(addr); instruction_text = $"RLA (${ReadByte(PC):X2},X)"; PC++; cycleCount += 8; break;
                case 0x41: addr = IndexedIndirectX(ReadByte(PC)); A = EOR(ReadByte(addr)); instruction_text = $"EOR (${ReadByte(PC):X2},X)"; PC++; cycleCount += 6; break;
                case 0x43: addr = IndexedIndirectX(ReadByte(PC)); SRE(addr); instruction_text = $"SRE (${ReadByte(PC):X2},X)"; PC++; cycleCount += 8; break;
                case 0x61: addr = IndexedIndirectX(ReadByte(PC)); A = ADC(ReadByte(addr)); instruction_text = $"ADC (${ReadByte(PC):X2},X)"; PC++; cycleCount += 6; break;
                case 0x63: addr = IndexedIndirectX(ReadByte(PC)); RRA(addr); instruction_text = $"RRA (${ReadByte(PC):X2},X)"; PC++; cycleCount += 8; break;
                case 0x81: addr = IndexedIndirectX(ReadByte(PC)); WriteByte(addr, A); instruction_text = $"STA (${ReadByte(PC):X2},X)"; PC++; cycleCount += 6; break;
                case 0x83: addr = IndexedIndirectX(ReadByte(PC)); WriteByte(addr, (byte)(A & X)); instruction_text = $"SAX (${ReadByte(PC):X2},X)"; PC++; cycleCount += 6; break;
                case 0xA1: addr = IndexedIndirectX(ReadByte(PC)); A = LDA(ReadByte(addr)); instruction_text = $"LDA (${ReadByte(PC):X2},X)"; PC++; cycleCount += 6; break;
                case 0xA3: addr = IndexedIndirectX(ReadByte(PC)); A = X = LAX(ReadByte(addr)); instruction_text = $"LAX (${ReadByte(PC):X2},X)"; PC++; cycleCount += 6; break;
                case 0xC1: addr = IndexedIndirectX(ReadByte(PC)); Compare(A, ReadByte(addr)); instruction_text = $"CMP (${ReadByte(PC):X2},X)"; PC++; cycleCount += 6; break;
                case 0xC3: addr = IndexedIndirectX(ReadByte(PC)); DCP(addr); instruction_text = $"DCP (${ReadByte(PC):X2},X)"; PC++; cycleCount += 8; break;
                case 0xE1: addr = IndexedIndirectX(ReadByte(PC)); A = SBC(ReadByte(addr)); instruction_text = $"SBC (${ReadByte(PC):X2},X)"; PC++; cycleCount += 6; break;
                case 0xE3: addr = IndexedIndirectX(ReadByte(PC)); ISC(addr); instruction_text = $"ISC (${ReadByte(PC):X2},X)"; PC++; cycleCount += 8; break;

                // *** INDIRECT INDEXED (Y) ***
                // Istruzioni che usano indirizzamento indiretto indicizzato con Y
                case 0x11: addr = IndirectIndexedY(ReadByte(PC)); A = ORA(ReadByte(addr)); instruction_text = $"ORA (${ReadByte(PC):X2}),Y"; PC++; cycleCount += 5; break;
                case 0x13: addr = IndirectIndexedY(ReadByte(PC)); SLO(addr); instruction_text = $"SLO (${ReadByte(PC):X2}),Y"; PC++; cycleCount += 8; break;
                case 0x31: addr = IndirectIndexedY(ReadByte(PC)); A = AND(ReadByte(addr)); instruction_text = $"AND (${ReadByte(PC):X2}),Y"; PC++; cycleCount += 5; break;
                case 0x33: addr = IndirectIndexedY(ReadByte(PC)); RLA(addr); instruction_text = $"RLA (${ReadByte(PC):X2}),Y"; PC++; cycleCount += 8; break;
                case 0x51: addr = IndirectIndexedY(ReadByte(PC)); A = EOR(ReadByte(addr)); instruction_text = $"EOR (${ReadByte(PC):X2}),Y"; PC++; cycleCount += 5; break;
                case 0x53: addr = IndirectIndexedY(ReadByte(PC)); SRE(addr); instruction_text = $"SRE (${ReadByte(PC):X2}),Y"; PC++; cycleCount += 8; break;
                case 0x71: addr = IndirectIndexedY(ReadByte(PC)); A = ADC(ReadByte(addr)); instruction_text = $"ADC (${ReadByte(PC):X2}),Y"; PC++; cycleCount += 5; break;
                case 0x73: addr = IndirectIndexedY(ReadByte(PC)); RRA(addr); instruction_text = $"RRA (${ReadByte(PC):X2}),Y"; PC++; cycleCount += 8; break;
                case 0x91: addr = IndirectIndexedY(ReadByte(PC)); WriteByte(addr, A); instruction_text = $"STA (${ReadByte(PC):X2}),Y"; PC++; cycleCount += 6; break;
                case 0x93: addr = IndirectIndexedY(ReadByte(PC)); WriteByte(addr, (byte)(A & X & ((addr >> 8) + 1))); instruction_text = $"AHX (${ReadByte(PC):X2}),Y"; PC++; cycleCount += 6; break;
                case 0xB1: addr = IndirectIndexedY(ReadByte(PC)); A = LDA(ReadByte(addr)); instruction_text = $"LDA (${ReadByte(PC):X2}),Y"; PC++; cycleCount += 5; break;
                case 0xB3: addr = IndirectIndexedY(ReadByte(PC)); A = X = LAX(ReadByte(addr)); instruction_text = $"LAX (${ReadByte(PC):X2}),Y"; PC++; cycleCount += 5; break;
                case 0xD1: addr = IndirectIndexedY(ReadByte(PC)); Compare(A, ReadByte(addr)); instruction_text = $"CMP (${ReadByte(PC):X2}),Y"; PC++; cycleCount += 5; break;
                case 0xD3: addr = IndirectIndexedY(ReadByte(PC)); DCP(addr); instruction_text = $"DCP (${ReadByte(PC):X2}),Y"; PC++; cycleCount += 8; break;
                case 0xF1: addr = IndirectIndexedY(ReadByte(PC)); A = SBC(ReadByte(addr)); instruction_text = $"SBC (${ReadByte(PC):X2}),Y"; PC++; cycleCount += 5; break;
                case 0xF3: addr = IndirectIndexedY(ReadByte(PC)); ISC(addr); instruction_text = $"ISC (${ReadByte(PC):X2}),Y"; PC++; cycleCount += 8; break;

                // *** BRANCH OPERATIONS ***
                // Istruzioni di salto condizionale
                case 0x10: branchTaken = (Status & FLAG_N) == 0; Branch(branchTaken); instruction_text = $"BPL ${(sbyte)ReadByte((ushort)(PC - 1)):+00;-00}"; cycleCount += branchTaken ? 3 : 2; break;
                case 0x30: branchTaken = (Status & FLAG_N) != 0; Branch(branchTaken); instruction_text = $"BMI ${(sbyte)ReadByte((ushort)(PC - 1)):+00;-00}"; cycleCount += branchTaken ? 3 : 2; break;
                case 0x50: branchTaken = (Status & FLAG_V) == 0; Branch(branchTaken); instruction_text = $"BVC ${(sbyte)ReadByte((ushort)(PC - 1)):+00;-00}"; cycleCount += branchTaken ? 3 : 2; break;
                case 0x70: branchTaken = (Status & FLAG_V) != 0; Branch(branchTaken); instruction_text = $"BVS ${(sbyte)ReadByte((ushort)(PC - 1)):+00;-00}"; cycleCount += branchTaken ? 3 : 2; break;
                case 0x90: branchTaken = (Status & FLAG_C) == 0; Branch(branchTaken); instruction_text = $"BCC ${(sbyte)ReadByte((ushort)(PC - 1)):+00;-00}"; cycleCount += branchTaken ? 3 : 2; break;
                case 0xB0: branchTaken = (Status & FLAG_C) != 0; Branch(branchTaken); instruction_text = $"BCS ${(sbyte)ReadByte((ushort)(PC - 1)):+00;-00}"; cycleCount += branchTaken ? 3 : 2; break;
                case 0xD0: branchTaken = (Status & FLAG_Z) == 0; Branch(branchTaken); instruction_text = $"BNE ${(sbyte)ReadByte((ushort)(PC - 1)):+00;-00}"; cycleCount += branchTaken ? 3 : 2; break;
                case 0xF0: branchTaken = (Status & FLAG_Z) != 0; Branch(branchTaken); instruction_text = $"BEQ ${(sbyte)ReadByte((ushort)(PC - 1)):+00;-00}"; cycleCount += branchTaken ? 3 : 2; break;

                // *** JAM/KIL (ILLEGAL) ***
                // Opcode che bloccano il processore
                case 0x02: instruction_text = "JAM"; cycleCount += 2; break;
                case 0x12: instruction_text = "JAM"; cycleCount += 2; break;
                case 0x22: instruction_text = "JAM"; cycleCount += 2; break;
                case 0x32: instruction_text = "JAM"; cycleCount += 2; break;
                case 0x42: instruction_text = "JAM"; cycleCount += 2; break;
                case 0x52: instruction_text = "JAM"; cycleCount += 2; break;
                case 0x62: instruction_text = "JAM"; cycleCount += 2; break;
                case 0x72: instruction_text = "JAM"; cycleCount += 2; break;
                case 0x92: instruction_text = "JAM"; cycleCount += 2; break;
                case 0xB2: instruction_text = "JAM"; cycleCount += 2; break;
                case 0xD2: instruction_text = "JAM"; cycleCount += 2; break;
                case 0xF2: instruction_text = "JAM"; cycleCount += 2; break;

                default:
                    // Istruzione sconosciuta
                    instruction_text = $"??? (${opcode:X2})";
                    throw new UnknownOpcodeException(opcode, (ushort)((ushort)(PC - 1)));
            }

            if (loggingEnabled)
            {
                Console.WriteLine($"[{instruction_PC:X4}] {opcode:x2} {instruction_text} | A={A:X2}, X={X:X2}, Y={Y:X2}, SP={SP:X2}, PC={PC:X4}, Status={Status:X2} | Cycles: {cycleCount}");
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