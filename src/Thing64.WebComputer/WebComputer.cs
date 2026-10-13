using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Thing64.C64;
using Thing64.Crt;
using Thing64.Mos;
using Thing64.Prg;

namespace Thing64.WebComputer;

// The C64 driven by the page (wwwroot/js/computer.js). The browser has no thread for the emulation: at every
// animation frame the page calls Frame with the elapsed time and the board runs the matching cycles; at the end of
// each VIC-II frame the picture goes to the page through present(). Keys arrive as KeyboardEvent.code
[SupportedOSPlatform("browser")]
public static partial class WebComputer
{
    const long CyclesPerSecond = 985248;
    static readonly TimeSpan BootTime = TimeSpan.FromSeconds(2.5);

    static Board? board;
    static readonly CanvasScreen screen = new();
    static MonitorCable? cable;
    static readonly HashSet<string> pressed = new(StringComparer.Ordinal);

    // A program waits for the KERNAL to reach READY. before it is loaded (the boot runs in real time, a frame at a time)
    static byte[]? pendingProgram;
    static long cycles, loadAtCycle;

    [JSImport("present", "computer")]
    internal static partial void Present([JSMarshalAs<JSType.MemoryView>] Span<byte> pixels);

    public static bool HasEmbeddedRoms => typeof(WebComputer).Assembly.GetManifestResourceNames().Contains("roms/kernal");

    // Debug builds carry the ROMs (see the project file); the published site asks the user for them
    public static SystemRoms? EmbeddedRoms()
    {
        if (!HasEmbeddedRoms) return null;
        byte[] Read(string name)
        {
            using var stream = typeof(WebComputer).Assembly.GetManifestResourceStream("roms/" + name)!;
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return copy.ToArray();
        }
        return new SystemRoms(kernal: Read("kernal"), basic: Read("basic"), chargen: Read("chargen"));
    }

    public static void Start(SystemRoms roms)
    {
        board = new Board(roms, startClock: false);
        cable = new MonitorCable(board.VideoOut, screen);
        cycles = 0;
    }

    // Runs the cycles for the elapsed time (at most 100 ms, so that a tab coming back does not freeze the page)
    // and returns how many times faster than a real C64 the emulation could run in this frame
    [JSExport]
    public static double Frame(double milliseconds)
    {
        if (board is null) return 0;
        long count = (long)(Math.Min(milliseconds, 100) * CyclesPerSecond / 1000);
        if (count <= 0) return 0;
        var clock = Stopwatch.StartNew();
        board.RunCycles(count);
        cycles += count;
        double speed = count / (double)CyclesPerSecond / Math.Max(clock.Elapsed.TotalSeconds, 1e-6);

        if (pendingProgram is not null && cycles >= loadAtCycle)
        {
            var program = pendingProgram;
            pendingProgram = null;
            var (start, _) = board.LoadPrg(program);
            board.TypeText(board.IsBasicProgram(start) ? "RUN\r" : $"SYS{start}\r");
        }
        return speed;
    }

    [JSExport]
    public static bool KeyDown(string code)
    {
        if (board is null || !WebKeyboardLayout.IsMapped(code)) return false;
        pressed.Add(code);
        board.Keyboard.SetPressedKeys(WebKeyboardLayout.ToC64Keys(pressed));
        return true;
    }

    [JSExport]
    public static bool KeyUp(string code)
    {
        if (board is null) return false;
        bool known = pressed.Remove(code);
        board.Keyboard.SetPressedKeys(WebKeyboardLayout.ToC64Keys(pressed));
        return known;
    }

    // The page lost the focus: no key stays pressed
    [JSExport]
    public static void ReleaseKeys()
    {
        pressed.Clear();
        board?.Keyboard.ReleaseAll();
    }

    [JSExport]
    public static void Reset()
    {
        if (board is null) return;
        pendingProgram = null;
        board.RemoveCartridge();
        board.Reset();
    }

    // A .prg restarts the machine and runs after the boot; a .crt is inserted (which restarts the machine).
    // Returns a description for the page, or the error
    [JSExport]
    public static string Load(string name, byte[] content)
    {
        if (board is null) return "The C64 is not running";
        try
        {
            if (name.EndsWith(".crt", StringComparison.OrdinalIgnoreCase))
            {
                var cartridge = CrtFile.Load(content);
                pendingProgram = null;
                board.InsertCartridge(cartridge);
                return $"{(string.IsNullOrEmpty(cartridge.Name) ? name : cartridge.Name)} ({cartridge.TypeName})";
            }
            var (start, end) = PrgFile.GetRange(content);
            board.RemoveCartridge();
            board.Reset();
            pendingProgram = content;
            loadAtCycle = cycles + (long)(BootTime.TotalSeconds * CyclesPerSecond);
            return $"{name} ${start:X4}-${end - 1:X4}";
        }
        catch (Exception e) when (e is ArgumentException or InvalidDataException or NotSupportedException)
        {
            return "Error: " + e.Message;
        }
    }

    // The monitor: the VIC-II renders into an unmanaged buffer (32-bit BGRA), which the page copies to the canvas
    sealed unsafe class CanvasScreen : IScreen
    {
        const int Size = MOS6569.SCREEN_WIDTH * MOS6569.SCREEN_HEIGHT * 4;
        readonly byte* pixels = (byte*)NativeMemory.AllocZeroed(Size);

        public byte* BeginRender() => pixels;

        public void EndRender() => Present(new Span<byte>(pixels, Size));
    }
}
