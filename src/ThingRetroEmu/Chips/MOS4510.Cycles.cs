namespace ThingRetroEmu.Chips;

// Cycle-stepped engine: every Tick is one φ2 cycle, and the instruction takes the number of cycles the manual
// gives (implied instructions take one, a read of base page three, absolute four, indirect five...). The
// cycles in which the real chip only computes (indexing, internal work) do not touch the bus here; every other
// cycle has exactly one access. The 65CE02 has no dummy reads and no page crossing penalty.
// Reference: "C65 Specifications", chapter 2.3.7.5 (cycles of each instruction)
public partial class MOS4510
{
    int cycle;                   // cycle within the instruction; 0 = next opcode fetch
    byte opcode;
    Instruction instruction;     // instruction being executed
    bool addressReady;           // the effective address has been calculated
    int step;                    // step of the data phase, after the address
    ushort address;              // effective address
    byte pointer;                // base page pointer (indirect modes)
    byte operandLow, operandHigh;
    byte data, dataHigh;         // data read, waiting to be written back
    bool interruptPending;       // decided when the previous instruction ended
    bool inInterrupt;            // IRQ/NMI sequence running instead of an instruction
    bool nmiPending;             // /NMI triggers on the falling edge and stays pending until served
    bool nmiWasLow;
    int length;                  // cycles of the instruction being executed (to know if it was a one-cycle one)

    internal bool IsInstructionBoundary => cycle == 0;
    internal long InstructionCount { get; private set; }

    // One clock cycle. /NMI is always sampled. With AEC or /DMA low the bus belongs to someone else and the CPU waits
    public void Tick()
    {
        SampleNmi();
        if (!AecInput() || !DmaInput()) return;

        length++;
        if (cycle == 0) Fetch();
        else if (inInterrupt) InterruptCycle();
        else Execute();

        // Interrupts are polled when an instruction ends, but not after a one-cycle one, and not between MAP and EOM
        if (cycle == 0)
        {
            interruptPending = length > 1 && !mapInhibit &&
                               (nmiPending || (IrqInput() && (Status & FLAG_I) == 0));
            length = 0;
        }
    }

    void SampleNmi()
    {
        bool nmiLow = NmiInput();
        if (nmiLow && !nmiWasLow) nmiPending = true;
        nmiWasLow = nmiLow;
    }

    void Next() => cycle++;
    void Done() => cycle = 0;

    // Cycle 0: opcode fetch (the whole instruction when it takes a single cycle), or start of the interrupt sequence
    void Fetch()
    {
        addressReady = false;
        step = 0;
        if (interruptPending)
        {
            interruptPending = false;
            inInterrupt = true;
            cycle = 1; // this cycle does nothing on the bus
            return;
        }

        opcode = ReadByte(PC++);
        instruction = Instructions[opcode];
        InstructionCount++;
        instructionPC = (ushort)(PC - 1);

        if ((instruction.Mode is Mode.Imp or Mode.Acc) && instruction.Op is not (Op.BRK or Op.RTI or Op.RTS or
            Op.PHA or Op.PHP or Op.PHX or Op.PHY or Op.PHZ or Op.PLA or Op.PLP or Op.PLX or Op.PLY or Op.PLZ))
        {
            // Implied: one cycle, or one more per internal cycle listed in the table
            if (ImpliedCycles(instruction.Op, instruction.Mode) == 1) ExecuteImplied(instruction.Op);
            else cycle = 1;
            return;
        }
        cycle = 1;
    }

