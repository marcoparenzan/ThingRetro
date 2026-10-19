using System.Numerics;

namespace ThingCommodore.Graphics;

// One block of data for the C64 (the bitmap, the screen, the characters, ...): Name is also its file and its label
public sealed record GraphicsPart(string Name, byte[] Data, string Description);

// An image converted: the data, the colors chosen, the constants for the assembly, and how it will look
public sealed class Conversion
{
    public required GraphicsMode Mode { get; init; }
    public required int Columns { get; init; }
    public required int Rows { get; init; }
    public required RgbImage Preview { get; init; }
    public required IReadOnlyList<GraphicsPart> Parts { get; init; }
    public required IReadOnlyDictionary<string, int> Constants { get; init; }
    public required string Summary { get; init; }
}

// From a picture to the C64: resize it to the mode's resolution, choose the colors (the shared ones, then the best
// ones for each cell, which is the whole difficulty of C64 graphics), place every pixel on one of its cell's colors
// (with dithering, to fake the colors in between), then encode the result as the VIC-II reads it
public static partial class Converter
{
    const int Transparent = -1;

    public static Conversion Convert(RgbImage source, GraphicsAsset asset)
    {
        var palette = C64Palette.Named(asset.Palette);
        var layout = new Layout(asset);
        var (colors, opaque) = Resize(source, layout, asset);

        // Shared colors: those the image uses most, unless the asset fixes them
        var popular = Popularity(colors, opaque, palette);
        int background = asset.Background ?? popular[0];
        int multicolor1 = asset.Multicolor1 ?? popular.First(c => c != background);
        int multicolor2 = asset.Multicolor2 ?? popular.First(c => c != background && c != multicolor1);
        if (!layout.Sprites)
        {
            // Pixels outside the picture (fit, transparent ones) are background
            for (int i = 0; i < colors.Length; i++) if (!opaque[i]) colors[i] = palette[layout.Mode == GraphicsMode.HiresBitmap ? 0 : background];
        }

        // Colors of each cell: the codes of its pixels (00, 01, 10, 11) mean these palette colors
        var cells = new int[layout.Columns * layout.Rows][];
        for (int cell = 0; cell < cells.Length; cell++)
            cells[cell] = BestCellColors(cell, colors, opaque, layout, palette, asset.Dither, background, multicolor1, multicolor2);

        int[] codes = PlacePixels(colors, opaque, cells, layout, palette, asset.Dither);
        var encoded = Encode(codes, cells, layout, asset, background, multicolor1, multicolor2);

        return new Conversion
        {
            Mode = layout.Mode,
            Columns = layout.Columns,
            Rows = layout.Rows,
            Preview = Render(encoded.Codes, cells, layout, palette),
            Parts = encoded.Parts,
            Constants = encoded.Constants,
            Summary = encoded.Summary,
        };
    }

    // The resolution of a mode: cells of pixels, with double-wide pixels in multicolor
    sealed class Layout
    {
        public GraphicsMode Mode { get; }
        public bool Multicolor { get; }
        public bool Sprites { get; }
        public bool Bitmap { get; }
        public int PixelWidth => Multicolor ? 2 : 1;
        public int CellWidth { get; }
        public int CellHeight { get; }
        public int Columns { get; }
        public int Rows { get; }
        public int Width => Columns * CellWidth;
        public int Height => Rows * CellHeight;

        public Layout(GraphicsAsset asset)
        {
            Mode = asset.Mode;
            Multicolor = Mode is GraphicsMode.MulticolorBitmap or GraphicsMode.MulticolorCharset or GraphicsMode.MulticolorSprites;
            Sprites = Mode is GraphicsMode.HiresSprites or GraphicsMode.MulticolorSprites;
            Bitmap = Mode is GraphicsMode.HiresBitmap or GraphicsMode.MulticolorBitmap;
            CellWidth = Sprites ? 24 / PixelWidth : 8 / PixelWidth;
            CellHeight = Sprites ? 21 : 8;
            Columns = Bitmap ? 40 : asset.Columns ?? (Sprites ? 1 : 40);
            Rows = Bitmap ? 25 : asset.Rows ?? (Sprites ? 1 : 25);
            if (Columns is < 1 or > 1024 || Rows is < 1 or > 1024) throw new InvalidDataException($"{Columns}x{Rows} cells");
        }

