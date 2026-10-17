using Thing65.Graphics;

namespace Thing65.WebStudio.CodeBehind;

// What a ;@ sprites region means: sprites of 64 bytes each (21 rows of 3 bytes, and one unused), each starting at a
// label of the region, hires and multicolor ones mixed.
//   ;@ sprites    color=7 background=6 multicolor1=11 multicolor2=1      (defaults for every sprite)
//   ;@ animation  walk frames=spriteWalk1,spriteWalk2 speed=8          (frames by label, speed in frames of 1/50 s)
//               * = $1000
//   spriteIdle:   .byte ...
//   ;@ multicolor=1 color=5                                            (only for the sprite right below)
//   spriteWalk1:  .byte ...
//   ;@ end
// multicolor1 and multicolor2 are $D025 and $D026, shared by all the sprites; background only shows them on it.
// A pixel is a code: hires 0 transparent, 1 the color; multicolor 0 transparent, 1 multicolor 1, 2 the color,
// 3 multicolor 2
public sealed class SpriteSheet
{
    public const int Rows = 21;
    public const int BlockSize = 64;

    public sealed record Sprite(string Name, int Offset, bool Multicolor, int Color)
    {
        public int Across => Multicolor ? 12 : 24;
    }

    public sealed record Animation(string Name, IReadOnlyList<int> Frames, int Speed);

    public IReadOnlyList<Sprite> Sprites { get; }
    public IReadOnlyList<Animation> Animations { get; }
    public int Multicolor1 { get; }
    public int Multicolor2 { get; }
    public int Background { get; }
    public C65Palette Palette { get; } = C65Palette.Pepto;

    public SpriteSheet(AsmRegion region, string[] lines, ProgramImage? image, int values)
    {
        int Number(IReadOnlyDictionary<string, string> parameters, string name, int otherwise) =>
            parameters.TryGetValue(name, out var text) ? ProgramImage.Value(text, image) : otherwise;

        bool multicolor = Number(region.Parameters, "multicolor", 0) != 0;
        int color = Number(region.Parameters, "color", 1) & 15;
        Multicolor1 = Number(region.Parameters, "multicolor1", 11) & 15;
        Multicolor2 = Number(region.Parameters, "multicolor2", 15) & 15;
        Background = Number(region.Parameters, "background", 0) & 15;

        // One sprite per label; without labels, one every 64 values
        var labels = region.LabelOffsets(lines);
        var sprites = new List<Sprite>();
        if (labels.Count > 0)
        {
            foreach (var (label, offset, directives) in labels)
            {
                var own = directives.SelectMany(d => AsmRegion.ParametersOf(d)).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
                sprites.Add(new Sprite(label, offset, Number(own, "multicolor", multicolor ? 1 : 0) != 0, Number(own, "color", color) & 15));
            }
        }
        else
        {
            for (int offset = 0; offset < Math.Max(values, 1); offset += BlockSize)
                sprites.Add(new Sprite($"sprite {offset / BlockSize}", offset, multicolor, color));
        }
        Sprites = sprites;

        Animations = region.Header.Where(h => h.StartsWith("animation ")).Select(h =>
        {
            var words = h.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var parameters = AsmRegion.ParametersOf(h);
            var frames = parameters.GetValueOrDefault("frames", "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(name => sprites.FindIndex(s => s.Name == name)).Where(i => i >= 0).ToList();
            return new Animation(words.Length > 1 && !words[1].Contains('=') ? words[1] : "animation", frames, Math.Max(1, Number(parameters, "speed", 8)));
        }).ToList();
    }

    // The colors of a sprite's codes; -1 is transparent
    public int ColorOf(Sprite sprite, int code) => sprite.Multicolor
        ? code switch { 1 => Multicolor1, 2 => sprite.Color, 3 => Multicolor2, _ => -1 }
        : code == 1 ? sprite.Color : -1;

    // The 63 bytes of a sprite to one code per pixel
    public static int[] Decode(Sprite sprite, IReadOnlyList<int> values)
    {
        int bits = sprite.Multicolor ? 2 : 1, perByte = 8 / bits;
        var codes = new int[sprite.Across * Rows];
        for (int y = 0; y < Rows; y++)
            for (int x = 0; x < sprite.Across; x++)
            {
                int at = sprite.Offset + y * 3 + x / perByte;
                int value = at < values.Count ? values[at] : 0;
                codes[y * sprite.Across + x] = value >> (8 - bits * (x % perByte + 1)) & ((1 << bits) - 1);
            }
        return codes;
    }

    // The codes back into the values (the 64th byte, unused by the VIC-II, stays as it is)
    public static void Encode(Sprite sprite, int[] codes, int[] values)
    {
        int bits = sprite.Multicolor ? 2 : 1, perByte = 8 / bits;
        for (int i = 0; i < 63 && sprite.Offset + i < values.Length; i++) values[sprite.Offset + i] = 0;
        for (int y = 0; y < Rows; y++)
            for (int x = 0; x < sprite.Across; x++)
            {
                int at = sprite.Offset + y * 3 + x / perByte;
                if (at < values.Length) values[at] |= codes[y * sprite.Across + x] << (8 - bits * (x % perByte + 1));
            }
    }

    // At screen proportions (multicolor pixels twice as wide), scale times; transparent pixels on the background,
    // or really transparent
    public RgbImage Render(Sprite sprite, int[] codes, int scale, bool onBackground)
    {
        int pixelWidth = sprite.Multicolor ? 2 : 1;
        var image = new RgbImage(24 * scale, Rows * scale);
        for (int y = 0; y < Rows * scale; y++)
            for (int x = 0; x < 24 * scale; x++)
            {
                int color = ColorOf(sprite, codes[y / scale * sprite.Across + x / scale / pixelWidth]);
                image[x, y] = color >= 0 ? Palette.Argb(color) : onBackground ? Palette.Argb(Background) : 0u;
            }
        return image;
    }

    // A picture pasted in: a uniform background (the color of the top left corner, when the picture has no
    // transparency) becomes transparent, the rest is converted for this sprite (hires or multicolor, the shared
    // multicolors)
    public int[] FromPicture(Sprite sprite, RgbImage picture)
    {
        if (picture.Pixels.All(p => p >> 24 == 0xFF))
        {
            var corner = Graphics.Color.FromRgb(picture.Pixels[0]);
            for (int i = 0; i < picture.Pixels.Length; i++)
                if (C65Palette.Distance(Graphics.Color.FromRgb(picture.Pixels[i]), corner) < 1200) picture.Pixels[i] = 0;
        }
        var conversion = Converter.Convert(picture, new GraphicsAsset
        {
            Mode = sprite.Multicolor ? GraphicsMode.MulticolorSprites : GraphicsMode.HiresSprites,
            Fit = Fit.Fit,
            Dither = Dither.None,
            Multicolor1 = Multicolor1,
            Multicolor2 = Multicolor2,
        });
        var data = conversion.Parts.Single(p => p.Name == "sprites").Data.Select(b => (int)b).ToArray();
        return Decode(sprite with { Offset = 0 }, data);
    }
}
