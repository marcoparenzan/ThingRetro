namespace Fake64.Mos;

// Motore ciclo per ciclo: ogni Tick è un ciclo di φ2 con esattamente un accesso al bus, nello stesso
// ordine del chip (letture fittizie comprese, doppia scrittura nelle lettura-modifica-scrittura).
// Riferimento: "6502 Instruction Timing" (64doc, John West e Marko Mäkelä)
public partial class MOS6502
{
    int cycle;                   // ciclo all'interno dell'istruzione; 0 = prossimo prelievo di opcode
    Instruction instruction;     // istruzione in corso
    int accessCycle;             // ciclo in cui inizia l'accesso all'operando (0 = non ancora noto)
    ushort address;              // indirizzo effettivo
    ushort baseAddress;          // indirizzo prima dell'indice (abs,X / abs,Y / (zp),Y)
    byte pointer;                // puntatore in pagina zero (indiretti)
    byte data;                   // dato letto, in attesa di essere riscritto
    bool pageCrossed;
    bool interruptPending;       // deciso all'inizio dell'ultimo ciclo dell'istruzione precedente
    bool inInterrupt;            // sequenza IRQ/NMI in corso al posto di un'istruzione
    bool jammed;                 // opcode JAM: la CPU si blocca fino al reset
    bool nmiPending;             // /NMI scatta sul fronte di discesa e resta in sospeso finché non viene servito
    bool nmiWasLow;

    internal bool IsInstructionBoundary => cycle == 0;
    internal long InstructionCount { get; private set; }
    internal bool IsJammed => jammed;

    // Un ciclo di clock. /NMI viene campionato sempre. Con AEC basso il bus è del VIC-II e la CPU è ferma;
    // con RDY basso la CPU si ferma al primo ciclo di lettura, mentre le scritture proseguono
    public void Tick()
    {
        SampleNmi();
        if (jammed) return;
        if (!AecInput()) return;
        if (!RdyInput() && !IsWriteCycle()) return;

        // Gli interrupt si controllano all'inizio dell'ultimo ciclo: per questo dopo CLI viene eseguita
        // ancora un'istruzione, mentre un IRQ già attivo durante SEI viene servito
        bool interruptRequested = nmiPending || (IrqInput() && (Status & FLAG_I) == 0);

        if (cycle == 0) Fetch();
        else if (inInterrupt) InterruptCycle();
        else Execute();

        if (cycle == 0) interruptPending = interruptRequested;
    }

    void SampleNmi()
    {
        bool nmiLow = NmiInput();
        if (nmiLow && !nmiWasLow) nmiPending = true;
        nmiWasLow = nmiLow;
    }

    void Next() => cycle++;
    void Done() => cycle = 0;

    // Ciclo 0: prelievo dell'opcode, oppure inizio della sequenza di interrupt (lettura fittizia del PC)
    void Fetch()
    {
        accessCycle = 0;
        pageCrossed = false;
        if (interruptPending)
        {
            interruptPending = false;
            inInterrupt = true;
            ReadByte(PC);
            cycle = 1;
            return;
        }

        instructionPC = PC;
        byte opcode = ReadByte(PC++);
        instruction = Instructions[opcode];
        lastOpcode = opcode;
        InstructionCount++;
        cycle = 1;
    }

    void Execute()
    {
        switch (instruction.Kind)
        {
            case Kind.Implied:
                ReadByte(PC); // lettura fittizia del byte successivo
                ExecuteImplied(instruction.Op);
                Done();
                break;
            case Kind.Branch:
                BranchCycle();
                break;
            case Kind.Special:
                SpecialCycle();
                break;
            default:
                if (accessCycle != 0 && cycle >= accessCycle) AccessCycle(cycle - accessCycle);
                else AddressCycle();
                break;
        }
    }

