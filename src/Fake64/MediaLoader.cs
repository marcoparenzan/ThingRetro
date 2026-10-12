using Fake64.Crt;
using Fake64.Prg;
using Fake64.C64;

namespace Fake64;

// Apertura dei file supportati dall'app: .prg (Fake64.Prg) e .crt (Fake64.Crt). La scheda (Fake64.C64)
// non conosce nessuno dei due formati: qui si legge il file e si passa il risultato alla Board
static class MediaLoader
{
    // Tempo lasciato al KERNAL per arrivare a READY. prima di caricare un .prg all'avvio
    public static readonly TimeSpan BootTime = TimeSpan.FromSeconds(3);

    public static bool IsCartridge(string path) => HasExtension(path, ".crt");
    public static bool IsProgram(string path) => HasExtension(path, ".prg");
    public static bool IsSupported(string path) => IsCartridge(path) || IsProgram(path);

    static bool HasExtension(string path, string extension) =>
        string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase);

    // Restituisce una descrizione per il titolo della finestra; i file non validi sollevano un'eccezione
    // (IOException, InvalidDataException, NotSupportedException, ArgumentException)
    public static string Open(Board board, string path)
    {
        var content = File.ReadAllBytes(path);
        var fileName = Path.GetFileName(path);

        if (IsCartridge(path))
        {
            var cartridge = CrtFile.Load(content);
            board.Enqueue(() => board.InsertCartridge(cartridge));
            var name = string.IsNullOrEmpty(cartridge.Name) ? fileName : cartridge.Name;
            return $"{name} ({cartridge.TypeName})";
        }

        // Il caricamento scrive in RAM: va fatto sul thread di emulazione. Il file si verifica prima,
        // così gli errori arrivano a chi lo ha aperto
        var (start, end) = PrgFile.GetRange(content);

        // Leggere i due byte del puntatore al BASIC da questo thread è innocuo: il BASIC non lo cambia
        bool basic = board.IsBasicProgram(start);
        board.Enqueue(() =>
        {
            board.LoadPrg(content);
            if (basic) board.TypeText("RUN\r");
        });
        return basic ? $"{fileName} (BASIC)" : $"{fileName} ${start:X4}-${end - 1:X4}: SYS {start}";
    }
}
