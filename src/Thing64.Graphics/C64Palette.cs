namespace Thing64.Graphics;

// The 16 colors of the VIC-II. No two emulators agree on the exact RGB of a PAL C64: "pepto" is the one the Thing64
// emulator shows, "colodore" a later measurement, brighter and more saturated
public sealed class C64Palette
{
    public static readonly string[] Names =
        ["black", "white", "red", "cyan", "purple", "green", "blue", "yellow",
         "orange", "brown", "light red", "dark grey", "grey", "light green", "light blue", "light grey"];

    public static C64Palette Pepto { get; } = new("pepto",
        [0x000000, 0xFFFFFF, 0x68372B, 0x70A4B2, 0x6F3D86, 0x588D43, 0x352879, 0xB8C76F,
         0x6F4F25, 0x433900, 0x9A6759, 0x444444, 0x6C6C6C, 0x9AD284, 0x6C5EB5, 0x959595]);

    public static C64Palette Colodore { get; } = new("colodore",
        [0x000000, 0xFFFFFF, 0x813338, 0x75CEC8, 0x8E3C97, 0x56AC4D, 0x2E2C9B, 0xEDF171,
         0x8E5029, 0x553800, 0xC46C71, 0x4A4A4A, 0x7B7B7B, 0xA9FF9F, 0x706DEB, 0xB2B2B2]);

    public static C64Palette Named(string? name) => (name ?? "pepto").ToLowerInvariant() switch
    {
        "pepto" => Pepto,
        "colodore" => Colodore,
        _ => throw new InvalidDataException($"Unknown palette \"{name}\" (pepto or colodore)"),
    };

    public string Name { get; }
    public uint[] Rgb { get; }

    C64Palette(string name, uint[] rgb)
    {
        Name = name;
        Rgb = rgb;
    }

    public uint Argb(int color) => 0xFF000000 | Rgb[color];

    // How different two colors look: RGB distance weighted by the average red ("redmean"), much closer to the eye
    // than the plain RGB distance and cheap enough for millions of comparisons
    public static double Distance(Color a, Color b)
    {
        double meanRed = (a.R + b.R) / 2;
        double dr = a.R - b.R, dg = a.G - b.G, db = a.B - b.B;
        return (2 + meanRed / 256) * dr * dr + 4 * dg * dg + (2 + (255 - meanRed) / 256) * db * db;
    }

    public Color this[int color] => Color.FromRgb(Rgb[color]);
}

// A color with fractional components: the averages of resized pixels and the corrections of dithering
public readonly record struct Color(double R, double G, double B)
{
    public static Color FromRgb(uint rgb) => new((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);

    public static Color operator +(Color a, Color b) => new(a.R + b.R, a.G + b.G, a.B + b.B);
    public static Color operator -(Color a, Color b) => new(a.R - b.R, a.G - b.G, a.B - b.B);
    public static Color operator *(Color a, double k) => new(a.R * k, a.G * k, a.B * k);

    public Color Clamp() => new(Math.Clamp(R, 0, 255), Math.Clamp(G, 0, 255), Math.Clamp(B, 0, 255));
}
