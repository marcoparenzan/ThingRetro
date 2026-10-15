namespace Thing64.Graphics;

// A text screen taken from a running C64, as the VIC-II sees it: Board.CaptureVideo() gives the 16 KB of the VIC's
// bank (the character ROM already in place where the chip reads it), the 1024 nibbles of Color RAM and the registers
// $D000-$D03F. From those it knows where the screen and the characters are, and its colors
public sealed class VideoCapture
{
    public const int BankSize = 16384, ColorSize = 1024, RegistersSize = 64;
    public const int Size = BankSize + ColorSize + RegistersSize;
    public const int Columns = 40, Rows = 25, Cells = Columns * Rows;

    readonly byte[] data;

    public VideoCapture(byte[] data)
    {
        if (data.Length != Size) throw new FormatException($"A video capture is {Size} bytes, not {data.Length}");
        this.data = data;
        if ((Register(0x11) & 0x20) != 0) throw new NotSupportedException("The screen is in bitmap mode: only text screens can be captured");
        if ((Register(0x11) & 0x40) != 0) throw new NotSupportedException("The screen is in extended color mode: only standard and multicolor text screens can be captured");
    }

    byte Register(int register) => data[BankSize + ColorSize + register];

    // $D018: the screen matrix in steps of 1 KB, the characters in steps of 2 KB, both inside the VIC's bank
    public int ScreenAddress => (Register(0x18) >> 4) * 0x400;
    public int CharsetAddress => (Register(0x18) >> 1 & 7) * 0x800;

    public bool MulticolorMode => (Register(0x16) & 0x10) != 0;
    public int Border => Register(0x20) & 15;
    public int Background => Register(0x21) & 15;
    public int Multicolor1 => Register(0x22) & 15;
    public int Multicolor2 => Register(0x23) & 15;

    public int Code(int cell) => data[ScreenAddress + cell];
    public int Color(int cell) => data[BankSize + cell] & 15;

    // In multicolor mode a color from 8 up shows its character in multicolor: pairs of bits
    public bool IsMulticolorCell(int cell) => MulticolorMode && (Color(cell) & 8) != 0;

    // The 8 bytes of a character
    public byte[] Character(int code) => data[(CharsetAddress + code * 8)..(CharsetAddress + code * 8 + 8)];

    // The screen as the C64 shows it, 320x200 without the border
    public RgbImage Render(C64Palette palette)
    {
        var image = new RgbImage(Columns * 8, Rows * 8);
        for (int cell = 0; cell < Cells; cell++)
        {
            var bytes = Character(Code(cell));
            int color = Color(cell), x0 = cell % Columns * 8, y0 = cell / Columns * 8;
            bool multicolor = IsMulticolorCell(cell);
            uint[] inks = multicolor
                ? [palette.Argb(Background), palette.Argb(Multicolor1), palette.Argb(Multicolor2), palette.Argb(color & 7)]
                : [palette.Argb(Background), palette.Argb(MulticolorMode ? color & 7 : color)];
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    image[x0 + x, y0 + y] = multicolor ? inks[bytes[y] >> (6 - x / 2 * 2) & 3] : inks[bytes[y] >> (7 - x) & 1];
        }
        return image;
    }
}