    void Execute()
    {
        switch (instruction.Op)
        {
            case Op.BRK: InterruptSequence(brk: true); return;
            case Op.RTI: ReturnFromInterrupt(); return;
            case Op.RTS: case Op.RTN: ReturnFromSubroutine(); return;
            case Op.JSR: case Op.BSR: Call(); return;
            case Op.JMP: Jump(); return;
            case Op.PHA: case Op.PHP: case Op.PHX: case Op.PHY: case Op.PHZ: PushRegister(); return;
            case Op.PLA: case Op.PLP: case Op.PLX: case Op.PLY: case Op.PLZ: PullRegister(); return;
            case Op.BBR: case Op.BBS: BranchOnBit(); return;
        }

        switch (instruction.Mode)
        {
            case Mode.Imp:
            case Mode.Acc:
                // Implied with internal cycles: the operation is done in the last one
                if (cycle >= ImpliedCycles(instruction.Op, instruction.Mode) - 1) { ExecuteImplied(instruction.Op); Done(); }
                else Next();
                return;

            case Mode.Imm:
                ExecuteRead(instruction.Op, operandLow = ReadByte(PC++));
                Done();
                return;

            case Mode.Rel:
            case Mode.Rel16:
                BranchCycle();
                return;

            case Mode.Imm16: // PHW #imm16
                if (cycle == 1) { operandLow = ReadByte(PC++); Next(); }
                else if (cycle == 2) { operandHigh = ReadByte(PC++); Next(); }
                else if (cycle == 3) { Push(operandLow); Next(); }
                else { Push(operandHigh); Done(); }
                return;
        }

        if (!addressReady)
        {
            addressReady = AddressCycle();
            Next();
            return;
        }
        DataCycle();
    }

    // Cycles that compute the effective address. Returns true when it is ready (the cycle was the last of them)
    bool AddressCycle()
    {
        switch (instruction.Mode)
        {
            case Mode.Bp:
                address = BasePage(operandLow = ReadByte(PC++));
                return true;

            case Mode.BpX:
                address = BasePage((byte)((operandLow = ReadByte(PC++)) + X));
                return true;

            case Mode.BpY:
                address = BasePage((byte)((operandLow = ReadByte(PC++)) + Y));
                return true;

            case Mode.Abs:
            case Mode.AbsX:
            case Mode.AbsY:
                if (cycle == 1) { operandLow = ReadByte(PC++); return false; }
                operandHigh = ReadByte(PC++);
                address = (ushort)(operandHigh << 8 | operandLow);
                if (instruction.Mode == Mode.AbsX) address += X;
                else if (instruction.Mode == Mode.AbsY) address += Y;
                return true;

            case Mode.IndX:
                if (cycle == 1) { pointer = (byte)((operandLow = ReadByte(PC++)) + X); return false; }
                if (cycle == 2) { address = ReadByte(BasePage(pointer)); return false; }
                address |= (ushort)(ReadByte(BasePage((byte)(pointer + 1))) << 8);
                return true;

            case Mode.IndY:
            case Mode.IndZ:
                if (cycle == 1) { pointer = operandLow = ReadByte(PC++); return false; }
                if (cycle == 2) { address = ReadByte(BasePage(pointer)); return false; }
                address |= (ushort)(ReadByte(BasePage((byte)(pointer + 1))) << 8);
                address += instruction.Mode == Mode.IndY ? Y : Z;
                return true;

            case Mode.StackY: // (d,SP),Y: the pointer is d bytes above the stack pointer (16-bit sum)
                if (cycle == 1) { operandLow = ReadByte(PC++); return false; }
                if (cycle == 2) { address = ReadByte((ushort)(SP + operandLow)); return false; }
                if (cycle == 3) { address |= (ushort)(ReadByte((ushort)(SP + operandLow + 1)) << 8); return false; }
                address += Y; // internal cycle
                return true;

            case Mode.BpRel:
                throw new InvalidOperationException("BBR/BBS have their own cycles");

            default:
                throw new InvalidOperationException($"Mode {instruction.Mode} is not valid for {instruction.Op}");
        }
    }

    // After the address: the access to the operand
    void DataCycle()
    {
        Op op = instruction.Op;
        switch (op)
        {
            case Op.STA: case Op.STX: case Op.STY: case Op.STZ:
                WriteByte(address, WriteValue(op));
                Done();
                return;

            case Op.ASL: case Op.LSR: case Op.ASR: case Op.ROL: case Op.ROR: case Op.INC: case Op.DEC:
            case Op.TSB: case Op.TRB: case Op.RMB: case Op.SMB:
                if (step == 0) { data = ReadByte(address); step = 1; Next(); }
                else { WriteByte(address, ModifyByte(op, data)); Done(); }
                return;

            case Op.INW: case Op.DEW: case Op.ASW: case Op.ROW:
                // A word: low byte first. In base page the high byte is inside the page
                ushort high = instruction.Mode == Mode.Bp ? (ushort)((address & 0xFF00) | (byte)(address + 1)) : (ushort)(address + 1);
                if (step == 0) { data = ReadByte(address); step = 1; Next(); }
                else if (step == 1) { dataHigh = ReadByte(high); step = 2; Next(); }
                else if (step == 2)
                {
                    ushort result = ModifyWord(op, (ushort)(data | dataHigh << 8));
                    data = (byte)result;
                    dataHigh = (byte)(result >> 8);
                    WriteByte(address, data);
                    step = 3;
                    Next();
                }
                else { WriteByte(high, dataHigh); Done(); }
                return;

            case Op.PHW: // abs: reads the word, then pushes it: low byte first, so the first PLA gives the high byte
                if (step == 0) { data = ReadByte(address); step = 1; Next(); }
                else if (step == 1) { dataHigh = ReadByte((ushort)(address + 1)); step = 2; Next(); }
                else if (step == 2) { Push(data); step = 3; Next(); }
                else { Push(dataHigh); Done(); }
                return;

            default:
                ExecuteRead(op, ReadByte(address));
                Done();
                return;
        }
    }

