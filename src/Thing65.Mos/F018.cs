namespace Thing65.Mos;

// CSG F018 "DMAgic": the C65's DMA controller. It runs jobs described by a list in memory: COPY, MIX (a boolean mix of
// source and destination), SWAP and FILL of up to 64 KB, with independent direction, hold (fixed pointer), modulo
// (rectangular windows), XY_MOD (rectangles of a bitplane screen, with a barrel shifter) and I/O selection for the source
// and the destination, and chaining of several lists. While a job runs the CPU is halted (Busy drives its /DMA pin) and
// every Tick is one bus access: one for each list byte, then for each byte a read and a write (copy), two reads and
// a write (mix), two reads and two writes (swap) or a write (fill).
//
// Registers (at $D700): 0 = list address low, writing it starts the job; 1 = address high; 2 = bank (the list is
// in the first 1 MB); 3 = status (bit 7 busy, bit 0 chained), writing it cancels the job and reading it resumes a job
// that stopped for an interrupt.
// A list is 12 bytes (F018B, the default) or 11 (F018A):
//   F018B: command, count lo/hi, source lo/hi/bank, destination lo/hi/bank, sub-command, modulo lo/hi
//   F018A: the same without the sub-command; direction, modulo and hold come from bits 6, 5, 4 of the bank bytes
// Command: bits 0-1 operation (0 copy, 1 mix, 2 swap, 3 fill), bit 2 chain, bit 3 interrupt (the job stops when an IRQ or
// NMI is asserted and waits for the status to be read), bits 4/5 source and destination direction (F018B: 1 = descending).
// For MIX bits 4-7 are the minterms: the result bit is bit 4 + 2 * source bit + destination bit of the command
// (so $80 = source AND destination, $60 = XOR, $E0 = OR...), and the directions are ascending.
// Sub-command: bit 0/1 source modulo/hold, bit 2/3 destination; both bits set = XY_MOD.
// Bank byte: bits 0-3 address bits 16-19, bit 7 I/O.
// XY_MOD (the manual, chapter 4): the 16-bit address is Y (9 bits) * 128 + X (7 bits) of a bitplane screen; X counts bytes and
// Y lines; the count is columns (low byte) by rows; the modulo is a single byte, 128 - width + 1; the "misc" byte (the modulo high
// byte of the list) holds the screen location (bits 4-6: address bits 13-15), the barrel shift (bits 0-2) and, an
// assumption, bit 3 = 640 pixels wide (80 bytes per cell row, else 40). Ascending shifts right, descending left; the
// bits shifted out go into the next byte of the row.
// Reference: "C65 Specifications", chapters 2.7 and 4
public class F018 : IF018Pinout
{
    public IDmaMemory? Memory { get; set; }
    public Func<bool> BusGrant { get; set; } = () => true;
    public Func<bool> InterruptRequest { get; set; } = () => false;

    // List format: true = F018B (12 bytes), false = F018A (11 bytes)
    public bool F018B { get; set; } = true;

    // The CPU is held while a job runs, not while it is stopped for an interrupt
    public bool Busy => state != State.Idle && !paused;

    enum State { Idle, ReadList, Bytes }

    State state;
    bool paused;                       // stopped for an interrupt: the status read resumes it
    byte listLow, listHigh, listBank;
    int listAddress;                   // address of the list being executed
    readonly byte[] list = new byte[12];
    int listLength, listIndex;
    bool chained;

    // Decoded job
    int command;
    int operation;
    int minterms;
    int source, destination;
    bool sourceIo, destinationIo;
    bool sourceDown, destinationDown;
    bool sourceHold, destinationHold;
    bool sourceModulo, destinationModulo;
    bool sourceXy, destinationXy;
    int modulo;
    int misc;                          // XY_MOD: screen location, shift, width
    int columns;                       // bytes per row, for the modulo modes
    int sourceColumn, destinationColumn;
    int remaining;
    byte fillValue;
    bool interruptible;

    // The step of the byte being moved and the bytes in flight
    int phase;
    byte sourceByte, destinationByte;
    byte sourceCarry;                  // XY_MOD barrel shifter: the bits shifted out of the previous byte of the row

    public void Reset()
    {
        state = State.Idle;
        paused = false;
        listLow = listHigh = listBank = 0;
        chained = false;
    }

