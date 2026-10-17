using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Thing65.C65;
using Thing65.Mos;

namespace Thing65.WebComputer;

// The C65 driven by the page (wwwroot/js/computer.js). The browser has no thread for the emulation: at every
// animation frame the page calls Frame with the elapsed time and the board runs the matching cycles; at the end of
// each VIC-II frame the picture goes to the page through present(), and every 10 ms of sound through playAudio().
// Keys arrive as KeyboardEvent.code; the joystick takes its keys (keypad, or arrows + Ctrl) and the gamepad
[SupportedOSPlatform("browser")]
public static partial class WebComputer
{
    const long CyclesPerSecond = 985248;
    static readonly TimeSpan BootTime = TimeSpan.FromSeconds(9);   // the memory test and the banner of the ROM

    static BoardC65? board;
    static readonly CanvasScreen screen = new();
    static MonitorCable? monitorCable;
    static AudioCable? audioCable;
    static readonly HashSet<string> pressed = new(StringComparer.Ordinal);

    // A program is started after the disk with it is loaded: its RUN or SYS is typed once the DOS has read the file
    static string? pendingGo;
    static long cycles, goAtCycle;

    [JSImport("present", "computer")]
    internal static partial void Present([JSMarshalAs<JSType.MemoryView>] Span<byte> pixels);

    [JSImport("playAudio", "computer")]
    internal static partial void PlayAudio([JSMarshalAs<JSType.MemoryView>] Span<byte> samples);

    public const string RomName = "c-65-19910828.rom";

    public static bool HasEmbeddedRoms => typeof(WebComputer).Assembly.GetManifestResourceNames().Contains("roms/" + RomName);

    // Debug builds carry the ROM (see the project file); the published site asks the user for it
    public static SystemRom128? EmbeddedRom()
    {
        if (!HasEmbeddedRoms) return null;
        using var stream = typeof(WebComputer).Assembly.GetManifestResourceStream("roms/" + RomName)!;
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return new SystemRom128(copy.ToArray());
    }

    public static void Start(SystemRom128 rom)
    {
        board = new BoardC65(rom, startClock: false);
        monitorCable = new MonitorCable(board.VideoOut, screen);
        cycles = 0;
        SetJoystick(joystickPort, joystickKeys);
    }

    // Runs the cycles for the elapsed time (at most 100 ms, so that a tab coming back does not freeze the page)
    // and returns how many times faster than a real C65 the emulation could run in this frame
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