    // Branches: 2 cycles (byte offset) or 3 (word offset), taken or not. Both offsets are relative to the address
    // of the opcode + 2
    void BranchCycle()
    {
        if (instruction.Mode == Mode.Rel)
        {
            operandLow = ReadByte(PC++); // PC is now opcode + 2
            if (BranchTaken(instruction.Op)) PC = (ushort)(PC + (sbyte)operandLow);
            Done();
        }
        else if (cycle == 1) { operandLow = ReadByte(PC++); Next(); }
        else
        {
            operandHigh = ReadByte(PC++); // PC is now opcode + 3; the offset is relative to opcode + 2
            if (BranchTaken(instruction.Op)) PC = (ushort)(PC - 1 + (short)(operandHigh << 8 | operandLow));
            Done();
        }
    }

    // BBR/BBS bp,rel: 4 cycles. Branches if bit n of the base page byte is 0 (BBR) or 1 (BBS)
    void BranchOnBit()
    {
        if (cycle == 1) { operandLow = ReadByte(PC++); Next(); }
        else if (cycle == 2) { data = ReadByte(BasePage(operandLow)); Next(); }
        else
        {
            operandHigh = ReadByte(PC++); // PC is now opcode + 3
            sbyte offset = (sbyte)operandHigh;
            bool bit = (data & (1 << ((opcode >> 4) & 7))) != 0;
            if (bit == (instruction.Op == Op.BBS)) PC = (ushort)(PC + offset);
            Done();
        }
    }

    void PushRegister()
    {
        if (cycle == 1) { Next(); return; } // internal
        Push(instruction.Op switch
        {
            Op.PHA => A,
            Op.PHX => X,
            Op.PHY => Y,
            Op.PHZ => Z,
            _ => (byte)(Status | FLAG_B), // PHP: B is always pushed set
        });
        Done();
    }

    void PullRegister()
    {
        if (cycle == 1) { Next(); return; } // internal
        byte value = Pop();
        switch (instruction.Op)
        {
            case Op.PLA: A = value; SetZN(A); break;
            case Op.PLX: X = value; SetZN(X); break;
            case Op.PLY: Y = value; SetZN(Y); break;
            case Op.PLZ: Z = value; SetZN(Z); break;
            default: PullStatus(value); break;
        }
        Done();
    }

    // JMP abs (3 cycles), JMP (abs) (5), JMP (abs,X) (6: one more to add X)
    void Jump()
    {
        if (instruction.Mode == Mode.Abs)
        {
            if (cycle == 1) { operandLow = ReadByte(PC++); Next(); }
            else { operandHigh = ReadByte(PC); PC = (ushort)(operandHigh << 8 | operandLow); Done(); }
            return;
        }
        if (IndirectAddress(internalCycle: instruction.Mode == Mode.AbsXInd)) { PC = address; Done(); }
        else Next();
    }

