using Thing65.Graphics;

namespace Thing65.WebStudio.CodeBehind;

// What a ;@ screen region means: a C64 screen as the program keeps it, two labels inside the region:
//   ;@ screen width=40 height=25 screen=room1__screen colors=room1__colors
//   ;@        charset=charset1__characters first=$70 background=5 multicolor=1 multicolor1=$0E multicolor2=$01
//   room1__screen:   .byte ...   width x height screen codes (what goes to $0400)
//   room1__colors:   .byte ...   width x height colors (what goes to $D800)
//   ;@ end
// charset names the bitmaps of the custom characters (anywhere in the program), from screen code "first"; the other
// screen codes are the ROM's. With multicolor=1 a color with bit 3 set shows its character in multicolor: pairs of
// bits, 00 background, 01 multicolor1, 10 multicolor2, 11 the color 0-7; the other cells are hires with colors 0-7.
// When the charset is multicolor (its ;@ charset region says multicolor=1) the editor sets that bit by itself: the
// charset's own characters get it, the ROM's do not
public sealed class ScreenRegion
{
    public int Width { get; }
    public int Height { get; }
    public int ScreenAt { get; }
    public int ColorsAt { get; }
    public int First { get; }
    public int Characters { get; }
    public int Background { get; }
    public bool MulticolorMode { get; }
    public int Multicolor1 { get; }
    public int Multicolor2 { get; }
    public string CharsetLabel { get; }
    public bool CharsetMulticolor { get; }
    public C65Palette Palette { get; } = C65Palette.Pepto;

    readonly byte[] charset;

    public ScreenRegion(AsmRegion region, string[] lines, ProgramImage image, bool charsetMulticolor = false)
    {
        int Number(string name, int? otherwise = null) =>
            region.Parameters.TryGetValue(name, out var text) ? ProgramImage.Value(text, image)
            : otherwise ?? throw new FormatException($";@ screen: the parameter {name} is missing");
        var labels = region.LabelOffsets(lines).ToDictionary(l => l.Label, l => l.Offset);
        int At(string name) =>
            region.Parameters.TryGetValue(name, out var label) && labels.TryGetValue(label, out int offset) ? offset
            : throw new FormatException($";@ screen: {name}= must name a label inside the region");

        Width = Number("width", 40);
        Height = Number("height", 25);
        ScreenAt = At("screen");
        ColorsAt = At("colors");
        First = Number("first", 0) & 0xFF;
        Background = Number("background", 0) & 15;
        MulticolorMode = Number("multicolor", 0) != 0;
        Multicolor1 = Number("multicolor1", 11) & 15;
        Multicolor2 = Number("multicolor2", 15) & 15;
        CharsetLabel = region.Parameters.GetValueOrDefault("charset", "");
        CharsetMulticolor = charsetMulticolor;
        if (CharsetLabel.Length > 0)
        {
            int address = Number("charset");
            int length = Math.Clamp(image.NextLabelAfter(address) - address, 0, 256 * 8);
            charset = image.Read(address, length);
        }
        else
        {
            charset = [];
        }
        Characters = charset.Length / 8;
    }

    public int Cells => Width * Height;
    public bool IsMulticolor(int color) => MulticolorMode && (color & 8) != 0;

    // True when the charset decides the bit 3 of the cells' colors, so the pen does not
    public bool ChoosesMulticolor => CharsetMulticolor && MulticolorMode;

    // The color a cell gets when a character is painted in it: with a multicolor charset, bit 3 follows the
    // character (set for the charset's own, clear for the ROM's); otherwise the color as it is
    public int CellColor(int code, int color) =>
        !ChoosesMulticolor ? color : (color & 7) | (IsCustom(code) ? 8 : 0);
    public bool IsCustom(int code) => code >= First && code < First + Characters;

    // A cell's 8x8 pixels: its character in its color, hires or multicolor. ROM characters, which the studio does
    // not have, show as a dotted square (the space, $20, as nothing)
    public uint[] CellPixels(int code, int color)
    {
        var pixels = new uint[64];
        uint back = Palette.Argb(Background);
        uint ink = Palette.Argb(MulticolorMode ? color & 7 : color & 15);
        Array.Fill(pixels, back);
        int index = code - First;
        if (IsCustom(code) && IsMulticolor(color))
        {
            uint[] inks = [back, Palette.Argb(Multicolor1), Palette.Argb(Multicolor2), ink];
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    pixels[y * 8 + x] = inks[charset[index * 8 + y] >> (6 - x / 2 * 2) & 3];
        }
        else if (IsCustom(code))
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

    public RgbImage Render(IReadOnlyList<int> values)
    {
        var image = new RgbImage(Width * 8, Height * 8);
        var cache = new Dictionary<(int, int), uint[]>();
        for (int cell = 0; cell < Cells; cell++)
        {
            int code = values[ScreenAt + cell], color = values[ColorsAt + cell] & 15;
            if (!cache.TryGetValue((code, color), out var pixels)) cache[(code, color)] = pixels = CellPixels(code, color);
            int x0 = cell % Width * 8, y0 = cell / Width * 8;
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    image[x0 + x, y0 + y] = pixels[y * 8 + x];
        }
        return image;
    }

    public RgbImage RenderCell(int code, int color)
    {
        var image = new RgbImage(8, 8);
        CellPixels(code, CellColor(code, color)).CopyTo(image.Pixels, 0);
        return image;
    }
}
