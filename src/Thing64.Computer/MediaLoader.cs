using Thing64.Crt;
using Thing64.Prg;
using Thing64.C64;

namespace Thing64.Computer;

// Opening the files the app supports: .prg (Thing64.Prg) and .crt (Thing64.Crt). The board (Thing64.C64)
// knows neither format: the file is read here and the result is handed to the Board
public static class MediaLoader
{
    // Time left to the KERNAL to reach READY. before a .prg is loaded
    public static readonly TimeSpan BootTime = TimeSpan.FromSeconds(3);

    public static bool IsCartridge(string path) => HasExtension(path, ".crt");
    public static bool IsProgram(string path) => HasExtension(path, ".prg");
    public static bool IsD64(string path) => HasExtension(path, ".d64");   // a 1541 disk, for the drive on the IEC cable
    public static bool IsSupported(string path) => IsCartridge(path) || IsProgram(path) || IsD64(path);

    static bool HasExtension(string path, string extension) =>
        string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase);

    // Loads the file into a running machine and returns a description for the title. A BASIC program is RUN.
    // Invalid files throw (IOException, InvalidDataException, NotSupportedException, ArgumentException)
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

        // Loading writes RAM: it must happen on the emulation thread. The file is checked first,
        // so that errors reach whoever opened it
        var (start, end) = PrgFile.GetRange(content);

        // Reading the two bytes of the BASIC start pointer from this thread is harmless: BASIC does not change it
        bool basic = board.IsBasicProgram(start);
        board.Enqueue(() =>
        {
            board.LoadPrg(content);
            if (basic) board.TypeText("RUN\r");
        });
        return basic ? $"{fileName} (BASIC)" : $"{fileName} ${start:X4}-${end - 1:X4}: SYS {start}";
    }

    // Starts a program from scratch: reset, KERNAL boot at full speed, load, then RUN (BASIC, or machine code with
    // a BASIC stub at $0801) or SYS to its first byte. A cartridge is simply inserted, which resets the machine
    public static string Run(Board board, string path)
    {
        if (IsCartridge(path)) return Open(board, path);

        var content = File.ReadAllBytes(path);
        var (start, end) = PrgFile.GetRange(content);
        board.Enqueue(() =>
        {
            board.RemoveCartridge();
            board.Reset();
            board.FastForward(BootTime);
            board.LoadPrg(content);
            board.TypeText(board.IsBasicProgram(start) ? "RUN\r" : $"SYS{start}\r");
        });
        return $"{Path.GetFileName(path)} ${start:X4}-${end - 1:X4}";
    }
}
