using ThingCommodore.Graphics;

namespace Thing64.WebStudio.CodeBehind;

// What a ;@ charmap region means: the tables of the tiles and the bitmaps of the characters, all inside the region.
//   ;@ charmap  tiles=charmapChars colors=charmapColors charset=charmapCharacters first=$70 background=6
//   charmapChars:       .byte $20,$70,...       tile -> screen code
//   charmapColors:      .byte $06,$05,...       tile -> color (color RAM)
//   charmapCharacters:  .byte $FF,$FF,...       8 bytes per character, from screen code "first"
//   ;@ end
// tiles, colors and charset name labels of the region. Other parameters: count (tiles; default: up to the colors
// table), names (the prefix of the tiles' constants, TILE_), multicolor=1 with multicolor1/multicolor2 for multicolor
// characters: then a tile whose color has bit 3 set shows its character in multicolor (4x8, colors 0-7)
public sealed class Charmap
{
    public int TilesAt { get; }
    public int ColorsAt { get; }
    public int CharsetAt { get; }
    public int Count { get; }
    public int First { get; }
    public int Background { get; }
    public bool MulticolorMode { get; }
    public int Multicolor1 { get; }
    public int Multicolor2 { get; }
    public IReadOnlyList<string> Names { get; }
    public C64Palette Palette { get; } = C64Palette.Pepto;

    readonly int values;

    public Charmap(AsmRegion region, string[] lines, ProgramImage? image, int values)
    {
        this.values = values;
        var labels = region.LabelOffsets(lines).ToDictionary(l => l.Label, l => l.Offset);
        int At(string parameter) =>
            region.Parameters.TryGetValue(parameter, out var label) && labels.TryGetValue(label, out int offset) ? offset
            : throw new FormatException($";@ charmap: {parameter}= must name a label inside the region");
        int Number(string name, int otherwise) =>
            region.Parameters.TryGetValue(name, out var text) ? ProgramImage.Value(text, image) : otherwise;

        TilesAt = At("tiles");
        ColorsAt = At("colors");
        CharsetAt = At("charset");
        Count = Math.Clamp(Number("count", ColorsAt > TilesAt ? ColorsAt - TilesAt : CharsetAt - TilesAt), 1, 256);
        First = Number("first", 0) & 0xFF;
        Background = Number("background", 0) & 15;
        MulticolorMode = Number("multicolor", 0) != 0;
        Multicolor1 = Number("multicolor1", 11) & 15;
        Multicolor2 = Number("multicolor2", 15) & 15;

        string prefix = region.NamesPrefix;
        var names = new string?[Count];
        if (image is not null)
            foreach (var (name, value) in image.Symbols)
                if (name.StartsWith(prefix, StringComparison.Ordinal) && value >= 0 && value < Count && names[value] is null && !name.EndsWith("_COUNT"))
                    names[value] = name[prefix.Length..];
        Names = names.Select((n, i) => n ?? $"tile {i}").ToArray();
    }

    // How many characters the region holds (8 bytes each, from CharsetAt to the end)
    public int Characters => Math.Max(0, (values - CharsetAt) / 8);

    public int ScreenCode(IReadOnlyList<int> v, int tile) => v[TilesAt + tile];
    public int Color(IReadOnlyList<int> v, int tile) => v[ColorsAt + tile];

    // The index of a screen code among the region's characters, or -1 (a ROM character)
    public int CharacterOf(int screenCode)
    {
        int index = screenCode - First;
        return index >= 0 && index < Characters ? index : -1;
    }

    public bool IsMulticolor(int color) => MulticolorMode && (color & 8) != 0;

    // A character's 8 bytes to one code per pixel: 8x8 in hires, 4x8 (pairs of bits) in multicolor
    public int[] Decode(IReadOnlyList<int> v, int character, bool multicolor)
    {
        int across = multicolor ? 4 : 8, bits = multicolor ? 2 : 1;
        var codes = new int[across * 8];
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < across; x++)
                codes[y * across + x] = v[CharsetAt + character * 8 + y] >> (8 - bits * (x + 1)) & ((1 << bits) - 1);
        return codes;
    }

    public void Encode(int[] v, int character, bool multicolor, int[] codes)
    {
        int across = multicolor ? 4 : 8, bits = multicolor ? 2 : 1;
        for (int y = 0; y < 8; y++)
        {
            int value = 0;
            for (int x = 0; x < across; x++) value |= codes[y * across + x] << (8 - bits * (x + 1));
            v[CharsetAt + character * 8 + y] = value;
        }
    }

    // The colors of the codes: hires background / color; multicolor background, multicolor 1 and 2, color 0-7
    public int ColorOfCode(int code, int color, bool multicolor) => multicolor
        ? code switch { 0 => Background, 1 => Multicolor1, 2 => Multicolor2, _ => color & 7 }
        : code == 0 ? Background : MulticolorMode ? color & 7 : color & 15;   // in multicolor mode cells have 0-7

    // A character drawn with a color (a ROM character, which the studio does not have, as a dotted square)
    public RgbImage Render(IReadOnlyList<int> v, int screenCode, int color, int scale)
    {
        var image = new RgbImage(8 * scale, 8 * scale);
        int character = CharacterOf(screenCode);
        bool multicolor = IsMulticolor(color);
        if (character < 0)
        {
            Array.Fill(image.Pixels, Palette.Argb(Background));
            if (screenCode != 0x20)
                for (int i = 0; i < 8 * scale; i += 2 * scale)
                    for (int k = 0; k < scale; k++)
                    {
                        image[i + k, 0] = image[i + k, 8 * scale - 1] = Palette.Argb(color & 15);
                        image[0, i + k] = image[8 * scale - 1, i + k] = Palette.Argb(color & 15);
                    }
            return image;
        }
        var codes = Decode(v, character, multicolor);
        int across = multicolor ? 4 : 8, width = 8 / across;
        for (int y = 0; y < 8 * scale; y++)
            for (int x = 0; x < 8 * scale; x++)
                image[x, y] = Palette.Argb(ColorOfCode(codes[y / scale * across + x / scale / width], color, multicolor));
        return image;
    }
}