    // Cicli che calcolano l'indirizzo effettivo; quando è pronto fissano accessCycle
    void AddressCycle()
    {
        switch (instruction.Mode)
        {
            case Mode.Imm:
                ExecuteRead(instruction.Op, operandLow = ReadByte(PC++));
                Done();
                break;

            case Mode.Zp:
                address = operandLow = ReadByte(PC++);
                StartAccess();
                break;

            case Mode.ZpX:
            case Mode.ZpY:
                if (cycle == 1) { address = operandLow = ReadByte(PC++); Next(); }
                else
                {
                    ReadByte(address); // lettura fittizia dall'indirizzo non indicizzato
                    address = (byte)(address + (instruction.Mode == Mode.ZpX ? X : Y));
                    StartAccess();
                }
                break;

            case Mode.Abs:
                if (cycle == 1) { address = operandLow = ReadByte(PC++); Next(); }
                else { operandHigh = ReadByte(PC++); address |= (ushort)(operandHigh << 8); StartAccess(); }
                break;

            case Mode.AbsX:
            case Mode.AbsY:
                if (cycle == 1) { baseAddress = operandLow = ReadByte(PC++); Next(); }
                else if (cycle == 2)
                {
                    operandHigh = ReadByte(PC++);
                    baseAddress |= (ushort)(operandHigh << 8);
                    Index(instruction.Mode == Mode.AbsX ? X : Y);
                }
                else FixPage();
                break;

            case Mode.IndX:
                if (cycle == 1) { pointer = operandLow = ReadByte(PC++); Next(); }
                else if (cycle == 2) { ReadByte(pointer); pointer += X; Next(); }
                else if (cycle == 3) { address = ReadByte(pointer); Next(); }
                else { address |= (ushort)(ReadByte((byte)(pointer + 1)) << 8); StartAccess(); }
                break;

            case Mode.IndY:
                if (cycle == 1) { pointer = operandLow = ReadByte(PC++); Next(); }
                else if (cycle == 2) { baseAddress = ReadByte(pointer); Next(); }
                else if (cycle == 3)
                {
                    baseAddress |= (ushort)(ReadByte((byte)(pointer + 1)) << 8);
                    Index(Y);
                }
                else FixPage();
                break;

            default:
                throw new InvalidOperationException($"Modo {instruction.Mode} non valido per {instruction.Op}");
        }
    }

    void StartAccess()
    {
        accessCycle = cycle + 1;
        Next();
    }

    // Somma l'indice al byte basso. Le letture senza cambio di pagina accedono subito all'indirizzo giusto;
    // altrimenti (e sempre per scritture e lettura-modifica-scrittura) c'è prima una lettura fittizia
    // dall'indirizzo con il byte alto non ancora corretto
    void Index(byte index)
    {
        address = (ushort)(baseAddress + index);
        pageCrossed = (address & 0xFF00) != (baseAddress & 0xFF00);
        if (instruction.Kind == Kind.Read && !pageCrossed) StartAccess();
        else Next();
    }

    void FixPage()
    {
        ReadByte((ushort)((baseAddress & 0xFF00) | (address & 0x00FF)));
        StartAccess();
    }

    // Accesso all'operando: step 0 = primo ciclo dopo il calcolo dell'indirizzo
    void AccessCycle(int step)
    {
        switch (instruction.Kind)
        {
            case Kind.Read:
                ExecuteRead(instruction.Op, ReadByte(address));
                Done();
                break;

            case Kind.Write:
                {
                    byte value = WriteValue(instruction.Op);
                    // SHA/SHX/SHY/TAS: con cambio di pagina il byte alto dell'indirizzo diventa il valore scritto
                    if (pageCrossed && instruction.Op is Op.SHA or Op.SHX or Op.SHY or Op.TAS)
                        address = (ushort)((value << 8) | (address & 0x00FF));
                    WriteByte(address, value);
                    Done();
                    break;
                }

            case Kind.ReadModifyWrite:
                if (step == 0) { data = ReadByte(address); Next(); }
                else if (step == 1)
                {
                    WriteByte(address, data); // il 6502 NMOS riscrive il valore originale prima di quello nuovo
                    data = ExecuteReadModifyWrite(instruction.Op, data);
                    Next();
                }
                else { WriteByte(address, data); Done(); }
                break;
        }
    }

    // Salti condizionati: 2 cicli se non presi, 3 se presi, 4 se la destinazione è in un'altra pagina
    void BranchCycle()
    {
        if (cycle == 1)
        {
            operandLow = ReadByte(PC++);
            if (BranchTaken(instruction.Op)) Next();
            else Done();
        }
        else if (cycle == 2)
        {
            ReadByte(PC);
            address = (ushort)(PC + (sbyte)operandLow);
            if ((address & 0xFF00) == (PC & 0xFF00)) { PC = address; Done(); }
            else { PC = (ushort)((PC & 0xFF00) | (address & 0x00FF)); Next(); }
        }
        else
        {
            ReadByte(PC); // lettura fittizia dalla pagina sbagliata
            PC = address;
            Done();
        }
    }

    // Accesso allo stack: il puntatore SP indica la prossima posizione libera in pagina 1
    void Push(byte value) => WriteByte((ushort)(0x0100 + SP--), value);
    byte Pop() => ReadByte((ushort)(0x0100 + ++SP));
    void ReadStack() => ReadByte((ushort)(0x0100 + SP));

