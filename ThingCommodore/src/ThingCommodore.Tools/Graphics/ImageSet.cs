using System.Text;
using System.Text.Json;

namespace ThingCommodore.Graphics;

// An image set: several pictures that become screens of characters sharing one charset (a .imgset.json file).
//   { "name": "story", "first": 0, "sources": ["title.png", "castle.png"], "multicolor": true,
//     "columns": 40, "rows": 25, "fit": "fit", "resample": "box", "dither": "ordered", "brightness": 10 }
// Converting it gives the files of a Thing64 project that the visual editors already open: story.asm (a ;@ charset region with
// the characters of all the pictures, each kept once) and one file for each picture (a ;@ screen region with its screen codes
// and colors, 40x25 or less). The background and the two multicolors are shared too, so that the screens can follow one another
// without the program setting the VIC-II again. Colors left out are chosen from all the pictures together
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

    public const string Extension = ".imgset.json";

    public int CharacterLimit => Math.Clamp(MaxCharacters ?? 256 - First, 1, 256 - First);

    // The asset the converter of single pictures understands
    public GraphicsAsset ToGraphics() => new()
    {
        Mode = Multicolor ? GraphicsMode.MulticolorCharset : GraphicsMode.HiresCharset,
        Fit = Fit, Dither = Dither, Palette = Palette, Resample = Resample, Brightness = Brightness, Contrast = Contrast, Saturation = Saturation,
        Columns = Math.Clamp(Columns ?? 40, 1, 40), Rows = Math.Clamp(Rows ?? 25, 1, 25),
        Background = Background, Multicolor1 = Multicolor1, Multicolor2 = Multicolor2, MaxCharacters = CharacterLimit,
    };

    public static ImageSetAsset Load(string path) => Parse(File.ReadAllText(path));

    public static ImageSetAsset Parse(string json) =>
        JsonSerializer.Deserialize<ImageSetAsset>(json, GraphicsAsset.Json) ?? throw new InvalidDataException("Empty image set");

    public string ToJson() => JsonSerializer.Serialize(this, GraphicsAsset.Json);

    // story.imgset.json -> story
    public static string NameOf(string path)
    {
        string file = Path.GetFileName(path);
        return file.EndsWith(Extension, StringComparison.OrdinalIgnoreCase) ? file[..^Extension.Length] : Path.GetFileNameWithoutExtension(file);
    }
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
    public string CharsetFileName => Name + ".asm";
    public string ScreenFileName(SetScreen screen) => screen.Name + ".asm";

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

    // name.asm: the shared colors and the ;@ charset region, one character per line with the picture it came from
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

    // picture.asm: the ;@ screen region, with the codes of the charset
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
