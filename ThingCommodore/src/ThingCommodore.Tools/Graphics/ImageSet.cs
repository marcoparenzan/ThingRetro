using System.Text;
using System.Text.Json;

namespace ThingCommodore.Graphics;

// An image set: several pictures that become screens of characters sharing one charset. It lives in an assembly source, as a
// structured comment that the studio opens with its editor (like the other ";@" regions), followed by what the editor wrote:
//   ;@ imageset name=story first=$00 multicolor=1 columns=40 rows=25
//   ;@          fit=fit resample=box dither=ordered brightness=10
//   ;@          sources=title.png,castle.png
//   ;@ end
//   ; >>> image set story (written by the editor)
//   story__FIRST = $00 ...
//   ;@ charset first=story__FIRST multicolor=1     the characters of all the pictures, each kept once
//   ;@ screen width=40 height=25 ...               one for each picture: its screen codes and colors, 40x25 or less
//   ; <<< image set story
// The background and the two multicolors are shared too, so that the screens can follow one another without the program setting the
// VIC-II again. Colors left out are chosen from all the pictures together
public sealed record ImageSetAsset
{
    public string? Name { get; init; }
    public List<string> Sources { get; init; } = [];

    // The screen code of the first character of the charset (the characters below it are the ROM's)
    public int First { get; init; }

    public bool Multicolor { get; init; } = true;

    // The size of each screen in characters: up to 40x25
    public int? Columns { get; init; }
    public int? Rows { get; init; }

    public Fit Fit { get; init; } = Fit.Fit;
    public Dither Dither { get; init; } = Dither.Ordered;
    public string Palette { get; init; } = "pepto";

    // Downsampling, as in a graphics asset
    public Resample? Resample { get; init; }
    public int? Brightness { get; init; }
    public int? Contrast { get; init; }
    public int? Saturation { get; init; }

    public int? Background { get; init; }
    public int? Multicolor1 { get; init; }
    public int? Multicolor2 { get; init; }

    // At most this many characters in the charset, the ones that were there included (similar ones are merged): by default what fits from "first"
    public int? MaxCharacters { get; init; }

    // Start from the charset already written (name.asm, perhaps edited by hand): its characters stay where they are, the pictures reuse
    // them when they have the same bytes, and the new characters follow. Without it the charset is made again from the pictures
    public bool KeepCharset { get; init; }

    public int CharacterLimit => Math.Clamp(MaxCharacters ?? 256 - First, 1, 256 - First);

    // The asset the converter of single pictures understands
    public GraphicsAsset ToGraphics() => new()
    {
        Mode = Multicolor ? GraphicsMode.MulticolorCharset : GraphicsMode.HiresCharset,
        Fit = Fit, Dither = Dither, Palette = Palette, Resample = Resample, Brightness = Brightness, Contrast = Contrast, Saturation = Saturation,
        Columns = Math.Clamp(Columns ?? 40, 1, 40), Rows = Math.Clamp(Rows ?? 25, 1, 25),
        Background = Background, Multicolor1 = Multicolor1, Multicolor2 = Multicolor2, MaxCharacters = CharacterLimit,
    };

    // The same settings as the parameters of the ";@ imageset" lines: what is left at its default is left out
    public string[] HeaderLines(string fallbackName)
    {
        var first = new List<string>();
        void Add(List<string> list, string name, object? value) { if (value is not null) list.Add($"{name}={value}"); }
        Add(first, "name", Name ?? fallbackName);
        if (First != 0) Add(first, "first", $"${First:X2}");
        Add(first, "multicolor", Multicolor ? 1 : 0);
        Add(first, "columns", Columns);
        Add(first, "rows", Rows);
        var look = new List<string>();
        if (Fit != Fit.Fit) Add(look, "fit", Kebab(Fit));
        if (Resample is { } resample and not Graphics.Resample.Box) Add(look, "resample", Kebab(resample));
        if (Dither != Dither.Ordered) Add(look, "dither", Kebab(Dither));
        if (Palette != "pepto") Add(look, "palette", Palette);
        if (Brightness is { } and not 0) Add(look, "brightness", Brightness);
        if (Contrast is { } and not 0) Add(look, "contrast", Contrast);
        if (Saturation is { } and not 0) Add(look, "saturation", Saturation);
        var colors = new List<string>();
        Add(colors, "background", Background);
        Add(colors, "multicolor1", Multicolor1);
        Add(colors, "multicolor2", Multicolor2);
        Add(colors, "max", MaxCharacters);
        if (KeepCharset) Add(colors, "keep", 1);

        var lines = new List<string> { ";@ imageset " + string.Join(" ", first) };
        foreach (var group in new[] { look, colors })
            if (group.Count > 0) lines.Add(";@          " + string.Join(" ", group));
        if (Sources.Count > 0) lines.Add(";@          sources=" + string.Join(",", Sources.Select(Escape)));
        return [.. lines];
    }

