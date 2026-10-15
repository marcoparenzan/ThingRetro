using System.Globalization;
using System.Text;

namespace Thing64.Graphics;

// A screen taken from the C64 and written against the shared charset: its screen codes are the charset's, not the
// ones the running program used
public sealed record CapturedScreen(string Name, VideoCapture Source, int[] Codes)
{
    public int[] Colors => Enumerable.Range(0, VideoCapture.Cells).Select(Source.Color).ToArray();
}

// The charset that successive captures share. Each capture adds the characters its screen uses that the charset does
// not have yet (a character is its 8 bytes: the same bytes are the same character), the codes of the screens are
// the charset's own, from "First" on, and the whole thing is written as the files of a Thing64 project:
//   ;@ charset first=$00 multicolor=1      the characters, one per line
//   ;@ screen ...                          the screen codes and the colors, 40x25
// The charset can start from one a project already has (Seed): its characters keep their codes and the new ones
// follow, so what is written replaces that file. The blank characters at its end are free places: a charset that
// holds all 256 characters with the last ones empty takes the new characters there and keeps its size
public sealed class CapturedCharset
{
    readonly List<byte[]> characters = [];
    readonly List<string> descriptions = [];
    readonly Dictionary<string, int> index = [];
    readonly List<CapturedScreen> screens = [];
    readonly Dictionary<string, (bool Hires, bool Multicolor)> usage = [];     // by screen: how its characters are used
    bool seeded, seedMulticolor;
    int free;          // the characters from here on are free places (blank ones at the end of a charset it started from)

    public CapturedCharset(string name, int first = 0)
    {
        Name = Identifier(name);
        First = first;
    }

    public string Name { get; }

    // The screen code of the first character
    public int First { get; }

    public IReadOnlyList<byte[]> Characters => characters;
    public IReadOnlyList<string> Descriptions => descriptions;
    public IReadOnlyList<CapturedScreen> Screens => screens;

    public string Label => Name + "__characters";

    // The characters of a charset the project already has; multicolor: its region says multicolor=1
    public void Seed(IEnumerable<(byte[] Bytes, string Description)> existing, bool multicolor)
    {
        if (screens.Count > 0) throw new InvalidOperationException("A charset starts from another one before the first capture");
        characters.Clear();
        descriptions.Clear();
        index.Clear();
        foreach (var (bytes, description) in existing)
        {
            if (First + characters.Count > 255) throw new InvalidOperationException($"The charset has more than {256 - First} characters from ${First:X2}");
            characters.Add(bytes);
            descriptions.Add(description);
        }
        free = characters.Count;
        while (free > 0 && characters[free - 1].All(b => b == 0)) free--;
        for (int i = 0; i < free; i++) index.TryAdd(Key(characters[i]), i);     // the free places are not there to be found
        seeded = characters.Count > 0;
        seedMulticolor = multicolor;
    }

    // Takes a screen: adds the characters it uses, and returns it with the codes of this charset
    public CapturedScreen Add(string screenName, VideoCapture capture)
    {
        screenName = Identifier(screenName);
        if (screens.Any(s => s.Name == screenName)) throw new ArgumentException($"There is already a screen called {screenName}");

        // The characters of the screen that are new, in the order the screen shows them
        var added = new List<(string Key, byte[] Bytes, int Code)>();
        var pending = new HashSet<string>();
        for (int cell = 0; cell < VideoCapture.Cells; cell++)
        {
            int code = capture.Code(cell);
            var bytes = capture.Character(code);
            if (Find(code, bytes) < 0 && pending.Add(Key(bytes))) added.Add((Key(bytes), bytes, code));
        }
        int length = Math.Max(characters.Count, free + added.Count);
        if (First + length > 256)
            throw new InvalidOperationException($"The charset would have {length} characters, from ${First:X2}: only {256 - First} fit");

        foreach (var (key, bytes, code) in added)
        {
            index[key] = free;
            string description = $"screen code ${code:X2} of {screenName}";
            if (free < characters.Count) { characters[free] = bytes; descriptions[free] = description; }
            else { characters.Add(bytes); descriptions.Add(description); }
            free++;
        }

        var codes = new int[VideoCapture.Cells];
        bool hires = false, multicolor = false;
        for (int cell = 0; cell < codes.Length; cell++)
        {
            var bytes = capture.Character(capture.Code(cell));
            codes[cell] = First + Find(capture.Code(cell), bytes);
            if (IsNeutral(bytes)) continue;       // blank and solid look the same hires or multicolor
            if (capture.IsMulticolorCell(cell)) multicolor = true; else hires = true;
        }

        var screen = new CapturedScreen(screenName, capture, codes);
        screens.Add(screen);
        usage[screenName] = (hires, multicolor);
        return screen;
    }

    // Every character of the charset is multicolor: the screens use them only in multicolor cells (a blank or a
    // solid character does not count: it looks the same both ways), or the charset the captures started from said so
    public bool Multicolor => characters.Count > 0 && !usage.Values.Any(u => u.Hires) &&
                              (seeded ? seedMulticolor : usage.Values.Any(u => u.Multicolor));

