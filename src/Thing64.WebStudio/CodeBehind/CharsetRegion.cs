using Thing64.Graphics;

namespace Thing64.WebStudio.CodeBehind;

// What a ;@ charset region means: the bitmaps of characters, 8 bytes each, from screen code "first", under the first
// label of the region (the charset's name, which ;@ screen regions reference with charset=):
//   ;@ charset first=$70
//   charset1__characters:   .byte $FF,$57,... ; $70 wall A
//   charset1__charactersEnd:
//   ;@ end
// A character has no color: the screens that use it give it one per cell, and decide whether it is hires or
// multicolor. So the editor shows each character as the screens use it, with their background and multicolors
public sealed class CharsetRegion
{
    public string Label { get; }
    public int First { get; }
    public int Characters { get; }
    public C64Palette Palette { get; } = C64Palette.Pepto;

    public CharsetRegion(AsmRegion region, string[] lines, ProgramImage? image, int values)
    {
        Label = region.Labels.FirstOrDefault() ?? throw new FormatException(";@ charset: the region needs a label, the charset's name");
        First = (region.Parameters.TryGetValue("first", out var first) ? ProgramImage.Value(first, image) : 0) & 0xFF;
        Characters = values / 8;
    }

    public int[] Decode(IReadOnlyList<int> values, int character, bool multicolor)
    {
        int across = multicolor ? 4 : 8, bits = multicolor ? 2 : 1;
        var codes = new int[across * 8];
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < across; x++)
                codes[y * across + x] = values[character * 8 + y] >> (8 - bits * (x + 1)) & ((1 << bits) - 1);
        return codes;
    }

    public void Encode(int[] values, int character, bool multicolor, int[] codes)
    {
        int across = multicolor ? 4 : 8, bits = multicolor ? 2 : 1;
        for (int y = 0; y < 8; y++)
        {
            int value = 0;
            for (int x = 0; x < across; x++) value |= codes[y * across + x] << (8 - bits * (x + 1));
            values[character * 8 + y] = value;
        }
    }

    public RgbImage Render(IReadOnlyList<int> values, int character, Look look, int scale)
    {
        var image = new RgbImage(8 * scale, 8 * scale);
        var codes = Decode(values, character, look.Multicolor);
        int across = look.Multicolor ? 4 : 8, width = 8 / across;
        for (int y = 0; y < 8 * scale; y++)
            for (int x = 0; x < 8 * scale; x++)
                image[x, y] = Palette.Argb(look.ColorOf(codes[y / scale * across + x / scale / width]));
        return image;
    }
}

// How a character looks: the colors of a screen that uses it, and its cell color there (bit 3: multicolor)
public sealed record Look(int Background, int Multicolor1, int Multicolor2, bool MulticolorMode, int Color)
{
    public bool Multicolor => MulticolorMode && (Color & 8) != 0;

    public int ColorOf(int code) => Multicolor
        ? code switch { 0 => Background, 1 => Multicolor1, 2 => Multicolor2, _ => Color & 7 }
        : code == 0 ? Background : MulticolorMode ? Color & 7 : Color & 15;
}

// The ;@ screen regions of the project that use a charset: how its characters are used, and their screen codes
// renumbered when characters move
public static class CharsetUsers
{
    public sealed record Screen(string File, AsmRegion Region, ScreenRegion Model, List<int> Values);

    public static List<Screen> Find(Project project, string charsetLabel, ProgramImage image)
    {
        var screens = new List<Screen>();
        foreach (string file in Directory.EnumerateFiles(project.Folder, "*.asm", SearchOption.AllDirectories))
        {
            if (Path.GetRelativePath(project.Folder, file).Split(Path.DirectorySeparatorChar).Any(p => p.StartsWith('.'))) continue;
            string[] lines = File.ReadAllText(file).Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
            if (!lines.Any(l => l.Contains(";@"))) continue;
            foreach (var region in AsmRegion.Find(lines))
                if (region.Editor == "screen" && region.Parameters.GetValueOrDefault("charset") == charsetLabel)
                    screens.Add(new Screen(file, region, new ScreenRegion(region, lines, image), region.ReadBytes(lines, image)));
        }
        return screens;
    }

    // Each character's look: the color its cells have most often in the first screen that uses it
    public static Look[] Looks(List<Screen> screens, int first, int characters)
    {
        var defaultLook = screens.FirstOrDefault()?.Model is { } m
            ? new Look(m.Background, m.Multicolor1, m.Multicolor2, m.MulticolorMode, 1)
            : new Look(6, 11, 15, false, 1);
        var looks = Enumerable.Repeat(defaultLook, characters).ToArray();
        var counts = new Dictionary<(int Character, int Color), int>();
        foreach (var screen in screens)
            for (int cell = 0; cell < screen.Model.Cells; cell++)
            {
                int character = screen.Values[screen.Model.ScreenAt + cell] - first;
                if (character < 0 || character >= characters) continue;
                var key = (character, screen.Values[screen.Model.ColorsAt + cell] & 15);
                counts[key] = counts.GetValueOrDefault(key) + 1;
            }
        foreach (var group in counts.GroupBy(c => c.Key.Character))
            looks[group.Key] = defaultLook with { Color = group.MaxBy(c => c.Value).Key.Color };
        return looks;
    }

    // Where a character is used: "file(line)" of each screen
    public static List<string> Using(Project project, List<Screen> screens, int code) =>
        screens.Where(s => Enumerable.Range(0, s.Model.Cells).Any(c => s.Values[s.Model.ScreenAt + c] == code))
            .Select(s => $"{project.RelativePath(s.File)}({s.Region.StartLine + 1})").ToList();

    // The screen codes of the custom characters renumbered (old index -> new index) in every screen
    public static void Renumber(List<Screen> screens, int first, IReadOnlyDictionary<int, int> moved)
    {
        foreach (var group in screens.GroupBy(s => s.File))
        {
            string text = File.ReadAllText(group.Key);
            string newline = text.Contains("\r\n") ? "\r\n" : "\n";
            string[] lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
            foreach (var screen in group.OrderByDescending(s => s.Region.StartLine))
            {
                var region = AsmRegion.Find(lines).First(r => r.StartLine == screen.Region.StartLine);
                var values = screen.Values.ToList();
                for (int cell = 0; cell < screen.Model.Cells; cell++)
                {
                    int at = screen.Model.ScreenAt + cell;
                    if (moved.TryGetValue(values[at] - first, out int now)) values[at] = first + now;
                }
                lines = region.WriteBytes(lines, values, screen.Model.Width);
            }
            File.WriteAllText(group.Key, string.Join(newline, lines));
        }
    }
}
