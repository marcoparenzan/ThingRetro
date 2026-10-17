namespace Thing65.Mos;

// CSG F018 "DMAgic": the C65's DMA controller. It runs jobs described by a list in memory: COPY and FILL of up to
// 64 KB, with independent direction, hold (fixed pointer), modulo (rectangular windows) and I/O selection for the
// source and the destination, and chaining of several lists. While a job runs the CPU is halted (Busy drives its
// /DMA pin) and every Tick is one bus access: one for each list byte, then a read and a write for each byte copied
// or a write for each byte filled.
//
// Registers (at $D700): 0 = list address low, writing it starts the job; 1 = address high; 2 = bank (the list is
// in the first 1 MB); 3 = status (bit 7 busy, bit 0 chained), writing it cancels the job.
// A list is 12 bytes (F018B, the default) or 11 (F018A):
//   F018B: command, count lo/hi, source lo/hi/bank, destination lo/hi/bank, sub-command, modulo lo/hi
//   F018A: the same without the sub-command; direction, modulo and hold come from bits 6, 5, 4 of the bank bytes
// Command: bits 0-1 operation (0 copy, 1 mix, 2 swap, 3 fill), bit 2 chain, bit 3 interrupt, bits 4/5 source and
// destination direction (F018B: 1 = descending). Sub-command: bit 0/1 source modulo/hold, bit 2/3 destination.
// Bank byte: bits 0-3 address bits 16-19, bit 7 I/O.
// Not implemented, because the chip did not implement it either or the manual does not define it: MIX and SWAP
// (the job ends at once), XY_MOD (hold + modulo: treated as linear), yielding to interrupts (the INT bit).
// Reference: "C65 Specifications", chapters 2.7 and 4
public class F018 : IF018Pinout
{
    public IDmaMemory? Memory { get; set; }
    public Func<bool> BusGrant { get; set; } = () => true;

    // List format: true = F018B (12 bytes), false = F018A (11 bytes)
    public bool F018B { get; set; } = true;

    public bool Busy => state != State.Idle;

    enum State { Idle, ReadList, CopyRead, CopyWrite, Fill }

    State state;
    byte listLow, listHigh, listBank;
    int listAddress;                   // address of the list being executed
    readonly byte[] list = new byte[12];
    int listLength, listIndex;
    bool chained;

    // Decoded job
    int command;
    int source, destination;
    bool sourceIo, destinationIo;
    bool sourceDown, destinationDown;
    bool sourceHold, destinationHold;
    bool sourceModulo, destinationModulo;
    int modulo;
    int columns;                       // bytes per row, for the modulo modes
    int sourceColumn, destinationColumn;
    int remaining;
    byte value;                        // byte read, waiting to be written

    public void Reset()
    {
        state = State.Idle;
        listLow = listHigh = listBank = 0;
        chained = false;
    }

    public byte Read(int register) => (register & 3) switch
    {
        0 => listLow,
        1 => listHigh,
        2 => listBank,
        _ => (byte)((Busy ? 0x80 : 0) | (chained ? 0x01 : 0)),
    };

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
            default: state = State.Idle; chained = false; break; // cancels the job
        }
    }

    void BeginList()
    {
        listLength = F018B ? 12 : 11;
        listIndex = 0;
        state = State.ReadList;
    }

    public void Tick()
    {
        if (state == State.Idle || Memory is null || !BusGrant()) return;

        switch (state)
        {
            case State.ReadList:
                list[listIndex] = Memory.Read((listAddress + listIndex) & 0xFFFFF, false);
                if (++listIndex == listLength) Decode();
                break;

            case State.CopyRead:
                value = Memory.Read(source, sourceIo);
                Advance(ref source, ref sourceColumn, sourceHold, sourceDown, sourceModulo);
                state = State.CopyWrite;
                break;

            case State.CopyWrite:
                Memory.Write(destination, destinationIo, value);
                Advance(ref destination, ref destinationColumn, destinationHold, destinationDown, destinationModulo);
                if (--remaining == 0) Finish(); else state = State.CopyRead;
                break;

            case State.Fill:
                Memory.Write(destination, destinationIo, value);
                Advance(ref destination, ref destinationColumn, destinationHold, destinationDown, destinationModulo);
                if (--remaining == 0) Finish();
                break;
        }
    }

    void Decode()
    {
        command = list[0];
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
        // Hold + modulo is the XY_MOD mode, which is not implemented: it is treated as linear
        sourceHold = sourceHoldBit && !sourceMod;
        sourceModulo = sourceMod && !sourceHoldBit;
        destinationHold = destinationHoldBit && !destinationMod;
        destinationModulo = destinationMod && !destinationHoldBit;

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
        chained = (command & 0x04) != 0;

        switch (command & 3)
        {
            case 0: state = State.CopyRead; break;
            case 3: value = list[3]; state = State.Fill; break; // the fill byte is the low byte of the source
            default: Finish(); break;                           // MIX and SWAP: not implemented
        }
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
    // instead of 1, which takes the pointer to the start of the next row; descending pointers subtract
    void Advance(ref int address, ref int column, bool hold, bool down, bool moduloMode)
    {
        if (hold) return;
        int step = 1;
        if (moduloMode && ++column == columns)
        {
            column = 0;
            step = modulo;
        }
        address = (address + (down ? -step : step)) & 0xFFFFF;
    }
}
