using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThingCommodore.Graphics;

// The C64 graphics modes an image can become, with the colors each one allows:
//   hires-bitmap        320x200, each 8x8 cell 2 colors of 16 (screen RAM)
//   multicolor-bitmap   160x200 with double-wide pixels, each 4x8 cell the background + 3 colors (screen RAM, color RAM)
//   hires-charset       characters 8x8: the background + 1 color per cell (color RAM)
//   multicolor-charset  characters 4x8: background, multicolor 1 and 2 shared, + 1 color of 0-7 per cell
//   hires-sprites       24x21: transparent + 1 color per sprite
//   multicolor-sprites  12x21: transparent, multicolor 1 and 2 shared, + 1 color per sprite
public enum GraphicsMode
{
    HiresBitmap, MulticolorBitmap, HiresCharset, MulticolorCharset, HiresSprites, MulticolorSprites,
}

public enum Fit { Fit, Fill, Stretch }

public enum Dither { None, Ordered, FloydSteinberg }

public sealed record TileSize(int Width, int Height);

// A graphics asset: a .gfx.json file next to its image, saying how to convert it and what to produce.
//   { "source": "title.png", "mode": "multicolor-bitmap", "fit": "fit", "dither": "ordered",
//     "background": 0, "addresses": { "bitmap": "$2000" } }
// Building it writes the data files (name.bitmap.bin, name.screen.bin, ...) and name.inc.asm, which a source
// includes with !source: since the build always regenerates them, the assembly never falls behind the image.
// Colors left out (background, multicolor1, multicolor2) are chosen from the image
public sealed record GraphicsAsset
{
    public string Source { get; init; } = "";
    public GraphicsMode Mode { get; init; } = GraphicsMode.MulticolorBitmap;
    public Fit Fit { get; init; } = Fit.Fit;
    public Dither Dither { get; init; } = Dither.Ordered;
    public string Palette { get; init; } = "pepto";

    // Characters: the screen in cells (at most 40x25 for a full screen; more for a map wider than the screen).
    // Sprites: how many sprites across and down
    public int? Columns { get; init; }
    public int? Rows { get; init; }

    public int? Background { get; init; }
    public int? Multicolor1 { get; init; }
    public int? Multicolor2 { get; init; }

    // Characters: at most this many different ones (similar ones are merged); tiles: blocks of characters repeated
    public int MaxCharacters { get; init; } = 256;
    public TileSize? Tile { get; init; }

    // Names: labels and files (by default the name of the .gfx.json), the include file, the address of each part
    public string? Name { get; init; }
    public string? Include { get; init; }
    public Dictionary<string, string> Addresses { get; init; } = [];

    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) },
    };

    public const string Extension = ".gfx.json";

    public static GraphicsAsset Load(string path) =>
        JsonSerializer.Deserialize<GraphicsAsset>(File.ReadAllText(path), Json) ?? throw new InvalidDataException($"{path}: empty");

    public static GraphicsAsset Parse(string json) =>
        JsonSerializer.Deserialize<GraphicsAsset>(json, Json) ?? throw new InvalidDataException("Empty graphics asset");

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    // title.gfx.json -> title
    public static string NameOf(string assetPath)
    {
        string file = Path.GetFileName(assetPath);
        return file.EndsWith(Extension, StringComparison.OrdinalIgnoreCase) ? file[..^Extension.Length] : Path.GetFileNameWithoutExtension(file);
    }
}