    public byte Read(int register)
    {
        switch (register & 3)
        {
            case 0: return listLow;
            case 1: return listHigh;
            case 2: return listBank;
            default:
                {
                    byte status = (byte)((state != State.Idle ? 0x80 : 0) | (chained ? 0x01 : 0));
                    paused = false; // reading the status resumes a job that stopped for an interrupt
                    return status;
                }
        }
    }

    public void Write(int register, byte value)
    {
        switch (register & 3)
        {
            case 0:
                listLow = value;
                listAddress = (listBank & 0x0F) << 16 | listHigh << 8 | listLow;
                BeginList();
                break;
            case 1: listHigh = value; break;
            case 2: listBank = value; break;
            default: state = State.Idle; paused = false; chained = false; break; // cancels the job
        }
    }

    void BeginList()
    {
        listLength = F018B ? 12 : 11;
        listIndex = 0;
        paused = false;
        state = State.ReadList;
    }

    public void Tick()
    {
        if (state == State.Idle || paused || Memory is null || !BusGrant()) return;

        if (state == State.ReadList)
        {
            list[listIndex] = Memory.Read((listAddress + listIndex) & 0xFFFFF, false);
            if (++listIndex == listLength) Decode();
            return;
        }

        // Between two bytes a job that allows it stops for an interrupt
        if (phase == 0 && interruptible && InterruptRequest())
        {
            paused = true;
            return;
        }
        StepByte();
    }

    // One bus access of the byte being moved
    void StepByte()
    {
        switch (operation)
        {
            case 0: // COPY: read, write
                if (phase == 0) { sourceByte = ReadSource(); phase = 1; }
                else { Write(sourceXy || destinationXy ? Shifted(sourceByte, destinationDown, ref sourceCarry, destinationColumn) : sourceByte, destinationIo); EndByte(); }
                break;

            case 1: // MIX: read the source, read the destination, write the mix
                if (phase == 0) { sourceByte = ReadSource(); phase = 1; }
                else if (phase == 1) { destinationByte = Memory!.Read(Physical(destination, destinationXy), destinationIo); phase = 2; }
                else { Write(Mix(sourceByte, destinationByte), destinationIo); EndByte(); }
                break;

            case 2: // SWAP: read both, write each into the other's place
                if (phase == 0) { sourceByte = ReadSource(); phase = 1; }
                else if (phase == 1) { destinationByte = Memory!.Read(Physical(destination, destinationXy), destinationIo); phase = 2; }
                else if (phase == 2) { Memory!.Write(Physical(source, sourceXy, behind: true), sourceIo, destinationByte); phase = 3; }
                else { Write(sourceByte, destinationIo); EndByte(); }
                break;

            default: // FILL: write
                Write(fillValue, destinationIo);
                EndByte();
                break;
        }
    }

    byte ReadSource()
    {
        byte value = Memory!.Read(Physical(source, sourceXy), sourceIo);
        sourceBeforeAdvance = source;
        Advance(ref source, ref sourceColumn, sourceHold, sourceDown, sourceModulo, sourceXy);
        return value;
    }

    int sourceBeforeAdvance;

    // SWAP writes back to the place the source byte was read from
    int Physical(int address, bool xy, bool behind) => Physical(behind ? sourceBeforeAdvance : address, xy);

    void Write(byte value, bool io)
    {
        Memory!.Write(Physical(destination, destinationXy), io, value);
        Advance(ref destination, ref destinationColumn, destinationHold, destinationDown, destinationModulo, destinationXy);
    }

    void EndByte()
    {
        phase = 0;
        if (--remaining == 0) Finish();
    }

    // The minterm table: for every bit, the command's bit 4 + 2 * source + destination
    byte Mix(byte s, byte d)
    {
        int result = 0;
        for (int bit = 0; bit < 8; bit++)
        {
            int index = ((s >> bit) & 1) * 2 + ((d >> bit) & 1);
            result |= (minterms >> index & 1) << bit;
        }
        return (byte)result;
    }

