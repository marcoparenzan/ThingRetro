namespace Thing65.C65;

// Types text on the keyboard matrix: for each character it presses the key (with Shift if needed), holds it for a
// while and releases it, so that the ROM's keyboard scan, which looks at the matrix about 60 times per second, sees
// every key like a person typing. Letters are typed as the unshifted key (the machine shows them in capitals)
public static class KeyTyper
{
    // One step of the typing: the keys pressed for a number of cycles (no keys = a pause between two characters)
    public readonly record struct Step(C65Key[] Keys, int Cycles);

    public const int HoldCycles = 60_000;    // about 60 ms
    public const int ReleaseCycles = 60_000;

    // Keys that type a character, and whether Shift goes with them
    static readonly Dictionary<char, (C65Key Key, bool Shift)> Map = BuildMap();

    static Dictionary<char, (C65Key, bool)> BuildMap()
    {
        var map = new Dictionary<char, (C65Key, bool)>();
        for (char c = 'A'; c <= 'Z'; c++) map[c] = (Enum.Parse<C65Key>(c.ToString()), false);
        for (char c = '0'; c <= '9'; c++) map[c] = (Enum.Parse<C65Key>("D" + c), false);

        map[' '] = (C65Key.Space, false);
        map['\r'] = (C65Key.Return, false);
        map['\n'] = (C65Key.Return, false);
        map[','] = (C65Key.Comma, false);
        map['.'] = (C65Key.Period, false);
        map[':'] = (C65Key.Colon, false);
        map[';'] = (C65Key.Semicolon, false);
        map['='] = (C65Key.Equals, false);
        map['+'] = (C65Key.Plus, false);
        map['-'] = (C65Key.Minus, false);
        map['*'] = (C65Key.Asterisk, false);
        map['/'] = (C65Key.Slash, false);
        map['@'] = (C65Key.At, false);
        map['<'] = (C65Key.Comma, true);
        map['>'] = (C65Key.Period, true);
        map['?'] = (C65Key.Slash, true);
        map['"'] = (C65Key.D2, true);
        map['!'] = (C65Key.D1, true);
        map['#'] = (C65Key.D3, true);
        map['$'] = (C65Key.D4, true);
        map['%'] = (C65Key.D5, true);
        map['&'] = (C65Key.D6, true);
        map['\''] = (C65Key.D7, true);
        map['('] = (C65Key.D8, true);
        map[')'] = (C65Key.D9, true);
        map['['] = (C65Key.Colon, true);
        map[']'] = (C65Key.Semicolon, true);
        return map;
    }

    public static bool CanType(char c) => Map.ContainsKey(char.ToUpperInvariant(c));

    // The steps that type the text; characters that cannot be typed are skipped
    public static IEnumerable<Step> Steps(string text)
    {
        foreach (char character in text)
        {
            if (!Map.TryGetValue(char.ToUpperInvariant(character), out var entry)) continue;
            yield return new Step(entry.Shift ? [entry.Key, C65Key.LeftShift] : [entry.Key], HoldCycles);
            yield return new Step([], ReleaseCycles);
        }
    }
}
