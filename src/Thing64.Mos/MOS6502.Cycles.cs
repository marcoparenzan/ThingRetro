namespace Thing64.Mos;

// Cycle-stepped engine: every Tick is one φ2 cycle with exactly one bus access, in the same order as the
// chip (dummy reads included, double write in read-modify-write instructions).
// Reference: "6502 Instruction Timing" (64doc, John West and Marko Mäkelä)
public partial class MOS6502
{
    int cycle;                   // cycle within the instruction; 0 = next opcode fetch
    Instruction instruction;     // instruction being executed
    int accessCycle;             // cycle where the operand access starts (0 = not known yet)
    ushort address;              // effective address
    ushort baseAddress;          // address before indexing (abs,X / abs,Y / (zp),Y)
    byte pointer;                // zero-page pointer (indirect modes)
    byte data;                   // data read, waiting to be written back
    bool pageCrossed;
    bool interruptPending;       // decided at the start of the last cycle of the previous instruction
    bool inInterrupt;            // IRQ/NMI sequence running instead of an instruction
    bool jammed;                 // JAM opcode: the CPU halts until reset
    bool nmiPending;             // /NMI triggers on the falling edge and stays pending until served
    bool nmiWasLow;

    internal bool IsInstructionBoundary => cycle == 0;
    internal long InstructionCount { get; private set; }
    internal bool IsJammed => jammed;

    // One clock cycle. /NMI is always sampled. With AEC low the bus belongs to the VIC-II and the CPU is halted;
    // with RDY low the CPU stops at the first read cycle, while writes go on
    public void Tick()
    {
        SampleNmi();
        if (jammed) return;
        if (!AecInput()) return;
        if (!RdyInput() && !IsWriteCycle()) return;

        // Interrupts are polled at the start of the last cycle: that is why one more instruction runs after CLI,
        // while an IRQ already active during SEI is served
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

    // Cycle 0: opcode fetch, or start of the interrupt sequence (dummy read at PC)
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
                ReadByte(PC); // dummy read of the next byte
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

    // Cycles that compute the effective address; when it is ready they set accessCycle
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
                    ReadByte(address); // dummy read from the unindexed address
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
                throw new InvalidOperationException($"Mode {instruction.Mode} is not valid for {instruction.Op}");
        }
    }

    void StartAccess()
    {
        accessCycle = cycle + 1;
        Next();
    }

    // Adds the index to the low byte. Reads that do not cross a page access the right address at once;
    // otherwise (and always for writes and read-modify-write) there is first a dummy read from the address
    // whose high byte has not been fixed yet
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

    // Operand access: step 0 = first cycle after the address calculation
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
                    // SHA/SHX/SHY/TAS: when a page is crossed the high byte of the address becomes the value written
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
                    WriteByte(address, data); // the NMOS 6502 writes the original value back before the new one
                    data = ExecuteReadModifyWrite(instruction.Op, data);
                    Next();
                }
                else { WriteByte(address, data); Done(); }
                break;
        }
    }

    // Branches: 2 cycles if not taken, 3 if taken, 4 if the target is in another page
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
            ReadByte(PC); // dummy read from the wrong page
            PC = address;
            Done();
        }
    }

    // Stack access: SP points to the next free location in page 1
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

            case Op.JMP: // JMP ($xxxx): the high byte of the pointer is not incremented ($xxFF -> $xx00)
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

            case Op.JSR: // pushes the address of the last byte of JSR (return address - 1)
                if (cycle == 1) { operandLow = ReadByte(PC++); Next(); }
                else if (cycle == 2) { ReadStack(); Next(); }
                else if (cycle == 3) { Push((byte)(PC >> 8)); Next(); }
                else if (cycle == 4) { Push((byte)PC); Next(); }
                else { operandHigh = ReadByte(PC); PC = (ushort)(operandHigh << 8 | operandLow); Done(); }
                break;

            case Op.RTS: // resumes from the pushed address + 1
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

            case Op.BRK: // like an interrupt, with the padding byte skipped and B set in the pushed status
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

    // Cycles 2-6 shared by BRK, IRQ and NMI: push PC and status, then read the vector.
    // The vector is chosen at cycle 4: an NMI arriving before then "hijacks" a BRK or an IRQ too
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

    // Kind of the next cycle, without running it: with RDY low only reads stop the CPU
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