        public int CellOf(int x, int y) => (y / CellHeight) * Columns + x / CellWidth;
    }

    // Each C64 pixel comes from the part of the picture it covers (fit: all of it, with borders; fill: the middle, cut; stretch: all of
    // it, deformed), by the average of that part (box), its middle pixel (nearest) or a blend of the four pixels around its middle
    // (bilinear); then the tones are corrected. Less than half covered by opaque pixels: transparent
    static (Color[] Colors, bool[] Opaque) Resize(RgbImage source, Layout layout, GraphicsAsset asset)
    {
        var fit = asset.Fit;
        var resample = asset.Resample ?? Resample.Box;
        double screenWidth = layout.Width * layout.PixelWidth, screenHeight = layout.Height;
        double scaleX = screenWidth / source.Width, scaleY = screenHeight / source.Height;
        if (fit == Fit.Fit) scaleX = scaleY = Math.Min(scaleX, scaleY);
        else if (fit == Fit.Fill) scaleX = scaleY = Math.Max(scaleX, scaleY);
        double left = (screenWidth - source.Width * scaleX) / 2, top = (screenHeight - source.Height * scaleY) / 2;

        var colors = new Color[layout.Width * layout.Height];
        var opaque = new bool[colors.Length];
        for (int y = 0; y < layout.Height; y++)
        {
            double v0 = (y - top) / scaleY, v1 = (y + 1 - top) / scaleY;
            for (int x = 0; x < layout.Width; x++)
            {
                double u0 = (x * layout.PixelWidth - left) / scaleX, u1 = ((x + 1) * layout.PixelWidth - left) / scaleX;
                Color sum = default;
                double weight = 0, needed = (u1 - u0) * (v1 - v0) / 2;
                switch (resample)
                {
                    case Resample.Nearest:
                        Sample((int)Math.Floor((u0 + u1) / 2), (int)Math.Floor((v0 + v1) / 2), 1);
                        needed = 0.5;
                        break;
                    case Resample.Bilinear:
                    {
                        double u = (u0 + u1) / 2 - 0.5, v = (v0 + v1) / 2 - 0.5;
                        int sx = (int)Math.Floor(u), sy = (int)Math.Floor(v);
                        double fx = u - sx, fy = v - sy;
                        Sample(sx, sy, (1 - fx) * (1 - fy));
                        Sample(sx + 1, sy, fx * (1 - fy));
                        Sample(sx, sy + 1, (1 - fx) * fy);
                        Sample(sx + 1, sy + 1, fx * fy);
                        needed = 0.5;
                        break;
                    }
                    default:
                        for (int sy = Math.Max(0, (int)Math.Floor(v0)); sy < Math.Min(source.Height, (int)Math.Ceiling(v1)); sy++)
                        {
                            double h = Math.Min(sy + 1, v1) - Math.Max(sy, v0);
                            for (int sx = Math.Max(0, (int)Math.Floor(u0)); sx < Math.Min(source.Width, (int)Math.Ceiling(u1)); sx++)
                                Sample(sx, sy, h * (Math.Min(sx + 1, u1) - Math.Max(sx, u0)));
                        }
                        break;
                }
                int i = y * layout.Width + x;
                opaque[i] = weight >= needed;
                colors[i] = weight > 0 ? Tone(sum * (1 / weight), asset) : default;

                void Sample(int sx, int sy, double share)
                {
                    if (sx < 0 || sy < 0 || sx >= source.Width || sy >= source.Height) return;
                    uint pixel = source[sx, sy];
                    double w = share * (pixel >> 24) / 255.0;
                    sum += Color.FromRgb(pixel) * w;
                    weight += w;
                }
            }
        }
        return (colors, opaque);
    }