    // Removes a screen. Its characters stay: the others may use them, and the codes of the screens do not move
    public void Remove(string screenName)
    {
        screens.RemoveAll(s => s.Name == screenName);
        usage.Remove(screenName);
    }

    public void Rename(string screenName, string newName)
    {
        newName = Identifier(newName);
        if (newName == screenName) return;
        if (screens.Any(s => s.Name == newName)) throw new ArgumentException($"There is already a screen called {newName}");
        int at = screens.FindIndex(s => s.Name == screenName);
        if (at < 0) throw new ArgumentException($"There is no screen called {screenName}");
        screens[at] = screens[at] with { Name = newName };
        usage[newName] = usage[screenName];
        usage.Remove(screenName);
    }

    // ----------------------------------------------------------------------------------------------- the files

    public string CharsetFileName => Name + ".asm";

    public string CharsetText()
    {
        var text = new StringBuilder();
        int last = First + characters.Count - 1;
        text.AppendLine($"; {Name}: {characters.Count} characters taken from the C64" +
                        (characters.Count > 0 ? $", screen codes ${First:X2}-${last:X2}." : "."));
        text.AppendLine($"; The screens that use it name it: charset={Label} first={Name}__FIRST");
        text.AppendLine();
        text.AppendLine($"{Name}__FIRST = ${First:X2}                    ; the screen code of its first character");
        text.AppendLine();
        text.AppendLine($";@ charset first={Name}__FIRST{(Multicolor ? " multicolor=1" : "")}");
        text.AppendLine($"{Label}:");
        for (int i = 0; i < characters.Count; i++)
            text.AppendLine($"            .byte {string.Join(",", characters[i].Select(b => $"${b:X2}"))} ; ${First + i:X2} {descriptions[i]}");
        text.AppendLine($"{Name}__charactersEnd:");
        text.AppendLine(";@ end");
        return text.ToString();
    }

    public string ScreenFileName(CapturedScreen screen) => screen.Name + ".asm";

    public string ScreenText(CapturedScreen screen)
    {
        var capture = screen.Source;
        var text = new StringBuilder();
        text.AppendLine($"; {screen.Name}: a screen taken from the C64 ({VideoCapture.Columns}x{VideoCapture.Rows}), with the characters of {Name}.");
        text.AppendLine("; The colors the program had set in the VIC-II: the program sets them again when it shows the screen.");
        text.AppendLine();
        text.AppendLine($"{screen.Name}__BACKGROUND   = {capture.Background}");
        text.AppendLine($"{screen.Name}__BORDER       = {capture.Border}");
        if (capture.MulticolorMode)
        {
            text.AppendLine($"{screen.Name}__MULTICOLOR_1 = ${capture.Multicolor1:X2}");
            text.AppendLine($"{screen.Name}__MULTICOLOR_2 = ${capture.Multicolor2:X2}");
        }
        text.AppendLine();
        text.AppendLine($";@ screen width={VideoCapture.Columns} height={VideoCapture.Rows} screen={screen.Name}__screen colors={screen.Name}__colors");
        text.AppendLine($";@        charset={Label} first={Name}__FIRST");
        text.AppendLine($";@        background={screen.Name}__BACKGROUND" + (capture.MulticolorMode
            ? $" multicolor=1 multicolor1={screen.Name}__MULTICOLOR_1 multicolor2={screen.Name}__MULTICOLOR_2" : ""));
        text.AppendLine($"{screen.Name}__screen:");
        Rows(text, screen.Codes);
        text.AppendLine($"{screen.Name}__colors:");
        Rows(text, screen.Colors);
        text.AppendLine(";@ end");
        return text.ToString();
    }

    static void Rows(StringBuilder text, int[] values)
    {
        for (int row = 0; row < VideoCapture.Rows; row++)
            text.AppendLine("            .byte " + string.Join(",", Enumerable.Range(0, VideoCapture.Columns).Select(c => $"${values[row * VideoCapture.Columns + c]:X2}")) + $" ; row {row}");
    }

    // ------------------------------------------------------------------------------------------------- helpers

    // The index of a character the charset has: the one at the code it had in the program when that is the same
    // character (so a charset the program was built from keeps its codes, duplicates and all), otherwise the first
    // with those bytes; -1 if there is none
    int Find(int code, byte[] bytes)
    {
        int at = code - First;
        if (at >= 0 && at < free && characters[at].AsSpan().SequenceEqual(bytes)) return at;
        return index.TryGetValue(Key(bytes), out int found) ? found : -1;
    }

    static string Key(byte[] bytes) => Convert.ToHexString(bytes);

    static bool IsNeutral(byte[] bytes) => bytes.All(b => b == 0) || bytes.All(b => b == 0xFF);

    // A label: letters, digits and underscores, not starting with a digit
    public static string Identifier(string name)
    {
        var clean = new string(name.Trim().Select(c => char.IsAsciiLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
        if (clean.Length == 0) throw new ArgumentException("A name is needed");
        return char.IsAsciiDigit(clean[0]) ? "_" + clean : clean;
    }
}
