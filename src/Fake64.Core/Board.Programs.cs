using System.Collections.Concurrent;

namespace Fake64;

// Caricamento di programmi e input da tastiera, come farebbero LOAD e l'utente al prompt BASIC
public partial class Board
{
    const ushort KEYBOARD_BUFFER = 0x0277;
    const ushort KEYBOARD_BUFFER_COUNT = 0x00C6;
    const int KEYBOARD_BUFFER_SIZE = 10;

    // Puntatori del BASIC in zero page
    const ushort TXTTAB = 0x2B; // inizio del programma
    const ushort VARTAB = 0x2D; // inizio delle variabili = fine del programma
    const ushort ARYTAB = 0x2F; // inizio degli array
    const ushort STREND = 0x31; // fine degli array
    const ushort LOAD_END = 0xAE; // indirizzo di fine impostato da LOAD

    readonly ConcurrentQueue<Action> pendingCommands = new();

    // Esegue un'azione sul thread di emulazione fra due cicli, così non legge o scrive la memoria
    // mentre la CPU sta eseguendo; se il clock in tempo reale non è attivo la esegue subito
    public void Enqueue(Action command)
    {
        if (clock is null) command();
        else pendingCommands.Enqueue(command);
    }

    void RunPendingCommands()
    {
        while (pendingCommands.TryDequeue(out var command)) command();
    }

    // Carica un file .prg (i primi due byte sono l'indirizzo di caricamento) direttamente in RAM.
    // Se l'indirizzo coincide con l'inizio del BASIC aggiorna i puntatori come LOAD, così RUN e LIST funzionano
    public (ushort Start, ushort End) LoadPrg(byte[] prg)
    {
        if (prg.Length < 3)
            throw new ArgumentException("Un file .prg contiene almeno l'indirizzo di caricamento e un byte di dati", nameof(prg));

        ushort start = (ushort)(prg[0] | prg[1] << 8);
        int end = start + prg.Length - 2;
        if (end > 0x10000)
            throw new ArgumentException($"Il programma (${start:X4}, {prg.Length - 2} byte) supera la fine della memoria", nameof(prg));

        for (int i = 2; i < prg.Length; i++)
            ram64.Address((ushort)(start + i - 2), prg[i]);

        WriteRamWord(LOAD_END, (ushort)end);
        if (start == ReadRamWord(TXTTAB))
        {
            WriteRamWord(VARTAB, (ushort)end);
            WriteRamWord(ARYTAB, (ushort)end);
            WriteRamWord(STREND, (ushort)end);
        }

        return (start, (ushort)end);
    }

    public (ushort Start, ushort End) LoadPrg(string path) => LoadPrg(File.ReadAllBytes(path));

    public bool IsBasicProgram(ushort start) => start == ReadRamWord(TXTTAB);

    // Inserisce testo nel buffer di tastiera del KERNAL, come se fosse stato digitato ("\r" = RETURN)
    public void TypeText(string text)
    {
        if (text.Length > KEYBOARD_BUFFER_SIZE)
            throw new ArgumentException($"Il buffer di tastiera contiene al massimo {KEYBOARD_BUFFER_SIZE} caratteri", nameof(text));

        for (int i = 0; i < text.Length; i++)
            ram64.Address((ushort)(KEYBOARD_BUFFER + i), (byte)char.ToUpperInvariant(text[i]));
        ram64.Address(KEYBOARD_BUFFER_COUNT, (byte)text.Length);
    }

    ushort ReadRamWord(ushort addr) => (ushort)(ram64.Address(addr) | ram64.Address((ushort)(addr + 1)) << 8);

    void WriteRamWord(ushort addr, ushort value)
    {
        ram64.Address(addr, (byte)value);
        ram64.Address((ushort)(addr + 1), (byte)(value >> 8));
    }
}
