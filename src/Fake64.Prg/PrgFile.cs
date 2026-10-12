namespace Fake64.Prg;

// File .prg: i primi due byte sono l'indirizzo di caricamento (little-endian), il resto i dati.
// Il caricamento scrive direttamente nella RAM della Board, anche sotto le ROM; se l'indirizzo coincide
// con l'inizio del BASIC aggiorna i puntatori come fa LOAD, così RUN e LIST funzionano.
// Va eseguito sul thread di emulazione (Board.Enqueue) se il clock in tempo reale è attivo
public static class PrgFile
{
    // Puntatori del BASIC in zero page
    const ushort TXTTAB = 0x2B; // inizio del programma
    const ushort VARTAB = 0x2D; // inizio delle variabili = fine del programma
    const ushort ARYTAB = 0x2F; // inizio degli array
    const ushort STREND = 0x31; // fine degli array
    const ushort LOAD_END = 0xAE; // indirizzo di fine impostato da LOAD

    // Indirizzi occupati dal programma (End escluso); solleva ArgumentException se il file non è valido
    public static (ushort Start, int End) GetRange(byte[] prg)
    {
        if (prg.Length < 3)
            throw new ArgumentException("Un file .prg contiene almeno l'indirizzo di caricamento e un byte di dati", nameof(prg));

        ushort start = (ushort)(prg[0] | prg[1] << 8);
        int end = start + prg.Length - 2;
        if (end > 0x10000)
            throw new ArgumentException($"Il programma (${start:X4}, {prg.Length - 2} byte) supera la fine della memoria", nameof(prg));

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

    // Vero se il programma inizia dove il BASIC si aspetta il testo del programma (di solito $0801)
    public static bool IsBasicProgram(this Board board, ushort start) => start == ReadWord(board.Ram, TXTTAB);

    static ushort ReadWord(Ram64KbChip ram, ushort addr) => (ushort)(ram.Address(addr) | ram.Address((ushort)(addr + 1)) << 8);

    static void WriteWord(Ram64KbChip ram, ushort addr, ushort value)
    {
        ram.Address(addr, (byte)value);
        ram.Address((ushort)(addr + 1), (byte)(value >> 8));
    }
}
