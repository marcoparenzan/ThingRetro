using ThingRetroEmu.Buses;

namespace ThingCommodore.Printers;

/*
    The Commodore MPS-803 dot matrix printer: 80 columns at 10 characters per inch, 6 lines per inch, bit image graphics of 480
    dots across. A stand-alone device on the IEC cable (device 4, or 5), which writes on a DotPaper and hands out a PDF.

    What it understands (the codes the computer sends with PRINT#):
      printable characters   PETSCII, drawn with the character generator of the computer (the printer's own font is not available)
      13  CR                 print the line and feed (cancels the double width, the reverse and the bit images)
      141                    the same
      10  LF                 line feed
      12  FF                 new page
      14 / 15                double width on / back to normal characters
      18 / 146               reverse on / off
      17 / 145               lower case and upper case sets (the secondary address 7 starts with the lower case)
      8                      bit image mode: every byte from 128 up is a column of 7 dots (bit 0 the top one); 26 repeats the last
                             column (the next byte is the count); a byte below 128 is ignored
      16 nn                  tab to the column nn (two digits)
    Not done: the ESC sequences (dot tabs, repeated graphics with ESC), the status channel, TALK.
    Reference: "MPS-803 User's Manual"
 */
public sealed class Mps803 : IecListener
{
    const int CELL = 24;                // a character cell in dots of the paper: 8 dots of 80 per inch
    const int GRAPHIC_DOT = 4;          // a column of a bit image: 60 per inch
    const int COLUMNS = DotPaper.WIDTH / CELL;
    const int TEXT_LINE = 12;           // 6 lines per inch
    const int GRAPHICS_LINE = 9;

    readonly byte[] chargen;
    int x, y;                           // the head on the sheet: dots across, rows down
    bool lowerCase, wide, reverse, graphics, repeatNext;
    int lastColumn;
    int tabDigits = -1, tabValue;       // reading the two digits that follow 16
    bool lineHasInk;

    // chargen: the 4096 bytes of the C64 character generator (the same ROM as the computer's)
    public Mps803(byte[] chargen, int deviceNumber = 4) : base(deviceNumber)
    {
        ArgumentNullException.ThrowIfNull(chargen);
        if (chargen.Length < 4096) throw new ArgumentException("The character generator has 4096 bytes", nameof(chargen));
        this.chargen = chargen;
    }

    public DotPaper Paper { get; } = new();

    // Everything printed so far as a PDF
    public byte[] ToPdf() => PdfFile.Write(Paper);

    // Everything printed since the last time
    public bool HasPrinted => Paper.Sheets > 1 || lineHasInk || y > 0;

    public void NewPaper()
    {
        Paper.Clear();
        x = y = 0;
        lineHasInk = false;
    }

    protected override void OnListen(int channel)
    {
        if (channel >= 0) lowerCase = channel == 7;    // OPEN 1,4,7: upper and lower case
    }

    protected override void OnData(byte value, bool eoi) => Print(value);

    // ---- the codes ------------------------------------------------------------------------------------------------------

    public void Print(byte code)
    {
        if (tabDigits >= 0)
        {
            tabValue = tabValue * 10 + (code - '0');
            if (++tabDigits == 2)
            {
                x = Math.Min(tabValue, COLUMNS - 1) * CELL;
                tabDigits = -1;
            }
            return;
        }
        if (repeatNext)
        {
            repeatNext = false;
            for (int i = 0; i < code; i++) Column(lastColumn);
            return;
        }

        switch (code)
        {
            case 13 or 141: CarriageReturn(); return;
            case 10: LineFeed(); return;
            case 12: NewPage(); return;
            case 14: wide = true; graphics = false; return;
            case 15: wide = false; graphics = false; return;
            case 18: reverse = true; return;
            case 146: reverse = false; return;
            case 17: lowerCase = true; return;
            case 145: lowerCase = false; return;
            case 8: graphics = true; return;
            case 16: tabDigits = 0; tabValue = 0; return;
            case 26 when graphics: repeatNext = true; return;
        }

        if (graphics)
        {
            if (code >= 128) Column(code & 0x7F);
            return;
        }
        if (code < 0x20 || code is >= 0x80 and < 0xA0) return;     // control codes with nothing to draw
        Glyph(ScreenCode(code));
    }

    // PETSCII to the number of the character in the generator
    static int ScreenCode(byte c) => c switch
    {
        < 0x40 => c,
        < 0x60 => c - 0x40,
        < 0x80 => c - 0x20,
        < 0xC0 => c - 0x40,
        < 0xFF => c - 0x80,
        _ => 0x5E,
    };

    void Glyph(int screenCode)
    {
        int width = wide ? CELL * 2 : CELL;
        if (x + width > DotPaper.WIDTH) CarriageReturn();
        int dot = width / 8;
        int set = lowerCase ? 0x800 : 0;
        for (int row = 0; row < 8; row++)
        {
            int bits = chargen[set + screenCode * 8 + row];
            if (reverse) bits ^= 0xFF;
            for (int column = 0; column < 8; column++)
                if ((bits & 0x80 >> column) != 0) Paper.Plot(x + column * dot, y + row, dot);
        }
        x += width;
        lineHasInk = true;
    }

    // A column of a bit image: seven dots, the lowest bit on top
    void Column(int pattern)
    {
        lastColumn = pattern;
        if (x + GRAPHIC_DOT > DotPaper.WIDTH) CarriageReturn(keepMode: true);
        for (int row = 0; row < 7; row++)
            if ((pattern >> row & 1) != 0) Paper.Plot(x, y + row, GRAPHIC_DOT);
        x += GRAPHIC_DOT;
        lineHasInk = true;
    }

    void CarriageReturn(bool keepMode = false)
    {
        x = 0;
        LineFeed();
        if (!keepMode)
        {
            wide = false;
            reverse = false;
            graphics = false;
        }
        lineHasInk = false;
    }

    void LineFeed()
    {
        y += graphics ? GRAPHICS_LINE : TEXT_LINE;
        if (y + 8 > DotPaper.HEIGHT) NewPage();
    }

    void NewPage()
    {
        if (lineHasInk || y > 0) Paper.NewSheet();
        x = y = 0;
        lineHasInk = false;
    }
}
