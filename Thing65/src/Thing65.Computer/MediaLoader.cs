using Thing65.C65;
using ThingCommodore.Software;

namespace Thing65.Computer;

// Opening the files the app supports: .d81 disk images and .prg programs. The board (Thing65.C65) knows neither
// format: the file is read here and the result is handed to the board. A program reaches the C65 the way the
// C65 expects: on a disk. The app makes a disk with the program on it, puts it in the internal drive and types the
// DLOAD (BASIC programs) or BLOAD ...,B0 and SYS (machine code) on the keyboard. Machine code must be below $2000: in C65
// mode the BASIC ROM is mapped over $2000-$7FFF
public static class MediaLoader
{
    // Time left to the ROM to reach READY. before the program is loaded
    public static readonly TimeSpan BootTime = TimeSpan.FromSeconds(9);

    // The load address of BASIC 10 programs (the text starts at $2001 in bank 0)
    public const int BasicStart = 0x2001;

    public static bool IsDisk(string path) => HasExtension(path, ".d81");
    public static bool IsProgram(string path) => HasExtension(path, ".prg");
    public static bool IsD64(string path) => HasExtension(path, ".d64");   // a 1541 disk, for the external drive
    public static bool IsSupported(string path) => IsDisk(path) || IsProgram(path) || IsD64(path);

    static bool HasExtension(string path, string extension) =>
        string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase);

    // Puts a disk image in the drive of a running machine and returns a description for the title.
    // A program is started from scratch (see Run). Invalid files throw (IOException, InvalidDataException, ArgumentException)
    public static string Open(BoardC65 board, string path, DiskSlot slot)
    {
        if (!IsDisk(path)) return Run(board, path, slot);

        var image = new D81Image(File.ReadAllBytes(path));
        board.Enqueue(() =>
        {
            slot.Set(image, path); // the disk that was in is written back to its file
            board.Disk = image;
        });
        return $"{Path.GetFileName(path)} (disk)";
    }

    // Starts a program or a disk from scratch: reset, boot at full speed, then (program) a disk with it is inserted
    // and loaded, and RUN or SYS typed
    public static string Run(BoardC65 board, string path, DiskSlot slot)
    {
        if (IsDisk(path))
        {
            var image = new D81Image(File.ReadAllBytes(path));
            board.Enqueue(() =>
            {
                slot.Set(image, path);
                board.Disk = image;
                board.Reset();
                board.FastForward(BootTime);
            });
            return $"{Path.GetFileName(path)} (disk)";
        }

        var content = File.ReadAllBytes(path);
        var (start, end) = PrgLoader.GetRange(content);
        string name = DiskName(path);
        var disk = D81Writer.Create(name, content);
        string load = start == BasicStart ? $"DLOAD\"{name}\"\r" : $"BLOAD\"{name}\",B0\r"; // B0: into the RAM of bank 0
        string go = start == BasicStart ? "RUN\r" : $"SYS {start}\r";

        board.Enqueue(() =>
        {
            slot.Set(null, null);
            board.Disk = null;
            board.Reset();
            board.FastForward(BootTime);
            slot.Set(disk, null); // made for the program: it has no file
            board.Disk = disk;
            board.TypeText(load);
            while (board.IsTyping) board.RunCycles(1000);
            board.RunCycles(3_000_000); // the DOS reads the file
            board.TypeText(go);
        });
        return start == BasicStart ? $"{Path.GetFileName(path)} (BASIC)" : $"{Path.GetFileName(path)} ${start:X4}-${end - 1:X4}";
    }

    // The name on the disk: the file name in capitals, at most 16 characters, letters and digits
    internal static string DiskName(string path)
    {
        var name = new string(Path.GetFileNameWithoutExtension(path).ToUpperInvariant().Where(char.IsAsciiLetterOrDigit).Take(16).ToArray());
        return name.Length == 0 ? "PROGRAM" : name;
    }
}
