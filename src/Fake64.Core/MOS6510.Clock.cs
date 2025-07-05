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

        if (nmiPending)
        {
            HandleNMI();
            nmiPending = false;
            cycleCount += 7; // NMI handling cycles
        }

        ushort addr;
        byte val;
        bool branchTaken;
        ushort instruction_PC = PC;
        string instruction_text = ""; // Variabile per la rappresentazione testuale dell'istruzione

        if (PC == 0xFD52)
        {
        }

        byte opcode = ReadByte(PC++);

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
            case 0x0C: addr = ReadWord(PC); instruction_text = $"NOP ${addr:X4}"; PC += 2; cycleCount += 4; break;
            case 0x0D: addr = ReadWord(PC); A = ORA(ReadByte(addr)); instruction_text = $"ORA ${addr:X4}"; PC += 2; cycleCount += 4; break;
            case 0x0E: addr = ReadWord(PC); val = ReadByte(addr); WriteByte(addr, ASL(val)); instruction_text = $"ASL ${addr:X4}"; PC += 2; cycleCount += 6; break;
            case 0x0F: addr = ReadWord(PC); SLO(addr); instruction_text = $"SLO ${addr:X4}"; PC += 2; cycleCount += 6; break;
            case 0x20: addr = ReadWord(PC); instruction_text = $"JSR ${addr:X4}"; JSR(addr); cycleCount += 6; break;
            case 0x2C: addr = ReadWord(PC); BIT(ReadByte(addr)); instruction_text = $"BIT ${addr:X4}"; PC += 2; cycleCount += 4; break;
            case 0x2D: addr = ReadWord(PC); A = AND(ReadByte(addr)); instruction_text = $"AND ${addr:X4}"; PC += 2; cycleCount += 4; break;
            case 0x2E: addr = ReadWord(PC); val = ReadByte(addr); WriteByte(addr, ROL(val)); instruction_text = $"ROL ${addr:X4}"; PC += 2; cycleCount += 6; break;
            case 0x2F: addr = ReadWord(PC); RLA(addr); instruction_text = $"RLA ${addr:X4}"; PC += 2; cycleCount += 6; break;
            case 0x4C: addr = ReadWord(PC); instruction_text = $"JMP ${addr:X4}"; PC = addr; cycleCount += 3; break;
            case 0x4D: addr = ReadWord(PC); A = EOR(ReadByte(addr)); instruction_text = $"EOR ${addr:X4}"; PC += 2; cycleCount += 4; break;
            case 0x4E: addr = ReadWord(PC); val = ReadByte(addr); WriteByte(addr, LSR(val)); instruction_text = $"LSR ${addr:X4}"; PC += 2; cycleCount += 6; break;
            case 0x4F: addr = ReadWord(PC); SRE(addr); instruction_text = $"SRE ${addr:X4}"; PC += 2; cycleCount += 6; break;
            case 0x6C: addr = ReadWord(PC); instruction_text = $"JMP (${addr:X4})"; PC = ReadWord(addr); cycleCount += 5; break;
            case 0x6D: addr = ReadWord(PC); A = ADC(ReadByte(addr)); instruction_text = $"ADC ${addr:X4}"; PC += 2; cycleCount += 4; break;
            case 0x6E: addr = ReadWord(PC); val = ReadByte(addr); WriteByte(addr, ROR(val)); instruction_text = $"ROR ${addr:X4}"; PC += 2; cycleCount += 6; break;
            case 0x6F: addr = ReadWord(PC); RRA(addr); instruction_text = $"RRA ${addr:X4}"; PC += 2; cycleCount += 6; break;
            case 0x8C: addr = ReadWord(PC); WriteByte(addr, Y); instruction_text = $"STY ${addr:X4}"; PC += 2; cycleCount += 4; break;
            case 0x8D: addr = ReadWord(PC); WriteByte(addr, A); instruction_text = $"STA ${addr:X4}"; PC += 2; cycleCount += 4; break;
            case 0x8E: addr = ReadWord(PC); WriteByte(addr, X); instruction_text = $"STX ${addr:X4}"; PC += 2; cycleCount += 4; break;
            case 0x8F: addr = ReadWord(PC); WriteByte(addr, (byte)(A & X)); instruction_text = $"SAX ${addr:X4}"; PC += 2; cycleCount += 4; break;
            case 0xAC: addr = ReadWord(PC); Y = LDY(ReadByte(addr)); instruction_text = $"LDY ${addr:X4}"; PC += 2; cycleCount += 4; break;
            case 0xAD: addr = ReadWord(PC); A = LDA(ReadByte(addr)); instruction_text = $"LDA ${addr:X4}"; PC += 2; cycleCount += 4; break;
            case 0xAE: addr = ReadWord(PC); X = LDX(ReadByte(addr)); instruction_text = $"LDX ${addr:X4}"; PC += 2; cycleCount += 4; break;
            case 0xAF: addr = ReadWord(PC); A = X = LAX(ReadByte(addr)); instruction_text = $"LAX ${addr:X4}"; PC += 2; cycleCount += 4; break;
            case 0xCC: addr = ReadWord(PC); Compare(Y, ReadByte(addr)); instruction_text = $"CPY ${addr:X4}"; PC += 2; cycleCount += 4; break;
            case 0xCD: addr = ReadWord(PC); Compare(A, ReadByte(addr)); instruction_text = $"CMP ${addr:X4}"; PC += 2; cycleCount += 4; break;
            case 0xCE: addr = ReadWord(PC); val = ReadByte(addr); WriteByte(addr, DEC(val)); instruction_text = $"DEC ${addr:X4}"; PC += 2; cycleCount += 6; break;
            case 0xCF: addr = ReadWord(PC); DCP(addr); instruction_text = $"DCP ${addr:X4}"; PC += 2; cycleCount += 6; break;
            case 0xEC: addr = ReadWord(PC); Compare(X, ReadByte(addr)); instruction_text = $"CPX ${addr:X4}"; PC += 2; cycleCount += 4; break;
            case 0xED: addr = ReadWord(PC); A = SBC(ReadByte(addr)); instruction_text = $"SBC ${addr:X4}"; PC += 2; cycleCount += 4; break;
            case 0xEE: addr = ReadWord(PC); val = ReadByte(addr); WriteByte(addr, INC(val)); instruction_text = $"INC ${addr:X4}"; PC += 2; cycleCount += 6; break;
            case 0xEF: addr = ReadWord(PC); ISC(addr); instruction_text = $"ISC ${addr:X4}"; PC += 2; cycleCount += 6; break;

            // *** ABSOLUTE INDEXED (X) ***
            // Istruzioni che usano indirizzamento assoluto indicizzato con X
            case 0x1C: addr = ReadWord(PC); instruction_text = $"NOP ${addr:X4},X"; PC += 2; cycleCount += 4; break;
            case 0x1D: addr = ReadWord(PC); ushort opAddr1D = addr; addr = (ushort)(addr + X); A = ORA(ReadByte(addr)); instruction_text = $"ORA ${opAddr1D:X4},X"; PC += 2; cycleCount += 4; break;
            case 0x1E: addr = ReadWord(PC); ushort opAddr1E = addr; addr = (ushort)(addr + X); val = ReadByte(addr); WriteByte(addr, ASL(val)); instruction_text = $"ASL ${opAddr1E:X4},X"; PC += 2; cycleCount += 7; break;
            case 0x1F: addr = ReadWord(PC); ushort opAddr1F = addr; addr = (ushort)(addr + X); SLO(addr); instruction_text = $"SLO ${opAddr1F:X4},X"; PC += 2; cycleCount += 7; break;
            case 0x3C: addr = ReadWord(PC); instruction_text = $"NOP ${addr:X4},X"; PC += 2; cycleCount += 4; break;
            case 0x3D: addr = ReadWord(PC); ushort opAddr3D = addr; addr = (ushort)(addr + X); A = AND(ReadByte(addr)); instruction_text = $"AND ${opAddr3D:X4},X"; PC += 2; cycleCount += 4; break;
            case 0x3E: addr = ReadWord(PC); ushort opAddr3E = addr; addr = (ushort)(addr + X); val = ReadByte(addr); WriteByte(addr, ROL(val)); instruction_text = $"ROL ${opAddr3E:X4},X"; PC += 2; cycleCount += 7; break;
            case 0x3F: addr = ReadWord(PC); ushort opAddr3F = addr; addr = (ushort)(addr + X); RLA(addr); instruction_text = $"RLA ${opAddr3F:X4},X"; PC += 2; cycleCount += 7; break;
            case 0x5C: addr = ReadWord(PC); instruction_text = $"NOP ${addr:X4},X"; PC += 2; cycleCount += 4; break;
            case 0x5D: addr = ReadWord(PC); ushort opAddr5D = addr; addr = (ushort)(addr + X); A = EOR(ReadByte(addr)); instruction_text = $"EOR ${opAddr5D:X4},X"; PC += 2; cycleCount += 4; break;
            case 0x5E: addr = ReadWord(PC); ushort opAddr5E = addr; addr = (ushort)(addr + X); val = ReadByte(addr); WriteByte(addr, LSR(val)); instruction_text = $"LSR ${opAddr5E:X4},X"; PC += 2; cycleCount += 7; break;
            case 0x5F: addr = ReadWord(PC); ushort opAddr5F = addr; addr = (ushort)(addr + X); SRE(addr); instruction_text = $"SRE ${opAddr5F:X4},X"; PC += 2; cycleCount += 7; break;
            case 0x7C: addr = ReadWord(PC); instruction_text = $"NOP ${addr:X4},X"; PC += 2; cycleCount += 4; break;
            case 0x7D: addr = ReadWord(PC); ushort opAddr7D = addr; addr = (ushort)(addr + X); A = ADC(ReadByte(addr)); instruction_text = $"ADC ${opAddr7D:X4},X"; PC += 2; cycleCount += 4; break;
            case 0x7E: addr = ReadWord(PC); ushort opAddr7E = addr; addr = (ushort)(addr + X); val = ReadByte(addr); WriteByte(addr, ROR(val)); instruction_text = $"ROR ${opAddr7E:X4},X"; PC += 2; cycleCount += 7; break;
            case 0x7F: addr = ReadWord(PC); ushort opAddr7F = addr; addr = (ushort)(addr + X); RRA(addr); instruction_text = $"RRA ${opAddr7F:X4},X"; PC += 2; cycleCount += 7; break;
            case 0x9C: addr = ReadWord(PC); ushort opAddr9C = addr; addr = (ushort)(addr + X); WriteByte(addr, (byte)(Y & ((addr >> 8) + 1))); instruction_text = $"SHY ${opAddr9C:X4},X"; PC += 2; cycleCount += 5; break;
            case 0x9D: addr = ReadWord(PC); ushort opAddr9D = addr; addr = (ushort)(addr + X); WriteByte(addr, A); instruction_text = $"STA ${opAddr9D:X4},X"; PC += 2; cycleCount += 5; break;
            case 0xBC: addr = ReadWord(PC); ushort opAddrBC = addr; addr = (ushort)(addr + X); Y = LDY(ReadByte(addr)); instruction_text = $"LDY ${opAddrBC:X4},X"; PC += 2; cycleCount += 4; break;
            case 0xBD: addr = ReadWord(PC); ushort opAddrBD = addr; addr = (ushort)(addr + X); A = LDA(ReadByte(addr)); instruction_text = $"LDA ${opAddrBD:X4},X"; PC += 2; cycleCount += 4; break;
            case 0xDC: addr = ReadWord(PC); instruction_text = $"NOP ${addr:X4},X"; PC += 2; cycleCount += 4; break;
            case 0xDD: addr = ReadWord(PC); ushort opAddrDD = addr; addr = (ushort)(addr + X); Compare(A, ReadByte(addr)); instruction_text = $"CMP ${opAddrDD:X4},X"; PC += 2; cycleCount += 4; break;
            case 0xDE: addr = ReadWord(PC); ushort opAddrDE = addr; addr = (ushort)(addr + X); val = ReadByte(addr); WriteByte(addr, DEC(val)); instruction_text = $"DEC ${opAddrDE:X4},X"; PC += 2; cycleCount += 7; break;
            case 0xDF: addr = ReadWord(PC); ushort opAddrDF = addr; addr = (ushort)(addr + X); DCP(addr); instruction_text = $"DCP ${opAddrDF:X4},X"; PC += 2; cycleCount += 7; break;
            case 0xFC: addr = ReadWord(PC); instruction_text = $"NOP ${addr:X4},X"; PC += 2; cycleCount += 4; break;
            case 0xFD: addr = ReadWord(PC); ushort opAddrFD = addr; addr = (ushort)(addr + X); A = SBC(ReadByte(addr)); instruction_text = $"SBC ${opAddrFD:X4},X"; PC += 2; cycleCount += 4; break;
            case 0xFE: addr = ReadWord(PC); ushort opAddrFE = addr; addr = (ushort)(addr + X); val = ReadByte(addr); WriteByte(addr, INC(val)); instruction_text = $"INC ${opAddrFE:X4},X"; PC += 2; cycleCount += 7; break;
            case 0xFF: addr = ReadWord(PC); ushort opAddrFF = addr; addr = (ushort)(addr + X); ISC(addr); instruction_text = $"ISC ${opAddrFF:X4},X"; PC += 2; cycleCount += 7; break;

            // *** ABSOLUTE INDEXED (Y) ***
            // Istruzioni che usano indirizzamento assoluto indicizzato con Y
            case 0x19: addr = ReadWord(PC); ushort opAddr19 = addr; addr = (ushort)(addr + Y); A = ORA(ReadByte(addr)); instruction_text = $"ORA ${opAddr19:X4},Y"; PC += 2; cycleCount += 4; break;
            case 0x1B: addr = ReadWord(PC); ushort opAddr1B = addr; addr = (ushort)(addr + Y); SLO(addr); instruction_text = $"SLO ${opAddr1B:X4},Y"; PC += 2; cycleCount += 7; break;
            case 0x39: addr = ReadWord(PC); ushort opAddr39 = addr; addr = (ushort)(addr + Y); A = AND(ReadByte(addr)); instruction_text = $"AND ${opAddr39:X4},Y"; PC += 2; cycleCount += 4; break;
            case 0x3B: addr = ReadWord(PC); ushort opAddr3B = addr; addr = (ushort)(addr + Y); RLA(addr); instruction_text = $"RLA ${opAddr3B:X4},Y"; PC += 2; cycleCount += 7; break;
            case 0x59: addr = ReadWord(PC); ushort opAddr59 = addr; addr = (ushort)(addr + Y); A = EOR(ReadByte(addr)); instruction_text = $"EOR ${opAddr59:X4},Y"; PC += 2; cycleCount += 4; break;
            case 0x5B: addr = ReadWord(PC); ushort opAddr5B = addr; addr = (ushort)(addr + Y); SRE(addr); instruction_text = $"SRE ${opAddr5B:X4},Y"; PC += 2; cycleCount += 7; break;
            case 0x79: addr = ReadWord(PC); ushort opAddr79 = addr; addr = (ushort)(addr + Y); A = ADC(ReadByte(addr)); instruction_text = $"ADC ${opAddr79:X4},Y"; PC += 2; cycleCount += 4; break;
            case 0x7B: addr = ReadWord(PC); ushort opAddr7B = addr; addr = (ushort)(addr + Y); RRA(addr); instruction_text = $"RRA ${opAddr7B:X4},Y"; PC += 2; cycleCount += 7; break;
            case 0x99: addr = ReadWord(PC); ushort opAddr99 = addr; addr = (ushort)(addr + Y); WriteByte(addr, A); instruction_text = $"STA ${opAddr99:X4},Y"; PC += 2; cycleCount += 5; break;
            case 0x9B: addr = ReadWord(PC); ushort opAddr9B = addr; addr = (ushort)(addr + Y); SP = (byte)(A & X); WriteByte(addr, (byte)(SP & ((addr >> 8) + 1))); instruction_text = $"TAS ${opAddr9B:X4},Y"; PC += 2; cycleCount += 5; break;
            case 0x9E: addr = ReadWord(PC); ushort opAddr9E = addr; addr = (ushort)(addr + Y); WriteByte(addr, (byte)(X & ((addr >> 8) + 1))); instruction_text = $"SHX ${opAddr9E:X4},Y"; PC += 2; cycleCount += 5; break;
            case 0x9F: addr = ReadWord(PC); ushort opAddr9F = addr; addr = (ushort)(addr + Y); WriteByte(addr, (byte)(A & X & ((addr >> 8) + 1))); instruction_text = $"AHX ${opAddr9F:X4},Y"; PC += 2; cycleCount += 5; break;
            case 0xB9: addr = ReadWord(PC); ushort opAddrB9 = addr; addr = (ushort)(addr + Y); A = LDA(ReadByte(addr)); instruction_text = $"LDA ${opAddrB9:X4},Y"; PC += 2; cycleCount += 4; break;
            case 0xBB: addr = ReadWord(PC); ushort opAddrBB = addr; addr = (ushort)(addr + Y); A = X = SP = (byte)(ReadByte(addr) & SP); SetZN(A); instruction_text = $"LAS ${opAddrBB:X4},Y"; PC += 2; cycleCount += 4; break;
            case 0xBE: addr = ReadWord(PC); ushort opAddrBE = addr; addr = (ushort)(addr + Y); X = LDX(ReadByte(addr)); instruction_text = $"LDX ${opAddrBE:X4},Y"; PC += 2; cycleCount += 4; break;
            case 0xBF: addr = ReadWord(PC); ushort opAddrBF = addr; addr = (ushort)(addr + Y); A = X = LAX(ReadByte(addr)); instruction_text = $"LAX ${opAddrBF:X4},Y"; PC += 2; cycleCount += 4; break;
            case 0xD9: addr = ReadWord(PC); ushort opAddrD9 = addr; addr = (ushort)(addr + Y); Compare(A, ReadByte(addr)); instruction_text = $"CMP ${opAddrD9:X4},Y"; PC += 2; cycleCount += 4; break;
            case 0xDB: addr = ReadWord(PC); ushort opAddrDB = addr; addr = (ushort)(addr + Y); DCP(addr); instruction_text = $"DCP ${opAddrDB:X4},Y"; PC += 2; cycleCount += 7; break;
            case 0xF9: addr = ReadWord(PC); ushort opAddrF9 = addr; addr = (ushort)(addr + Y); A = SBC(ReadByte(addr)); instruction_text = $"SBC ${opAddrF9:X4},Y"; PC += 2; cycleCount += 4; break;
            case 0xFB: addr = ReadWord(PC); ushort opAddrFB = addr; addr = (ushort)(addr + Y); ISC(addr); instruction_text = $"ISC ${opAddrFB:X4},Y"; PC += 2; cycleCount += 7; break;
                
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
                // 
                //
                // do something!
                break;
        }

        
        LogInstructionText(instruction_PC, opcode, instruction_text);
    }
}