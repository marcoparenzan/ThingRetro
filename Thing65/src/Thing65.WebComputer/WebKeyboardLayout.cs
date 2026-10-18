using Thing65.C65;

namespace Thing65.WebComputer;

// Mapping between browser keys (KeyboardEvent.code, the physical position) and C65 keys. Positional as in VICE and
// as in the desktop app (PcKeyboardLayout): the symbols right of the PC's L give those right of the C65's L.
// Some PC keys map to a C65 key pressed together with the left Shift
public static class WebKeyboardLayout
{
    readonly record struct Mapping(C65Key Key, bool Shift = false);

    static readonly Dictionary<string, Mapping> Map = BuildMap();

    static Dictionary<string, Mapping> BuildMap()
    {
        var map = new Dictionary<string, Mapping>(StringComparer.Ordinal);

        for (char letter = 'A'; letter <= 'Z'; letter++)
            map["Key" + letter] = new(Enum.Parse<C65Key>(letter.ToString()));
        for (char digit = '0'; digit <= '9'; digit++)
            map["Digit" + digit] = new(Enum.Parse<C65Key>("D" + digit));

        map["Space"] = new(C65Key.Space);
        map["Enter"] = new(C65Key.Return);
        map["Backspace"] = new(C65Key.Delete);
        map["Escape"] = new(C65Key.RunStop);
        map["ShiftLeft"] = new(C65Key.LeftShift);
        map["ShiftRight"] = new(C65Key.RightShift);
        map["ControlLeft"] = new(C65Key.Control);
        map["Tab"] = new(C65Key.Control);
        map["AltLeft"] = new(C65Key.Commodore);
        map["Home"] = new(C65Key.Home);

        // Cursor keys: the C65 has only CRSR right and CRSR down, left and up are obtained with Shift
        map["ArrowRight"] = new(C65Key.CursorRight);
        map["ArrowLeft"] = new(C65Key.CursorRight, Shift: true);
        map["ArrowDown"] = new(C65Key.CursorDown);
        map["ArrowUp"] = new(C65Key.CursorDown, Shift: true);

        // Function keys: F1, F3, F5, F7 are physical keys, F2, F4, F6, F8 are the same with Shift
        map["F1"] = new(C65Key.F1);
        map["F2"] = new(C65Key.F1, Shift: true);
        map["F3"] = new(C65Key.F3);
        map["F4"] = new(C65Key.F3, Shift: true);
        map["F5"] = new(C65Key.F5);
        map["F6"] = new(C65Key.F5, Shift: true);
        map["F7"] = new(C65Key.F7);
        map["F8"] = new(C65Key.F7, Shift: true);

        map["Period"] = new(C65Key.Period);
        map["Comma"] = new(C65Key.Comma);
        map["Semicolon"] = new(C65Key.Colon);
        map["Quote"] = new(C65Key.Semicolon);
        map["Slash"] = new(C65Key.Slash);
        map["Equal"] = new(C65Key.Plus);
        map["Minus"] = new(C65Key.Minus);
        map["Backslash"] = new(C65Key.UpArrow);
        map["BracketLeft"] = new(C65Key.At);
        map["BracketRight"] = new(C65Key.Asterisk);
        map["Backquote"] = new(C65Key.LeftArrow);

        return map;
    }

    public static bool IsMapped(string code) => Map.ContainsKey(code);

    // C65 keys pressed for a given set of pressed browser keys
    public static IReadOnlyCollection<C65Key> ToC65Keys(IEnumerable<string> codes)
    {
        var keys = new HashSet<C65Key>();
        foreach (var code in codes)
        {
            if (!Map.TryGetValue(code, out var mapping)) continue;
            keys.Add(mapping.Key);
            if (mapping.Shift) keys.Add(C65Key.LeftShift);
        }
        return keys;
    }
}
