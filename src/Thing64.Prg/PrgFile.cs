using Thing64.C64;

namespace Thing64.Prg;

// .prg file: the first two bytes are the load address (little-endian), the rest is the data.
// Loading writes straight into the Board's RAM, under the ROMs too; if the address is the start of BASIC
// it updates the pointers as LOAD does, so that RUN and LIST work.
// It must run on the emulation thread (Board.Enqueue) when the real-time clock is active
public static class PrgFile
{
    // BASIC pointers in zero page
    const ushort TXTTAB = 0x2B; // start of the program
    const ushort VARTAB = 0x2D; // start of the variables = end of the program
    const ushort ARYTAB = 0x2F; // start of the arrays
    const ushort STREND = 0x31; // end of the arrays
    const ushort LOAD_END = 0xAE; // end address set by LOAD

    // Addresses taken by the program (End excluded); throws ArgumentException if the file is not valid
    public static (ushort Start, int End) GetRange(byte[] prg)
    {
        if (prg.Length < 3)
            throw new ArgumentException("A .prg file holds at least the load address and one data byte", nameof(prg));

        ushort start = (ushort)(prg[0] | prg[1] << 8);
        int end = start + prg.Length - 2;
        if (end > 0x10000)
            throw new ArgumentException($"The program (${start:X4}, {prg.Length - 2} bytes) goes past the end of memory", nameof(prg));

        return (start, end);
    }

    public static (ushort Start, ushort End) LoadPrg(this Board board, byte[] prg)
    {
        var (start, end) = GetRange(prg);

        var ram = board.Ram;
        for (int i = 2; i < prg.Length; i++)
            ram.Address((ushort)(start + i - 2), prg[i]);

        WriteWord(ram, LOAD_END, (ushort)end);
        if (start == ReadWord(ram, TXTTAB))
        {
            WriteWord(ram, VARTAB, (ushort)end);
            WriteWord(ram, ARYTAB, (ushort)end);
            WriteWord(ram, STREND, (ushort)end);
        }

        return (start, (ushort)end);
    }

    public static (ushort Start, ushort End) LoadPrg(this Board board, string path) => board.LoadPrg(File.ReadAllBytes(path));

    // True if the program starts where BASIC expects the program text (usually $0801)
    public static bool IsBasicProgram(this Board board, ushort start) => start == ReadWord(board.Ram, TXTTAB);

    static ushort ReadWord(Ram64KbChip ram, ushort addr) => (ushort)(ram.Address(addr) | ram.Address((ushort)(addr + 1)) << 8);

    static void WriteWord(Ram64KbChip ram, ushort addr, ushort value)
    {
        ram.Address(addr, (byte)value);
        ram.Address((ushort)(addr + 1), (byte)(value >> 8));
    }
}