    // The settings from the parameters of the region
    public static ImageSetAsset FromParameters(IReadOnlyDictionary<string, string> parameters)
    {
        string? Text(string name) => parameters.TryGetValue(name, out var value) ? value : null;
        int? Number(string name) => Text(name) is { } text ? ParseNumber(name, text) : null;
        T? Choice<T>(string name) where T : struct, Enum => Text(name) is { } text
            ? Enum.GetValues<T>().Select(v => (T?)v).FirstOrDefault(v => Kebab(v!.Value) == text.ToLowerInvariant())
              ?? throw new FormatException($";@ imageset: {name}={text} is not one of {string.Join(", ", Enum.GetValues<T>().Select(v => Kebab(v)))}")
            : null;
        return new ImageSetAsset
        {
            Name = Text("name"),
            Sources = Text("sources")?.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Unescape).ToList() ?? [],
            First = Number("first") ?? 0,
            Multicolor = (Number("multicolor") ?? 1) != 0,
            Columns = Number("columns"),
            Rows = Number("rows"),
            Fit = Choice<Fit>("fit") ?? Fit.Fit,
            Dither = Choice<Dither>("dither") ?? Dither.Ordered,
            Palette = Text("palette") ?? "pepto",
            Resample = Choice<Resample>("resample"),
            Brightness = Number("brightness"),
            Contrast = Number("contrast"),
            Saturation = Number("saturation"),
            Background = Number("background"),
            Multicolor1 = Number("multicolor1"),
            Multicolor2 = Number("multicolor2"),
            MaxCharacters = Number("max"),
            KeepCharset = (Number("keep") ?? 0) != 0,
        };
    }

    static int ParseNumber(string name, string text)
    {
        bool ok = text.StartsWith('$') ? int.TryParse(text[1..], System.Globalization.NumberStyles.HexNumber, null, out int value) : int.TryParse(text, out value);
        return ok ? value : throw new FormatException($";@ imageset: {name}={text} is not a number");
    }

    static string Kebab<T>(T value) where T : struct, Enum => JsonNamingPolicy.KebabCaseLower.ConvertName(value.ToString());

    // The names of the pictures are separated by commas and the parameters by spaces: those two are written as %2C and %20
    static string Escape(string name) => name.Replace("%", "%25").Replace(",", "%2C").Replace(" ", "%20");
    static string Unescape(string name) => name.Replace("%20", " ").Replace("%2C", ",").Replace("%25", "%");
}

// A picture converted: its screen (codes of the shared charset, colors for the color RAM) and how it will look
public sealed record SetScreen(string Name, string Source, int[] Codes, byte[] Colors, RgbImage Preview);

// The pictures of a set converted together
public sealed class ImageSetConversion
{
    public required string Name { get; init; }
    public required int First { get; init; }
    public required bool Multicolor { get; init; }
    public required int Columns { get; init; }
    public required int Rows { get; init; }
    public required int Background { get; init; }
    public required int Multicolor1 { get; init; }
    public required int Multicolor2 { get; init; }
    public required string Palette { get; init; }
    public required IReadOnlyList<byte[]> Characters { get; init; }
    public required IReadOnlyList<string> Descriptions { get; init; }
    public required IReadOnlyList<SetScreen> Screens { get; init; }
    public required string Summary { get; init; }
    public int Frozen { get; init; }

    public string Label => Name + "__characters";

    // What the editor writes after the ;@ imageset lines: the shared colors, the ;@ charset region and a ;@ screen region for each picture,
    // between two marker lines so that it can be replaced the next time
    public string GeneratedText()
    {
        var text = new StringBuilder();
        text.AppendLine(ImageSetSource.StartMarker(Name));
        text.Append(CharsetText());
        foreach (var screen in Screens)
        {
            text.AppendLine();
            text.Append(ScreenText(screen));
        }
        text.AppendLine(ImageSetSource.EndMarker(Name));
        return text.ToString();
    }

    // The characters as a picture, 16 per row: hires bits in the ink color 15, multicolor codes in the shared colors
    public RgbImage CharsetImage()
    {
        var palette = C64Palette.Named(Palette);
        uint[] colors = Multicolor
            ? [palette.Argb(Background), palette.Argb(Multicolor1), palette.Argb(Multicolor2), palette.Argb(15)]
            : [palette.Argb(Background), palette.Argb(1)];
        int count = Math.Max(1, Characters.Count), across = 16, down = (count + across - 1) / across;
        var image = new RgbImage(across * 9, down * 9);
        Array.Fill(image.Pixels, 0xFF03052Au);
        for (int c = 0; c < Characters.Count; c++)
            for (int row = 0; row < 8; row++)
                for (int x = 0; x < 8; x++)
                {
                    int bits = Characters[c][row];
                    int code = Multicolor ? bits >> (6 - x / 2 * 2) & 3 : bits >> (7 - x) & 1;
                    image[c % across * 9 + x, c / across * 9 + row] = colors[code];
                }
        return image;
    }