    // JSR abs (5 cycles), JSR (abs) and JSR (abs,X) (7), BSR (5): push the address of the instruction's last byte,
    // then jump
    void Call()
    {
        if (instruction.Op == Op.BSR)
        {
            if (cycle == 1) { operandLow = ReadByte(PC++); Next(); }
            else if (cycle == 2) { operandHigh = ReadByte(PC++); Next(); }
            else if (cycle == 3) { Push((byte)((PC - 1) >> 8)); Next(); }
            else
            {
                Push((byte)(PC - 1));
                PC = (ushort)(PC - 1 + (short)(operandHigh << 8 | operandLow));
                Done();
            }
            return;
        }

        if (!addressReady)
        {
            if (instruction.Mode == Mode.Abs)
            {
                if (cycle == 1) operandLow = ReadByte(PC++);
                else { operandHigh = ReadByte(PC++); address = (ushort)(operandHigh << 8 | operandLow); addressReady = true; }
            }
            else addressReady = IndirectAddress(internalCycle: false);
            Next();
            return;
        }
        if (step == 0) { Push((byte)((PC - 1) >> 8)); step = 1; Next(); }
        else { Push((byte)(PC - 1)); PC = address; Done(); }
    }

    // The target word of (abs) or (abs,X) goes to "address". Cycles: 1-2 the operand, [3 internal], then the low
    // and high byte of the pointer; returns true in the last one. Used by JMP and JSR
    bool IndirectAddress(bool internalCycle)
    {
        int first = internalCycle ? 4 : 3; // cycle of the pointer's low byte
        if (cycle == 1) { operandLow = ReadByte(PC++); return false; }
        if (cycle == 2) { operandHigh = ReadByte(PC++); return false; }
        if (cycle < first) return false;
        ushort pointerAddress = (ushort)((operandHigh << 8 | operandLow) + (instruction.Mode == Mode.AbsXInd ? X : 0));
        if (cycle == first) { data = ReadByte(pointerAddress); return false; }
        address = (ushort)(data | ReadByte((ushort)(pointerAddress + 1)) << 8);
        return true;
    }

    // RTS (4 cycles): pulls the address and adds 1. RTN #n (7 cycles) does the same and also drops n bytes
    // of arguments from the stack
    void ReturnFromSubroutine()
    {
        if (instruction.Op == Op.RTN)
        {
            if (cycle == 1) operandLow = ReadByte(PC++);
            else if (cycle == 2) address = Pop();
            else if (cycle == 3) address |= (ushort)(Pop() << 8);
            else if (cycle == 6)
            {
                if ((Status & FLAG_E) != 0) SPL = (byte)(SPL + operandLow); else SP += operandLow;
                PC = (ushort)(address + 1);
                Done();
                return;
            }
        }
        else
        {
            if (cycle == 1) address = Pop();
            else if (cycle == 2) address |= (ushort)(Pop() << 8);
            else { PC = (ushort)(address + 1); Done(); return; }
        }
        Next();
    }

    // RTI (5 cycles): P, then PCL and PCH
    void ReturnFromInterrupt()
    {
        if (cycle == 1) { PullStatus(Pop()); Next(); }
        else if (cycle == 2) { address = Pop(); Next(); }
        else if (cycle == 3) { address |= (ushort)(Pop() << 8); Next(); }
        else { PC = address; Done(); }
    }

    void InterruptCycle()
    {
        if (cycle == 1) Next(); // internal
        else InterruptSequence(brk: false);
    }

    // Cycles 1-6 of BRK (cycle 1 skips the padding byte) and 2-6 of IRQ/NMI: push PC and status, then read the
    // vector. BRK stores PC+2, an interrupt the address of the next instruction. The vector is chosen at cycle 4:
    // an NMI arriving before then takes over a BRK or an IRQ too
    bool vectorIsNmi;

    void InterruptSequence(bool brk)
    {
        switch (cycle)
        {
            case 1: PC++; Next(); break;
            case 2: Push((byte)(PC >> 8)); Next(); break;
            case 3: Push((byte)PC); Next(); break;
            case 4:
                vectorIsNmi = nmiPending;
                if (nmiPending) nmiPending = false;
                Push((byte)(brk ? Status | FLAG_B : Status & ~FLAG_B));
                Next();
                break;
            case 5:
                data = ReadByte(vectorIsNmi ? (ushort)0xFFFA : (ushort)0xFFFE);
                Status = (byte)((Status | FLAG_I) & ~FLAG_D); // CMOS: interrupts clear decimal mode
                Next();
                break;
            default:
                PC = (ushort)(ReadByte(vectorIsNmi ? (ushort)0xFFFB : (ushort)0xFFFF) << 8 | data);
                inInterrupt = false;
                Done();
                break;
        }
    }
}