        if (pendingGo is not null && !board.IsTyping && cycles >= goAtCycle)
        {
            board.TypeText(pendingGo);
            pendingGo = null;
        }
        return speed;
    }

    #region Keyboard and joystick

    [JSExport]
    public static bool KeyDown(string code)
    {
        if (board is null) return false;
        if (JoystickBit(code) is int bit)
        {
            joystickKeyBits |= bit;
            joystickCodes.Add(code);
            UpdateJoystick();
            return true;
        }
        if (!WebKeyboardLayout.IsMapped(code)) return false;
        pressed.Add(code);
        board.Keyboard.SetPressedKeys(WebKeyboardLayout.ToC65Keys(pressed));
        return true;
    }

    [JSExport]
    public static bool KeyUp(string code)
    {
        if (board is null) return false;
        if (joystickCodes.Remove(code))
        {
            joystickKeyBits = joystickCodes.Aggregate(0, (bits, c) => bits | (JoystickBit(c) ?? 0));
            UpdateJoystick();
            return true;
        }
        bool known = pressed.Remove(code);
        board.Keyboard.SetPressedKeys(WebKeyboardLayout.ToC65Keys(pressed));
        return known;
    }

    // The page lost the focus: no key stays pressed
    [JSExport]
    public static void ReleaseKeys()
    {
        pressed.Clear();
        joystickCodes.Clear();
        joystickKeyBits = 0;
        UpdateJoystick();
        board?.Keyboard.ReleaseAll();
    }

    // The joystick: plugged into port 1 or 2 (0 = none), moved by the arrows with Ctrl as fire ("arrows", the
    // default) or by the keypad ("keypad"), and by any gamepad. Its keys do not reach the C65 keyboard
    const int Up = 0x01, Down = 0x02, Left = 0x04, Right = 0x08, Fire = 0x10;
    static readonly Joystick joystick = new();
    static readonly HashSet<string> joystickCodes = new(StringComparer.Ordinal);
    static int joystickPort = 2, joystickKeyBits, gamepadBits;
    static string joystickKeys = "arrows";

    static readonly Dictionary<string, int> KeypadKeys = new()
    {
        ["Numpad8"] = Up, ["Numpad2"] = Down, ["Numpad4"] = Left, ["Numpad6"] = Right,
        ["Numpad7"] = Up | Left, ["Numpad9"] = Up | Right, ["Numpad1"] = Down | Left, ["Numpad3"] = Down | Right,
        ["Numpad0"] = Fire, ["Numpad5"] = Fire,
    };

    static readonly Dictionary<string, int> ArrowKeys = new()
    {
        ["ArrowUp"] = Up, ["ArrowDown"] = Down, ["ArrowLeft"] = Left, ["ArrowRight"] = Right,
        ["ControlLeft"] = Fire, ["ControlRight"] = Fire,
    };

    static int? JoystickBit(string code)
    {
        if (joystickPort == 0) return null;
        var keys = joystickKeys == "arrows" ? ArrowKeys : KeypadKeys;
        return keys.TryGetValue(code, out int bit) ? bit : null;
    }

    [JSExport]
    public static void SetJoystick(int port, string keys)
    {
        joystickPort = port is 1 or 2 ? port : 0;
        joystickKeys = keys == "arrows" ? "arrows" : "keypad";
        joystickCodes.Clear();
        joystickKeyBits = 0;
        joystick.ReleaseAll();
        if (board is null) return;
        if (board.ControlPort1.Device == joystick) board.ControlPort1.Unplug();
        if (board.ControlPort2.Device == joystick) board.ControlPort2.Unplug();
        if (joystickPort == 1) board.ControlPort1.Plug(joystick);
        if (joystickPort == 2) board.ControlPort2.Plug(joystick);
    }

    // Gamepad state read by the page at every frame: bits up, down, left, right, fire
    [JSExport]
    public static void Gamepad(int bits)
    {
        gamepadBits = bits;
        UpdateJoystick();
    }

    static void UpdateJoystick()
    {
        int bits = joystickKeyBits | gamepadBits;
        joystick.SetState((bits & Up) != 0, (bits & Down) != 0, (bits & Left) != 0, (bits & Right) != 0, (bits & Fire) != 0);
    }

    #endregion

    #region Sound

    // Called by the page after a user gesture (browsers start audio only then), with the AudioContext's rate
    [JSExport]
    public static void EnableAudio(int sampleRate)
    {
        if (board is null || audioCable is not null) return;
        audioCable = new AudioCable(board.AudioOut, new WebSpeaker(sampleRate));
    }

    // 16-bit samples, 10 ms at a time, to the page's AudioWorklet
    sealed class WebSpeaker(int sampleRate) : ISpeaker
    {
        public int SampleRate => sampleRate;

        public void Play(ReadOnlySpan<short> samples)
        {
            var writable = MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(samples), samples.Length);
            PlayAudio(MemoryMarshal.AsBytes(writable));
        }
    }

    #endregion

    // The studio's capture of a screen is not available yet for the C65
    [JSExport]
    public static byte[] CaptureVideo() => [];

    // The 64 KB of bank 0, for the studio's memory viewer
    [JSExport]
    public static byte[] CaptureMemory()
    {
        if (board is null) return [];
        var bytes = new byte[0x10000];
        for (int i = 0; i < bytes.Length; i++) bytes[i] = board.Ram(i);
        return bytes;
    }

    [JSExport]
    public static void Reset()
    {
        if (board is null) return;
        pendingGo = null;
        board.Reset();
    }

    // A .d81 goes into the drive; a .prg is put on a disk that is made for it, and loaded and started like the
    // desktop app does (DLOAD + RUN for BASIC, BLOAD + SYS for machine code). Returns a description for the page,
    // or the error
    [JSExport]
    public static string Load(string name, byte[] content)
    {
        if (board is null) return "The C65 is not running";
        try
        {
            if (name.EndsWith(".d81", StringComparison.OrdinalIgnoreCase))
            {
                board.Disk = new D81Image(content);
                return $"{name} (disk)";
            }
            if (!name.EndsWith(".prg", StringComparison.OrdinalIgnoreCase))
                return "Error: only .prg and .d81 files can be loaded";

            if (content.Length < 3) throw new InvalidDataException("A .prg file has at least 3 bytes");
            int start = content[0] | content[1] << 8, end = start + content.Length - 2;
            string diskName = DiskName(name);
            bool basic = start == BasicStart;

            pendingGo = null;
            board.Disk = null;
            board.Reset();
            board.FastForward(BootTime);
            board.Disk = D81Writer.Create(diskName, content);
            board.TypeText(basic ? $"DLOAD\"{diskName}\"\u000D" : $"BLOAD\"{diskName}\",B0\u000D");
            pendingGo = basic ? "RUN\u000D" : $"SYS {start}\u000D";
            goAtCycle = cycles + 5_000_000;   // typing the command and the DOS reading the file
            return basic ? $"{name} (BASIC)" : $"{name} ${start:X4}-${end - 1:X4}";
        }
        catch (Exception e) when (e is ArgumentException or InvalidDataException or NotSupportedException)
        {
            return "Error: " + e.Message;
        }
    }

    const int BasicStart = 0x2001;   // the text of BASIC 10 programs

    // The name on the disk: the file name in capitals, at most 16 characters, letters and digits
    static string DiskName(string file)
    {
        var name = new string(Path.GetFileNameWithoutExtension(file).ToUpperInvariant().Where(char.IsAsciiLetterOrDigit).Take(16).ToArray());
        return name.Length == 0 ? "PROGRAM" : name;
    }

    // The monitor: the VIC-II renders into an unmanaged buffer (32-bit BGRA), which the page copies to the canvas
    sealed unsafe class CanvasScreen : IScreen
    {
        const int Size = CSG4567.SCREEN_WIDTH * CSG4567.SCREEN_HEIGHT * 4;
        readonly byte* pixels = (byte*)NativeMemory.AllocZeroed(Size);

        public byte* BeginRender() => pixels;

        public void EndRender() => Present(new Span<byte>(pixels, Size));
    }
}