    // The shared colors and the ;@ charset region, one character per line with the picture it came from
    public string CharsetText()
    {
        var text = new StringBuilder();
        int last = First + Characters.Count - 1;
        text.AppendLine($"; {Name}: {Characters.Count} characters for {Screens.Count} screen{(Screens.Count == 1 ? "" : "s")} of {Columns}x{Rows}" +
                        (Characters.Count > 0 ? $", screen codes ${First:X2}-${last:X2}." : "."));
        text.AppendLine($"; The screens that use it name it: charset={Label} first={Name}__FIRST. The colors are shared by all of them:");
        text.AppendLine("; the program sets them in the VIC-II once, whichever screen it shows.");
        text.AppendLine();
        text.AppendLine($"{Name}__FIRST       = ${First:X2}                    ; the screen code of its first character");
        text.AppendLine($"{Name}__BACKGROUND  = {Background}");
        if (Multicolor)
        {
            text.AppendLine($"{Name}__MULTICOLOR_1 = ${Multicolor1:X2}");
            text.AppendLine($"{Name}__MULTICOLOR_2 = ${Multicolor2:X2}");
        }
        text.AppendLine();
        text.AppendLine($";@ charset first={Name}__FIRST{(Multicolor ? " multicolor=1" : "")}");
        text.AppendLine($"{Label}:");
        for (int i = 0; i < Characters.Count; i++)
            text.AppendLine($"            .byte {string.Join(",", Characters[i].Select(b => $"${b:X2}"))} ; ${First + i:X2} {Descriptions[i]}".TrimEnd());
        text.AppendLine($"{Name}__charactersEnd:");
        text.AppendLine(";@ end");
        return text.ToString();
    }

    // The ;@ screen region of a picture, with the codes of the charset
    public string ScreenText(SetScreen screen)
    {
        var text = new StringBuilder();
        text.AppendLine($"; {screen.Name}: {screen.Source} as a screen of {Columns}x{Rows} characters, with the characters of {Name}.");
        text.AppendLine();
        text.AppendLine($";@ screen width={Columns} height={Rows} screen={screen.Name}__screen colors={screen.Name}__colors");
        text.AppendLine($";@        charset={Label} first={Name}__FIRST");
        text.AppendLine($";@        background={Name}__BACKGROUND" + (Multicolor ? $" multicolor=1 multicolor1={Name}__MULTICOLOR_1 multicolor2={Name}__MULTICOLOR_2" : ""));
        text.AppendLine($"{screen.Name}__screen:");
        WriteRows(text, screen.Codes.Select(c => First + c).ToArray());
        text.AppendLine($"{screen.Name}__colors:");
        WriteRows(text, screen.Colors.Select(c => (int)c).ToArray());
        text.AppendLine(";@ end");
        return text.ToString();
    }

    void WriteRows(StringBuilder text, int[] values)
    {
        for (int row = 0; row < Rows; row++)
            text.AppendLine("            .byte " + string.Join(",", Enumerable.Range(0, Columns).Select(c => $"${values[row * Columns + c]:X2}")) + $" ; row {row}");
    }
}

// The image set in the lines of an assembly source: the ;@ imageset lines (the settings) and what the editor wrote after them
public static class ImageSetSource
{
    public static string StartMarker(string name) => $"; >>> image set {name} (written by the editor from the ;@ imageset lines above: the charset and the screens can be edited)";
    public static string EndMarker(string name) => $"; <<< image set {name}";

    static bool IsStart(string line, string name) => line.StartsWith($"; >>> image set {name} ", StringComparison.Ordinal);
    static bool IsEnd(string line, string name) => line.TrimEnd() == EndMarker(name);

    public static bool HasGenerated(IReadOnlyList<string> lines, string name) => lines.Any(l => IsStart(l, name));

    // The ;@ lines of a region (from its first line up to the ;@ end) replaced by new ones
    public static string[] ReplaceHeader(string[] lines, int startLine, int endLine, string[] header) =>
        [.. lines[..startLine], .. header, .. lines[endLine..]];

    // What the editor wrote, replaced (or, the first time, put after the ;@ end of the region)
    public static string[] ReplaceGenerated(string[] lines, int regionEndLine, string name, string generated)
    {
        string[] block = generated.TrimEnd((char)13, (char)10).Split((char)10).Select(l => l.TrimEnd((char)13)).ToArray();
        int start = Array.FindIndex(lines, l => IsStart(l, name));
        if (start >= 0)
        {
            int end = Array.FindIndex(lines, start, l => IsEnd(l, name));
            if (end < 0) throw new FormatException($"The line \"{EndMarker(name)}\" is missing: the image set {name} cannot be replaced");
            return [.. lines[..start], .. block, .. lines[(end + 1)..]];
        }
        return [.. lines[..(regionEndLine + 1)], "", .. block, .. lines[(regionEndLine + 1)..]];
    }

    // A new source with an image set and nothing else yet
    public static string NewSource(ImageSetAsset set, string name) =>
        $"; Image set {name}: pictures that become screens of characters sharing one charset.\n" +
        "; Edit it with the link above the ;@ imageset lines, which also say how the pictures are reduced.\n\n" +
        string.Join("\n", set.HeaderLines(name)) + "\n;@ end\n";
}