    void Decode()
    {
        command = list[0];
        operation = command & 3;
        int count = list[1] | list[2] << 8;
        source = (list[5] & 0x0F) << 16 | list[4] << 8 | list[3];
        destination = (list[8] & 0x0F) << 16 | list[7] << 8 | list[6];
        sourceIo = (list[5] & 0x80) != 0;
        destinationIo = (list[8] & 0x80) != 0;

        bool sourceMod, sourceHoldBit, destinationMod, destinationHoldBit;
        if (F018B)
        {
            sourceDown = (command & 0x10) != 0;
            destinationDown = (command & 0x20) != 0;
            sourceMod = (list[9] & 0x01) != 0;
            sourceHoldBit = (list[9] & 0x02) != 0;
            destinationMod = (list[9] & 0x04) != 0;
            destinationHoldBit = (list[9] & 0x08) != 0;
            modulo = list[10] | list[11] << 8;
        }
        else
        {
            sourceDown = (list[5] & 0x40) != 0;
            sourceMod = (list[5] & 0x20) != 0;
            sourceHoldBit = (list[5] & 0x10) != 0;
            destinationDown = (list[8] & 0x40) != 0;
            destinationMod = (list[8] & 0x20) != 0;
            destinationHoldBit = (list[8] & 0x10) != 0;
            modulo = list[9] | list[10] << 8;
        }
        // Hold + modulo is XY_MOD
        sourceXy = sourceMod && sourceHoldBit;
        destinationXy = destinationMod && destinationHoldBit;
        sourceHold = sourceHoldBit && !sourceMod;
        sourceModulo = sourceMod && !sourceHoldBit;
        destinationHold = destinationHoldBit && !destinationMod;
        destinationModulo = destinationMod && !destinationHoldBit;
        if (sourceXy || destinationXy)
        {
            misc = modulo >> 8;
            modulo &= 0xFF;          // XY_MOD has a one-byte modulo
            sourceModulo |= sourceXy;
            destinationModulo |= destinationXy;
        }
        if (operation == 1) { minterms = command >> 4; sourceDown = destinationDown = false; }

        // With a modulo the count is columns (low byte) by rows (high byte), 0 meaning 256; otherwise it is the
        // number of bytes, 0 meaning 64 KB
        if (sourceModulo || destinationModulo)
        {
            columns = (count & 0xFF) == 0 ? 256 : count & 0xFF;
            remaining = columns * ((count >> 8) == 0 ? 256 : count >> 8);
        }
        else
        {
            columns = 1;
            remaining = count == 0 ? 0x10000 : count;
        }
        sourceColumn = destinationColumn = 0;
        sourceCarry = 0;
        chained = (command & 0x04) != 0;
        interruptible = (command & 0x08) != 0;
        fillValue = list[3]; // the fill byte is the low byte of the source
        phase = 0;
        state = State.Bytes;
    }

    // Next list when chained, otherwise the job is over
    void Finish()
    {
        if (chained)
        {
            listAddress = (listAddress + listLength) & 0xFFFFF;
            BeginList();
        }
        else state = State.Idle;
    }

    // Moves a pointer to the next byte. In modulo mode the last byte of a row is followed by the modulo
    // instead of 1, which takes the pointer to the start of the next row; descending pointers subtract.
    // XY_MOD pointers wrap inside their 64 KB bank (Y * 128 + X is a 16-bit number)
    void Advance(ref int address, ref int column, bool hold, bool down, bool moduloMode, bool xy)
    {
        if (hold) return;
        int step = 1;
        if (moduloMode && ++column == columns)
        {
            column = 0;
            step = modulo;
        }
        int next = address + (down ? -step : step);
        address = xy ? (address & 0xF0000) | (next & 0xFFFF) : next & 0xFFFFF;
    }

    // XY_MOD: the byte of the bitplane for X (7 bits) and Y (9 bits). The bitplane is made of cells of 8 bytes, a cell row is
    // 40 or 80 cells; the screen location (the misc byte) gives address bits 13-15
    int Physical(int address, bool xy)
    {
        if (!xy) return address;
        int pointer = address & 0xFFFF;
        int x = pointer & 0x7F, y = pointer >> 7;
        int cells = (misc & 0x08) != 0 ? 80 : 40;
        int offset = (misc >> 4 & 7) << 13;
        return (address & 0xF0000) | (offset + (y >> 3) * cells * 8 + x * 8 + (y & 7)) & 0xFFFF;
    }

    // The barrel shifter of XY_MOD: n = misc bits 0-2 (0 = none). Ascending shifts right, descending left; the bits that
    // fall out of one byte enter the next one of the same row (the first byte of a row gets zeros)
    byte Shifted(byte value, bool down, ref byte carry, int column)
    {
        int n = misc & 7;
        if (n == 0) return value;
        if (column == 0) carry = 0;
        byte result;
        if (!down)
        {
            result = (byte)(value >> n | carry << (8 - n));
            carry = (byte)(value & ((1 << n) - 1));
        }
        else
        {
            result = (byte)(value << n | carry >> (8 - n));
            carry = (byte)(value >> (8 - n));
        }
        return result;
    }
}
