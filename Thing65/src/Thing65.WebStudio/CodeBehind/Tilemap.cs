using ThingCommodore.Graphics;

namespace Thing65.WebStudio.CodeBehind;

// What a ;@ tilemap region means: a map of logical tile numbers, width x height, drawn through the program's own
// tables. Parameters (numbers or symbols):
//   width, height    the map, in tiles (one tile = one character)
//   tiles            the table tile -> screen code          colors   the table tile -> color (low nybble)
//   count            how many tiles (default: the length of the tiles table, up to the next label)
//   charset, first   the bitmaps of the custom characters, starting at screen code "first" (default 0)
//   chars            how many characters (default: up to the next label)
//   background       the background color                   names    the prefix of the tiles' constants (TILE_)
//   multicolor=1, multicolor1, multicolor2   multicolor characters: a tile whose color has bit 3 set shows its
//                    character in multicolor (pairs of bits: background, multicolor 1 and 2, the color 0-7)
// Screen codes outside the custom characters come from the ROM, which the studio does not have: the space ($20)
// is drawn empty, the others as a dotted square in their color
public sealed class Tilemap
{
    public int Width { get; }
    public int Height { get; }
    public int Count { get; }
    public int Background { get; }
    public bool MulticolorMode { get; }
    public int Multicolor1 { get; }
    public int Multicolor2 { get; }
    public IReadOnlyList<string> Names { get; }

    readonly byte[] screenCodes;
    readonly byte[] colors;
    readonly byte[] charset;
    readonly int first;
    readonly C64Palette palette = C64Palette.Pepto;

    public Tilemap(AsmRegion region, ProgramImage image)
    {
        int Number(string name, int? otherwise = null) =>
            region.Parameters.TryGetValue(name, out var text) ? ProgramImage.Value(text, image)
            : otherwise ?? throw new FormatException($";@ tilemap: the parameter {name} is missing");

        Width = Number("width");
        Height = Number("height");
        if (Width is < 1 or > 1024 || Height is < 1 or > 1024) throw new FormatException($";@ tilemap: {Width}x{Height}");

        int tiles = Number("tiles");
        Count = Math.Clamp(Number("count", image.NextLabelAfter(tiles) - tiles), 1, 256);
        screenCodes = image.Read(tiles, Count);
        colors = region.Parameters.ContainsKey("colors") ? image.Read(Number("colors"), Count) : Enumerable.Repeat((byte)1, Count).ToArray();
        Background = Number("background", 0) & 15;
        MulticolorMode = Number("multicolor", 0) != 0;
        Multicolor1 = Number("multicolor1", 11) & 15;
        Multicolor2 = Number("multicolor2", 15) & 15;

        if (region.Parameters.ContainsKey("charset"))
        {
            int address = Number("charset");
            first = Number("first", 0);
            int chars = Number("chars", (image.NextLabelAfter(address) - address) / 8);
            charset = image.Read(address, Math.Clamp(chars, 0, 256) * 8);
        }
        else
        {
            charset = [];
        }

        string prefix = region.NamesPrefix;
        var names = new string[Count];
        foreach (var (name, value) in image.Symbols)
            if (name.StartsWith(prefix, StringComparison.Ordinal) && value >= 0 && value < Count && names[value] is null && !name.EndsWith("_COUNT"))
                names[value] = name[prefix.Length..];
        Names = names.Select((n, i) => n ?? $"{i}").ToArray();
    }

    public int ScreenCode(int tile) => screenCodes[tile];
    public int Color(int tile) => colors[tile] & 15;

    // The 8x8 pixels of a tile, on the background
    public uint[] Pixels(int tile)
    {
        var pixels = new uint[64];
        uint back = palette.Argb(Background);
        Array.Fill(pixels, back);
        if (tile < 0 || tile >= Count) return pixels;
        int color = colors[tile] & 15;
        bool multicolor = MulticolorMode && (color & 8) != 0;
        uint ink = palette.Argb(MulticolorMode ? color & 7 : color);   // in multicolor mode cells have colors 0-7
        int code = ScreenCode(tile), index = code - first;
        if (index >= 0 && (index + 1) * 8 <= charset.Length && multicolor)
        {
            // Pairs of bits, each a double-wide pixel: 00 background, 01 multicolor 1, 10 multicolor 2, 11 the color
            uint[] inks = [back, palette.Argb(Multicolor1), palette.Argb(Multicolor2), ink];
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    pixels[y * 8 + x] = inks[charset[index * 8 + y] >> (6 - x / 2 * 2) & 3];
        }
        else if (index >= 0 && (index + 1) * 8 <= charset.Length)
        {
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    if ((charset[index * 8 + y] >> (7 - x) & 1) != 0) pixels[y * 8 + x] = ink;
        }
        else if (code != 0x20)
        {
            for (int i = 0; i < 8; i += 2)
            {
                pixels[i] = pixels[56 + i] = ink;
                pixels[i * 8] = pixels[i * 8 + 7] = ink;
            }
        }
        return pixels;
    }

    public RgbImage Render(IReadOnlyList<int> map)
    {
        var image = new RgbImage(Width * 8, Height * 8);
        var cache = new Dictionary<int, uint[]>();
        for (int cell = 0; cell < Width * Height; cell++)
        {
            int tile = cell < map.Count ? map[cell] : 0;
            if (!cache.TryGetValue(tile, out var pixels)) cache[tile] = pixels = Pixels(tile);
            int x0 = cell % Width * 8, y0 = cell / Width * 8;
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    image[x0 + x, y0 + y] = pixels[y * 8 + x];
                }
        }
        return image;
    }

    public RgbImage RenderTile(int tile)
    {
        var image = new RgbImage(8, 8);
        Pixels(tile).CopyTo(image.Pixels, 0);
        return image;
    }
}