    void SpecialCycle()
    {
        switch (instruction.Op)
        {
            case Op.JMP when instruction.Mode == Mode.Abs:
                if (cycle == 1) { operandLow = ReadByte(PC++); Next(); }
                else { operandHigh = ReadByte(PC); PC = (ushort)(operandHigh << 8 | operandLow); Done(); }
                break;

            case Op.JMP: // JMP ($xxxx): il byte alto del puntatore non viene incrementato ($xxFF -> $xx00)
                if (cycle == 1) { operandLow = ReadByte(PC++); Next(); }
                else if (cycle == 2) { operandHigh = ReadByte(PC++); Next(); }
                else if (cycle == 3) { data = ReadByte((ushort)(operandHigh << 8 | operandLow)); Next(); }
                else
                {
                    byte high = ReadByte((ushort)(operandHigh << 8 | (byte)(operandLow + 1)));
                    PC = (ushort)(high << 8 | data);
                    Done();
                }
                break;

            case Op.JSR: // salva l'indirizzo dell'ultimo byte di JSR (indirizzo di ritorno - 1)
                if (cycle == 1) { operandLow = ReadByte(PC++); Next(); }
                else if (cycle == 2) { ReadStack(); Next(); }
                else if (cycle == 3) { Push((byte)(PC >> 8)); Next(); }
                else if (cycle == 4) { Push((byte)PC); Next(); }
                else { operandHigh = ReadByte(PC); PC = (ushort)(operandHigh << 8 | operandLow); Done(); }
                break;

            case Op.RTS: // riprende dall'indirizzo salvato + 1
                if (cycle == 1) { ReadByte(PC); Next(); }
                else if (cycle == 2) { ReadStack(); Next(); }
                else if (cycle == 3) { PC = Pop(); Next(); }
                else if (cycle == 4) { PC |= (ushort)(Pop() << 8); Next(); }
                else { ReadByte(PC++); Done(); }
                break;

            case Op.RTI:
                if (cycle == 1) { ReadByte(PC); Next(); }
                else if (cycle == 2) { ReadStack(); Next(); }
                else if (cycle == 3) { Status = (byte)((Pop() & ~FLAG_B) | FLAG_U); Next(); }
                else if (cycle == 4) { PC = Pop(); Next(); }
                else { PC |= (ushort)(Pop() << 8); Done(); }
                break;

            case Op.PHA:
            case Op.PHP:
                if (cycle == 1) { ReadByte(PC); Next(); }
                else
                {
                    Push(instruction.Op == Op.PHA ? A : (byte)(Status | FLAG_B | FLAG_U));
                    Done();
                }
                break;

            case Op.PLA:
            case Op.PLP:
                if (cycle == 1) { ReadByte(PC); Next(); }
                else if (cycle == 2) { ReadStack(); Next(); }
                else
                {
                    byte value = Pop();
                    if (instruction.Op == Op.PLA) { A = value; SetZN(A); }
                    else Status = (byte)((value & ~FLAG_B) | FLAG_U);
                    Done();
                }
                break;

            case Op.BRK: // come un interrupt, con il byte di padding saltato e B a 1 nello stato salvato
                if (cycle == 1) { ReadByte(PC++); Next(); }
                else InterruptSequence(brk: true);
                break;

            case Op.JAM:
                jammed = true;
                break;
        }
    }

    void InterruptCycle()
    {
        if (cycle == 1) { ReadByte(PC); Next(); }
        else InterruptSequence(brk: false);
    }

    // Cicli 2-6 comuni a BRK, IRQ e NMI: salva PC e stato, poi legge il vettore.
    // Il vettore si sceglie al ciclo 4: un NMI arrivato prima "dirotta" anche un BRK o un IRQ
    bool vectorIsNmi;

    void InterruptSequence(bool brk)
    {
        switch (cycle)
        {
            case 2: Push((byte)(PC >> 8)); Next(); break;
            case 3: Push((byte)PC); Next(); break;
            case 4:
                vectorIsNmi = nmiPending;
                if (nmiPending) nmiPending = false;
                Push((byte)((brk ? Status | FLAG_B : Status & ~FLAG_B) | FLAG_U));
                Next();
                break;
            case 5:
                data = ReadByte(vectorIsNmi ? (ushort)0xFFFA : (ushort)0xFFFE);
                Status |= FLAG_I;
                Next();
                break;
            default:
                PC = (ushort)(ReadByte(vectorIsNmi ? (ushort)0xFFFB : (ushort)0xFFFF) << 8 | data);
                inInterrupt = false;
                Done();
                break;
        }
    }

    // Tipo del prossimo ciclo, senza eseguirlo: con RDY basso solo le letture fermano la CPU
    bool IsWriteCycle()
    {
        if (cycle == 0) return false;
        if (inInterrupt || (instruction.Kind == Kind.Special && instruction.Op == Op.BRK))
            return cycle is >= 2 and <= 4;
        if (instruction.Kind == Kind.Special)
            return instruction.Op switch
            {
                Op.JSR => cycle is 3 or 4,
                Op.PHA or Op.PHP => cycle == 2,
                _ => false,
            };
        if (accessCycle == 0 || cycle < accessCycle) return false;
        int step = cycle - accessCycle;
        return instruction.Kind switch
        {
            Kind.Write => true,
            Kind.ReadModifyWrite => step >= 1,
            _ => false,
        };
    }
}