    // The correction of the tones: saturation (the distance from the grey of the same brightness), contrast (around the middle grey),
    // brightness (added), in this order
    static Color Tone(Color color, GraphicsAsset asset)
    {
        if (asset.Saturation is { } saturation and not 0)
        {
            double luma = 0.299 * color.R + 0.587 * color.G + 0.114 * color.B, k = 1 + saturation / 100.0;
            color = new Color(luma + (color.R - luma) * k, luma + (color.G - luma) * k, luma + (color.B - luma) * k);
        }
        if (asset.Contrast is { } contrast and not 0)
        {
            double c = Math.Clamp(contrast, -100, 100) * 2.55, factor = 259 * (c + 255) / (255 * (259 - c));
            color = new Color((color.R - 128) * factor + 128, (color.G - 128) * factor + 128, (color.B - 128) * factor + 128);
        }
        if (asset.Brightness is { } brightness and not 0) color += new Color(1, 1, 1) * (Math.Clamp(brightness, -100, 100) * 2.55);
        return color.Clamp();
    }

    // The 16 colors, most used first (each pixel counts for the color nearest to it)
    static int[] Popularity(Color[] colors, bool[] opaque, C64Palette palette) => Ranked(Counts(colors, opaque, palette));

    static int[] Counts(Color[] colors, bool[] opaque, C64Palette palette)
    {
        var counts = new int[16];
        for (int i = 0; i < colors.Length; i++) if (opaque[i]) counts[Nearest(colors[i], palette, Enumerable.Range(0, 16))]++;
        return counts;
    }

    static int[] Ranked(int[] counts) => Enumerable.Range(0, 16).OrderByDescending(c => counts[c]).ToArray();

    static int Nearest(Color color, C64Palette palette, IEnumerable<int> choices) =>
        choices.MinBy(c => C64Palette.Distance(color, palette[c]));

    // The colors a cell can use, by pixel code. The free ones are searched among all the combinations, keeping
    // those that make the cell's pixels closest to the picture. With dithering the pixels are judged already moved by
    // the thresholds, as they will be placed: a flat grey then gets the two greys around it, which dithering can mix,
    // instead of the nearest grey and any other color
    static int[] BestCellColors(int cell, Color[] colors, bool[] opaque, Layout layout, C64Palette palette, Dither dither,
                                int background, int multicolor1, int multicolor2)
    {
        var (free, candidates, build) = layout.Mode switch
        {
            GraphicsMode.HiresBitmap => (2, 16, (Func<int[], int[]>)(f => [f[0], f[1]])),
            GraphicsMode.MulticolorBitmap => (3, 16, f => [background, f[0], f[1], f[2]]),
            GraphicsMode.HiresCharset => (1, 16, f => [background, f[0]]),
            GraphicsMode.MulticolorCharset => (1, 8, f => [background, multicolor1, multicolor2, f[0]]),
            GraphicsMode.HiresSprites => (1, 16, f => [Transparent, f[0]]),
            _ => (1, 16, (Func<int[], int[]>)(f => [Transparent, multicolor1, f[0], multicolor2])),
        };

        // The distance of each pixel of the cell from each of the 16 colors, computed once
        var pixels = new List<double[]>();
        int x0 = cell % layout.Columns * layout.CellWidth, y0 = cell / layout.Columns * layout.CellHeight;
        for (int y = y0; y < y0 + layout.CellHeight; y++)
            for (int x = x0; x < x0 + layout.CellWidth; x++)
            {
                int i = y * layout.Width + x;
                if (layout.Sprites && !opaque[i]) continue;
                Color target = dither == Dither.None ? colors[i] : (colors[i] + Threshold(x, y)).Clamp();
                pixels.Add(Enumerable.Range(0, 16).Select(c => C64Palette.Distance(target, palette[c])).ToArray());
            }

        int[]? best = null;
        double bestError = double.MaxValue;
        foreach (var choice in Combinations(candidates, free))
        {
            var codes = build(choice);
            double error = 0;
            foreach (var distances in pixels)
            {
                double nearest = double.MaxValue;
                foreach (int color in codes) if (color != Transparent && distances[color] < nearest) nearest = distances[color];
                error += nearest;
                if (error >= bestError) break;
            }
            if (error < bestError)
            {
                bestError = error;
                best = codes;
            }
        }
        return best!;
    }

