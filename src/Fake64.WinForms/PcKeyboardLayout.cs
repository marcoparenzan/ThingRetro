using Fake64.C64;

namespace Fake64.WinForms;

// Mapping between PC keys and C64 keys. It is positional, as in VICE: the symbols to the right of
// the PC's L produce those to the right of the C64's L (":" and ";").
// Some PC keys map to a C64 key pressed together with the left Shift
public static class PcKeyboardLayout
{
    readonly record struct Mapping(C64Key Key, bool Shift = false);

    static readonly Dictionary<Keys, Mapping> Map = BuildMap();

    static Dictionary<Keys, Mapping> BuildMap()
    {
        var map = new Dictionary<Keys, Mapping>();

        for (var key = Keys.A; key <= Keys.Z; key++)
            map[key] = new(Enum.Parse<C64Key>(key.ToString()));
        for (var key = Keys.D0; key <= Keys.D9; key++)
            map[key] = new(Enum.Parse<C64Key>(key.ToString()));

        map[Keys.Space] = new(C64Key.Space);
        map[Keys.Enter] = new(C64Key.Return);
        map[Keys.Back] = new(C64Key.Delete);
        map[Keys.Escape] = new(C64Key.RunStop);

        // Cursor keys: the C64 has only CRSR right and CRSR down, left and up are obtained with Shift
        map[Keys.Right] = new(C64Key.CursorRight);
        map[Keys.Left] = new(C64Key.CursorRight, Shift: true);
        map[Keys.Down] = new(C64Key.CursorDown);
        map[Keys.Up] = new(C64Key.CursorDown, Shift: true);

        // Function keys: F1, F3, F5, F7 are physical keys, F2, F4, F6, F8 are the same with Shift
        map[Keys.F1] = new(C64Key.F1);
        map[Keys.F2] = new(C64Key.F1, Shift: true);
        map[Keys.F3] = new(C64Key.F3);
        map[Keys.F4] = new(C64Key.F3, Shift: true);
        map[Keys.F5] = new(C64Key.F5);
        map[Keys.F6] = new(C64Key.F5, Shift: true);
        map[Keys.F7] = new(C64Key.F7);
        map[Keys.F8] = new(C64Key.F7, Shift: true);

        map[Keys.OemPeriod] = new(C64Key.Period);
        map[Keys.Oemcomma] = new(C64Key.Comma);
        map[Keys.OemSemicolon] = new(C64Key.Colon);
        map[Keys.OemQuotes] = new(C64Key.Semicolon);
        map[Keys.OemQuestion] = new(C64Key.Slash);
        map[Keys.Oemplus] = new(C64Key.Plus);
        map[Keys.OemMinus] = new(C64Key.Minus);
        map[Keys.OemPipe] = new(C64Key.UpArrow);

        return map;
    }

    public static bool IsMapped(Keys key) => key == Keys.ShiftKey || Map.ContainsKey(key);

    // C64 keys pressed for a given set of pressed PC keys (codes without modifiers)
    public static IReadOnlyCollection<C64Key> ToC64Keys(IEnumerable<Keys> pcKeys)
    {
        var keys = new HashSet<C64Key>();
        foreach (var pcKey in pcKeys)
        {
            if (pcKey == Keys.ShiftKey)
                keys.Add(C64Key.LeftShift);
            else if (Map.TryGetValue(pcKey, out var mapping))
            {
                keys.Add(mapping.Key);
                if (mapping.Shift) keys.Add(C64Key.LeftShift);
            }
        }
        return keys;
    }
}
