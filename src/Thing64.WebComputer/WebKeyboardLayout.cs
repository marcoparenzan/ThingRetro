using Thing64.C64;

namespace Thing64.WebComputer;

// Mapping between browser keys (KeyboardEvent.code, the physical position) and C64 keys. Positional as in VICE and
// as in the desktop app (PcKeyboardLayout): the symbols right of the PC's L give those right of the C64's L.
// Some PC keys map to a C64 key pressed together with the left Shift
public static class WebKeyboardLayout
{
    readonly record struct Mapping(C64Key Key, bool Shift = false);

    static readonly Dictionary<string, Mapping> Map = BuildMap();

    static Dictionary<string, Mapping> BuildMap()
    {
        var map = new Dictionary<string, Mapping>(StringComparer.Ordinal);

        for (char letter = 'A'; letter <= 'Z'; letter++)
            map["Key" + letter] = new(Enum.Parse<C64Key>(letter.ToString()));
        for (char digit = '0'; digit <= '9'; digit++)
            map["Digit" + digit] = new(Enum.Parse<C64Key>("D" + digit));

        map["Space"] = new(C64Key.Space);
        map["Enter"] = new(C64Key.Return);
        map["Backspace"] = new(C64Key.Delete);
        map["Escape"] = new(C64Key.RunStop);
        map["ShiftLeft"] = new(C64Key.LeftShift);
        map["ShiftRight"] = new(C64Key.RightShift);
        map["ControlLeft"] = new(C64Key.Control);
        map["Tab"] = new(C64Key.Control);
        map["AltLeft"] = new(C64Key.Commodore);
        map["Home"] = new(C64Key.Home);

        // Cursor keys: the C64 has only CRSR right and CRSR down, left and up are obtained with Shift
        map["ArrowRight"] = new(C64Key.CursorRight);
        map["ArrowLeft"] = new(C64Key.CursorRight, Shift: true);
        map["ArrowDown"] = new(C64Key.CursorDown);
        map["ArrowUp"] = new(C64Key.CursorDown, Shift: true);

        // Function keys: F1, F3, F5, F7 are physical keys, F2, F4, F6, F8 are the same with Shift
        map["F1"] = new(C64Key.F1);
        map["F2"] = new(C64Key.F1, Shift: true);
        map["F3"] = new(C64Key.F3);
        map["F4"] = new(C64Key.F3, Shift: true);
        map["F5"] = new(C64Key.F5);
        map["F6"] = new(C64Key.F5, Shift: true);
        map["F7"] = new(C64Key.F7);
        map["F8"] = new(C64Key.F7, Shift: true);

        map["Period"] = new(C64Key.Period);
        map["Comma"] = new(C64Key.Comma);
        map["Semicolon"] = new(C64Key.Colon);
        map["Quote"] = new(C64Key.Semicolon);
        map["Slash"] = new(C64Key.Slash);
        map["Equal"] = new(C64Key.Plus);
        map["Minus"] = new(C64Key.Minus);
        map["Backslash"] = new(C64Key.UpArrow);
        map["BracketLeft"] = new(C64Key.At);
        map["BracketRight"] = new(C64Key.Asterisk);
        map["Backquote"] = new(C64Key.LeftArrow);

        return map;
    }

    public static bool IsMapped(string code) => Map.ContainsKey(code);

    // C64 keys pressed for a given set of pressed browser keys
    public static IReadOnlyCollection<C64Key> ToC64Keys(IEnumerable<string> codes)
    {
        var keys = new HashSet<C64Key>();
        foreach (var code in codes)
        {
            if (!Map.TryGetValue(code, out var mapping)) continue;
            keys.Add(mapping.Key);
            if (mapping.Shift) keys.Add(C64Key.LeftShift);
        }
        return keys;
    }
}