    static IEnumerable<int[]> Combinations(int n, int k)
    {
        if (k == 1) { for (int a = 0; a < n; a++) yield return [a]; yield break; }
        if (k == 2) { for (int a = 0; a < n; a++) for (int b = a + 1; b < n; b++) yield return [a, b]; yield break; }
        for (int a = 0; a < n; a++) for (int b = a + 1; b < n; b++) for (int c = b + 1; c < n; c++) yield return [a, b, c];
    }

    // 4x4 Bayer matrix: thresholds spread evenly, so that neighboring pixels round in different directions
    static readonly int[] Bayer = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];
    const double OrderedSpread = 56;

    static Color Threshold(int x, int y) => new Color(1, 1, 1) * (((Bayer[(y & 3) * 4 + (x & 3)] + 0.5) / 16 - 0.5) * OrderedSpread);

    // Every pixel gets the code of one of its cell's colors. Ordered dithering shifts each pixel by a threshold of
    // the Bayer matrix before choosing; Floyd-Steinberg passes the error of each choice on to the next pixels
    static int[] PlacePixels(Color[] colors, bool[] opaque, int[][] cells, Layout layout, C64Palette palette, Dither dither)
    {
        int width = layout.Width, height = layout.Height;
        var work = (Color[])colors.Clone();
        var codes = new int[colors.Length];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                if (layout.Sprites && !opaque[i]) continue;   // code 0: transparent
                var cell = cells[layout.CellOf(x, y)];
                Color target = work[i];
                if (dither == Dither.Ordered) target += Threshold(x, y);

                int code = 0;
                double nearest = double.MaxValue;
                for (int c = 0; c < cell.Length; c++)
                {
                    if (cell[c] == Transparent) continue;
                    double distance = C64Palette.Distance(target.Clamp(), palette[cell[c]]);
                    if (distance < nearest) { nearest = distance; code = c; }
                }
                codes[i] = code;

                if (dither == Dither.FloydSteinberg)
                {
                    Color error = work[i] - palette[cell[code]];
                    void Spread(int dx, int dy, double share)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || nx >= width || ny >= height) return;
                        int n = ny * width + nx;
                        work[n] = new Color(Math.Clamp(work[n].R + error.R * share, -64, 320),
                                            Math.Clamp(work[n].G + error.G * share, -64, 320),
                                            Math.Clamp(work[n].B + error.B * share, -64, 320));
                    }
                    Spread(1, 0, 7.0 / 16);
                    Spread(-1, 1, 3.0 / 16);
                    Spread(0, 1, 5.0 / 16);
                    Spread(1, 1, 1.0 / 16);
                }
            }
        return codes;
    }

    sealed record Encoded(int[] Codes, List<GraphicsPart> Parts, Dictionary<string, int> Constants, string Summary);

    static Encoded Encode(int[] codes, int[][] cells, Layout layout, GraphicsAsset asset, int background, int multicolor1, int multicolor2)
    {
        var parts = new List<GraphicsPart>();
        var constants = new Dictionary<string, int>();
        string summary;
        int cellCount = layout.Columns * layout.Rows;

        if (layout.Bitmap)
        {
            var bitmap = new byte[8000];
            var screen = new byte[1000];
            var colorRam = new byte[1000];
            for (int cell = 0; cell < 1000; cell++)
            {
                CellBytes(codes, layout, cell).CopyTo(bitmap, cell * 8);
                var c = cells[cell];
                if (layout.Multicolor)
                {
                    screen[cell] = (byte)(c[1] << 4 | c[2]);
                    colorRam[cell] = (byte)c[3];
                }
                else
                {
                    screen[cell] = (byte)(c[1] << 4 | c[0]);
                }
            }
            parts.Add(new("bitmap", bitmap, "8000 bytes: 8 bytes per cell, cells left to right, top to bottom"));
            parts.Add(new("screen", screen, layout.Multicolor ? "Screen RAM: colors of the codes 01 (high nybble) and 10 (low nybble)" : "Screen RAM: color of the bits 1 (high nybble) and 0 (low nybble)"));
            if (layout.Multicolor)
            {
                parts.Add(new("colors", colorRam, "Color RAM ($D800): color of the code 11"));
                constants["BACKGROUND"] = background;
                summary = $"Multicolor bitmap 160x200, background {C64Palette.Names[background]}";
            }
            else
            {
                summary = "Hires bitmap 320x200";
            }
        }
        else if (layout.Sprites)
        {
            int count = cellCount;
            var data = new byte[count * 64];
            var spriteColors = new byte[count];
            for (int sprite = 0; sprite < count; sprite++)
            {
                CellBytes(codes, layout, sprite).CopyTo(data, sprite * 64);
                spriteColors[sprite] = (byte)cells[sprite][layout.Multicolor ? 2 : 1];
            }
            parts.Add(new("sprites", data, "64 bytes per sprite (21 rows of 3 bytes, and one unused)"));
            parts.Add(new("spritecolors", spriteColors, "The color of each sprite ($D027-$D02E)"));
            constants["SPRITES"] = count;
            if (layout.Multicolor)
            {
                constants["MULTICOLOR1"] = multicolor1;
                constants["MULTICOLOR2"] = multicolor2;
            }
            summary = $"{count} {(layout.Multicolor ? "multicolor" : "hires")} sprite{(count == 1 ? "" : "s")}";
        }
        else
        {
            // Characters: the same 8 bytes in many cells are one character; above the limit, the least used ones
            // are replaced by the most similar ones
            var charset = new Charset(layout.Multicolor);
            var screen = new int[cellCount];
            for (int cell = 0; cell < cellCount; cell++) screen[cell] = charset.Add(CellBytes(codes, layout, cell));
            int found = charset.Count;
            charset.Reduce(screen, asset.MaxCharacters);
            for (int cell = 0; cell < cellCount; cell++) WriteCell(codes, layout, cell, charset[screen[cell]]);

            var colorRam = new byte[cellCount];
            for (int cell = 0; cell < cellCount; cell++)
                colorRam[cell] = (byte)(layout.Multicolor ? cells[cell][3] | 8 : cells[cell][1]);

            parts.Add(new("charset", charset.Bytes(), $"{charset.Count} characters of 8 bytes"));
            parts.Add(new("screen", screen.Select(c => (byte)c).ToArray(), $"{layout.Columns}x{layout.Rows} screen codes"));
            parts.Add(new("colors", colorRam, $"{layout.Columns}x{layout.Rows} colors for the color RAM ($D800)"));
            constants["BACKGROUND"] = background;
            if (layout.Multicolor)
            {
                constants["MULTICOLOR1"] = multicolor1;
                constants["MULTICOLOR2"] = multicolor2;
            }
            constants["CHARACTERS"] = charset.Count;
            constants["COLUMNS"] = layout.Columns;
            constants["ROWS"] = layout.Rows;
            summary = $"{(layout.Multicolor ? "Multicolor" : "Hires")} characters {layout.Columns}x{layout.Rows}: {charset.Count} characters"
                      + (found > charset.Count ? $" (from {found}, similar ones merged)" : "");

            if (asset.Tile is { } tile) summary += AddTiles(screen, colorRam, layout, tile, parts, constants);
        }
        return new Encoded(codes, parts, constants, summary);
    }

    // Tiles: blocks of tile.Width x tile.Height characters (with their colors), each kept once, and the map of
    // the screen in tiles
    static string AddTiles(int[] screen, byte[] colorRam, Layout layout, TileSize tile, List<GraphicsPart> parts, Dictionary<string, int> constants)
    {
        if (tile.Width < 1 || tile.Height < 1 || layout.Columns % tile.Width != 0 || layout.Rows % tile.Height != 0)
            throw new InvalidDataException($"Tiles of {tile.Width}x{tile.Height} do not divide {layout.Columns}x{layout.Rows} cells");
        int across = layout.Columns / tile.Width, down = layout.Rows / tile.Height;
        var tiles = new List<byte[]>();
        var index = new Dictionary<string, int>();
        var map = new byte[across * down];
        for (int ty = 0; ty < down; ty++)
            for (int tx = 0; tx < across; tx++)
            {
                var content = new byte[tile.Width * tile.Height * 2];   // characters, then colors
                for (int y = 0; y < tile.Height; y++)
                    for (int x = 0; x < tile.Width; x++)
                    {
                        int cell = (ty * tile.Height + y) * layout.Columns + tx * tile.Width + x;
                        content[y * tile.Width + x] = (byte)screen[cell];
                        content[tile.Width * tile.Height + y * tile.Width + x] = colorRam[cell];
                    }
                string key = System.Convert.ToHexString(content);
                if (!index.TryGetValue(key, out int number))
                {
                    number = tiles.Count;
                    if (number == 256) throw new InvalidDataException($"More than 256 different tiles of {tile.Width}x{tile.Height}");
                    index[key] = number;
                    tiles.Add(content);
                }
                map[ty * across + tx] = (byte)number;
            }
        int size = tile.Width * tile.Height;
        parts.Add(new("tiles", tiles.SelectMany(t => t.Take(size)).ToArray(), $"{tiles.Count} tiles of {tile.Width}x{tile.Height} screen codes, row by row"));
        parts.Add(new("tilecolors", tiles.SelectMany(t => t.Skip(size)).ToArray(), $"The colors of the tiles, in the same order"));
        parts.Add(new("map", map, $"{across}x{down} tile numbers"));
        constants["TILES"] = tiles.Count;
        constants["TILE_WIDTH"] = tile.Width;
        constants["TILE_HEIGHT"] = tile.Height;
        constants["MAP_COLUMNS"] = across;
        constants["MAP_ROWS"] = down;
        return $", {tiles.Count} tiles of {tile.Width}x{tile.Height}";
    }

    // The bytes of a cell as the VIC-II reads them: one byte per row (sprites: three), the leftmost pixel in the
    // highest bits; 1 bit per pixel in hires, 2 in multicolor
    static byte[] CellBytes(int[] codes, Layout layout, int cell)
    {
        int bits = layout.Multicolor ? 2 : 1, perByte = 8 / bits, bytesPerRow = layout.CellWidth / perByte;
        var bytes = new byte[layout.Sprites ? 63 : 8];
        int x0 = cell % layout.Columns * layout.CellWidth, y0 = cell / layout.Columns * layout.CellHeight;
        for (int row = 0; row < layout.CellHeight; row++)
            for (int x = 0; x < layout.CellWidth; x++)
            {
                int code = codes[(y0 + row) * layout.Width + x0 + x];
                bytes[row * bytesPerRow + x / perByte] |= (byte)(code << (8 - bits * (x % perByte + 1)));
            }
        return bytes;
    }

    // The other way round: the codes of a character back into the cell (after characters were merged)
    static void WriteCell(int[] codes, Layout layout, int cell, byte[] bytes)
    {
        int bits = layout.Multicolor ? 2 : 1, perByte = 8 / bits, mask = (1 << bits) - 1;
        int x0 = cell % layout.Columns * layout.CellWidth, y0 = cell / layout.Columns * layout.CellHeight;
        for (int row = 0; row < 8; row++)
            for (int x = 0; x < layout.CellWidth; x++)
                codes[(y0 + row) * layout.Width + x0 + x] = bytes[row] >> (8 - bits * (x % perByte + 1)) & mask;
    }

    // How the C64 will show it, at screen resolution (multicolor pixels twice as wide)
    static RgbImage Render(int[] codes, int[][] cells, Layout layout, C64Palette palette)
    {
        var image = new RgbImage(layout.Width * layout.PixelWidth, layout.Height);
        for (int y = 0; y < layout.Height; y++)
            for (int x = 0; x < layout.Width; x++)
            {
                int color = cells[layout.CellOf(x, y)][codes[y * layout.Width + x]];
                uint argb = color == Transparent ? 0u : palette.Argb(color);
                for (int p = 0; p < layout.PixelWidth; p++) image[x * layout.PixelWidth + p, y] = argb;
            }
        return image;
    }

    // The characters of a character set, each kept once, with how many cells use it
    sealed class Charset(bool multicolor)
    {
        readonly List<byte[]> characters = [];
        readonly List<int> uses = [];
        readonly List<string> tags = [];
        readonly Dictionary<string, int> index = [];
        int frozen;

        public int Count => characters.Count;
        public byte[] this[int number] => characters[number];

        // What each character says about itself (for a set: the picture it first came from)
        public string Tag(int number) => tags[number];

        // Characters that are already there (a charset that was edited): they keep their places and are never merged away,
        // the pictures just find them
        public void Freeze(IEnumerable<(byte[] Bytes, string Tag)> existing)
        {
            foreach (var (bytes, tag) in existing)
            {
                index.TryAdd(System.Convert.ToHexString(bytes), characters.Count);
                characters.Add(bytes);
                uses.Add(0);
                tags.Add(tag);
            }
            frozen = characters.Count;
        }

        public int Add(byte[] bytes, string tag = "")
        {
            string key = System.Convert.ToHexString(bytes);
            if (!index.TryGetValue(key, out int number))
            {
                number = characters.Count;
                index[key] = number;
                characters.Add(bytes);
                uses.Add(0);
                tags.Add(tag);
            }
            uses[number]++;
            return number;
        }

        // Down to the limit: the least used character goes, its cells take the most similar one
        public void Reduce(int[] screen, int limit)
        {
            limit = Math.Clamp(limit, 1, 256);
            if (frozen > limit) throw new InvalidDataException($"The charset already has {frozen} characters: more than the {limit} allowed");
            while (characters.Count > limit)
            {
                int victim = Enumerable.Range(frozen, characters.Count - frozen).MinBy(c => uses[c]);
                int twin = Enumerable.Range(0, characters.Count).Where(c => c != victim).MinBy(c => Difference(characters[victim], characters[c]));
                int last = characters.Count - 1;
                for (int cell = 0; cell < screen.Length; cell++)
                {
                    if (screen[cell] == victim) screen[cell] = twin;
                    if (screen[cell] == last) screen[cell] = victim;   // the last one takes the place of the one removed
                }
                uses[twin] += uses[victim];
                characters[victim] = characters[last];
                uses[victim] = uses[last];
                tags[victim] = tags[last];
                characters.RemoveAt(last);
                uses.RemoveAt(last);
                tags.RemoveAt(last);
            }
        }

        // Pixels that differ: bits in hires, pairs of bits in multicolor
        int Difference(byte[] a, byte[] b)
        {
            int count = 0;
            for (int i = 0; i < 8; i++)
            {
                int x = a[i] ^ b[i];
                count += BitOperations.PopCount((uint)(multicolor ? (x | x >> 1) & 0x55 : x));
            }
            return count;
        }

        public byte[] Bytes() => characters.SelectMany(c => c).ToArray();
    }
}
