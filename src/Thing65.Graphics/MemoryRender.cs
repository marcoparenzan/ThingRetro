namespace Thing65.Graphics;

// How the cells of a screen, or the pixels of a tile, are read
public enum CellMode
{
    Hires,               // 8x8: the background and the ink
    MulticolorByColor,   // the C64's own rule: a color from 8 up is multicolor (pairs of bits), the rest are hires with colors 0-7
    Multicolor,          // every cell multicolor, whatever its color
}

// The colors a decoded picture uses: the background, the two shared multicolors, and the ink (the color of a cell
// in a charset or a sprite)
public sealed record PaintColors(int Background, int Multicolor1, int Multicolor2, int Ink);

// Bytes of memory as pictures, to find in a dump what is in it: a screen of characters, a charset, sprites, a
// bitmap. Every function reads RAM with the addresses wrapping at 64 KB; the offset is where to start
public static class MemoryRender
{
    // 8 pixels of one row of a tile: hires (a bit is the background or the ink) or multicolor (pairs of bits choose
    // among four colors, each pixel two wide)
    static void Row(RgbImage image, int x, int y, int bits, bool multicolor, uint[] pairs, uint ink)
    {
        for (int px = 0; px < 8; px++)
            image[x + px, y] = multicolor ? pairs[bits >> (6 - px / 2 * 2) & 3] : (bits >> (7 - px) & 1) != 0 ? ink : pairs[0];
    }

    // The screen the bytes from "offset" make, columns x rows of screen codes, each character from charRow(code,
    // row) (the bytes of its 8 rows). The color of a cell is the Color RAM's, or fixedColor
    public static RgbImage Screen(byte[] ram, byte[] colorRam, int offset, int columns, int rows,
        Func<int, int, byte> charRow, CellMode mode, PaintColors colors, int? fixedColor = null, C65Palette? palette = null)
    {
        palette ??= C65Palette.Pepto;
        var image = new RgbImage(columns * 8, rows * 8);
        for (int cell = 0; cell < columns * rows; cell++)
        {
            int code = ram[(offset + cell) & 0xFFFF];
            int color = fixedColor ?? colorRam[cell & (MemoryCapture.ColorSize - 1)] & 15;
            bool multicolor = mode == CellMode.Multicolor || mode == CellMode.MulticolorByColor && (color & 8) != 0;
            int ink = mode == CellMode.Hires ? color : color & 7;
            uint[] pairs = [palette.Argb(colors.Background), palette.Argb(colors.Multicolor1), palette.Argb(colors.Multicolor2), palette.Argb(ink)];
            int x0 = cell % columns * 8, y0 = cell / columns * 8;
            for (int row = 0; row < 8; row++)
                Row(image, x0, y0 + row, charRow(code, row), multicolor, pairs, palette.Argb(ink));
        }
        return image;
    }

    // Characters one after the other from "offset", 8 bytes each, perRow in a row
    public static RgbImage Charset(byte[] ram, int offset, int count, int perRow, bool multicolor, PaintColors colors, C65Palette? palette = null)
    {
        palette ??= C65Palette.Pepto;
        int rows = (count + perRow - 1) / perRow;
        var image = new RgbImage(perRow * 8, Math.Max(1, rows) * 8);
        uint[] pairs = [palette.Argb(colors.Background), palette.Argb(colors.Multicolor1), palette.Argb(colors.Multicolor2), palette.Argb(colors.Ink & 7)];
        uint ink = palette.Argb(colors.Ink);
        for (int k = 0; k < perRow * Math.Max(1, rows); k++)
            for (int row = 0; row < 8; row++)
                Row(image, k % perRow * 8, k / perRow * 8 + row,
                    k < count ? ram[(offset + k * 8 + row) & 0xFFFF] : 0, multicolor, pairs, ink);
        return image;
    }

    // Sprites one after the other from "offset", every "stride" bytes (64 for sprites in their usual blocks), 24x21
    // each (12x21 double-wide pixels in multicolor), perRow in a row. A multicolor sprite's bit pairs are
    // 00 transparent (the background), 01 multicolor 1, 10 its own color, 11 multicolor 2
    public static RgbImage Sprites(byte[] ram, int offset, int count, int perRow, bool multicolor, PaintColors colors,
        int stride = 64, C65Palette? palette = null)
    {
        palette ??= C65Palette.Pepto;
        int rows = (count + perRow - 1) / perRow;
        var image = new RgbImage(perRow * 24, Math.Max(1, rows) * 21);
        uint[] pairs = [palette.Argb(colors.Background), palette.Argb(colors.Multicolor1), palette.Argb(colors.Ink), palette.Argb(colors.Multicolor2)];
        uint ink = palette.Argb(colors.Ink);
        for (int k = 0; k < perRow * Math.Max(1, rows); k++)
            for (int y = 0; y < 21; y++)
                for (int b = 0; b < 3; b++)
                    Row(image, k % perRow * 24 + b * 8, k / perRow * 21 + y,
                        k < count ? ram[(offset + k * stride + y * 3 + b) & 0xFFFF] : 0, multicolor, pairs, ink);
        return image;
    }

    // A bitmap screen, 320x200, from "bitmapOffset" (8000 bytes: each cell 8 bytes). The colors of a cell come from
    // the screen RAM at "screenOffset" (hires: the high nibble is the ink, the low one the background; multicolor:
    // 01 the high nibble, 10 the low one, 11 the Color RAM) and 00 is the background
    public static RgbImage Bitmap(byte[] ram, byte[] colorRam, int bitmapOffset, int screenOffset, bool multicolor,
        int background, C65Palette? palette = null)
    {
        palette ??= C65Palette.Pepto;
        var image = new RgbImage(320, 200);
        for (int cell = 0; cell < 1000; cell++)
        {
            int colors = ram[(screenOffset + cell) & 0xFFFF], high = colors >> 4, low = colors & 15;
            int fromColorRam = colorRam[cell] & 15;
            uint[] pairs = multicolor
                ? [palette.Argb(background), palette.Argb(high), palette.Argb(low), palette.Argb(fromColorRam)]
                : [palette.Argb(low), 0, 0, 0];
            for (int row = 0; row < 8; row++)
                Row(image, cell % 40 * 8, cell / 40 * 8 + row, ram[(bitmapOffset + cell * 8 + row) & 0xFFFF], multicolor, pairs, palette.Argb(high));
        }
        return image;
    }
}
