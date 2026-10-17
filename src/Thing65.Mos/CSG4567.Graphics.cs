namespace Thing65.Mos;

// VIC-III rendering into a frame buffer of palette indexes (0-255). The unit is a "dot", half a VIC-II pixel: a
// VIC-II pixel is 2 dots wide, a 640-mode pixel 1. Every raster line gives two lines of the frame; they are the
// same, except in 400-line bitplane mode, where the second one comes from the odd scan of the bitplanes.
// As in the VIC-II, a register written in the middle of a line only changes the dots that come after (CatchUp).
public partial class CSG4567
{
    // Visible area: 42 pixels of border left/top, 320x200 of display, 41/42 right/bottom, in dots
    public const int SCREEN_WIDTH = 403 * 2;
    public const int SCREEN_HEIGHT = 284 * 2;
    const int FIRST_VISIBLE_LINE = 9;        // raster line shown on the first line of the frame buffer
    const int DISPLAY_LEFT = 42 * 2;         // frame buffer column matching sprite X 24
    const int SPRITE_X_TO_SCREEN = 18;       // in VIC-II pixels
    const int FIRST_TEXT_LINE = 0x30;        // text row 0 starts at raster line $30 + YSCROLL
    const int LINE_X_POSITIONS = 504;        // X coordinates in a line: 63 cycles x 8 pixels

    readonly byte[] frame = new byte[SCREEN_WIDTH * SCREEN_HEIGHT];

    // Working buffers for one line: the two lines of the frame, and what sprites need
    readonly byte[] lineTop = new byte[SCREEN_WIDTH];
    readonly byte[] lineBottom = new byte[SCREEN_WIDTH];
    readonly bool[] lineForeground = new bool[SCREEN_WIDTH]; // foreground dots (sprite priority and collisions)
    readonly byte[] lineSprites = new byte[SCREEN_WIDTH];    // mask of the sprites present in each dot
    byte[] line = null!;                                     // where the current composition goes

    int renderedTo;                                          // dots of the current line already in the frame

    // Palette index of a dot of the last frame drawn
    internal byte PixelAt(int x, int y) => frame[y * SCREEN_WIDTH + x];

    // Frame buffer column output in the current cycle: 8 pixels (16 dots) per cycle, sprite X 24 comes out in cycle 16
    int BeamX => Math.Clamp(2 * (8 * currentCycle - 86), 0, SCREEN_WIDTH);

    bool Dual => NewMode && (registers[REG_CONTROL_B] & (B_BPM | B_V400 | B_INT)) == (B_BPM | B_V400 | B_INT);

    // Before a register write: draws the dots already output on this line with the old values
    void CatchUp()
    {
        int x = BeamX;
        if (x <= renderedTo) return;
        DrawUpTo(x, detectCollisions: false);
    }

    // End of line: draws the remaining dots, detects sprite collisions, then the border check of cycle 63
    void RenderLine(int rasterLine)
    {
        DrawUpTo(SCREEN_WIDTH, detectCollisions: true);
        EndOfLineBorder(rasterLine);
        renderedTo = 0;
    }

    void DrawUpTo(int x, bool detectCollisions)
    {
        int y = rasterLine - FIRST_VISIBLE_LINE;
        bool visible = y >= 0 && y < SCREEN_HEIGHT / 2;
        bool dual = Dual;
        if (visible)
        {
            line = lineTop;
            ComposeLine(rasterLine, field: 0, detectCollisions);
            if (dual)
            {
                line = lineBottom;
                ComposeLine(rasterLine, field: 1, detectCollisions: false);
            }
        }
        ApplyBorder(rasterLine, renderedTo, x, visible, dual);
        if (visible)
        {
            int length = x - renderedTo;
            Array.Copy(lineTop, renderedTo, frame, 2 * y * SCREEN_WIDTH + renderedTo, length);
            Array.Copy(dual ? lineBottom : lineTop, renderedTo, frame, (2 * y + 1) * SCREEN_WIDTH + renderedTo, length);
        }
        renderedTo = x;
    }

    // Colors: in VIC-III mode the registers hold a full palette index, otherwise 4 bits
    byte ColorIndex(int register) => NewMode ? registers[register] : (byte)(registers[register] & 0x0F);

    // Computes the whole line (graphics and sprites, without border) with the current registers into "line"
    void ComposeLine(int rasterLine, int field, bool detectCollisions)
    {
        Array.Fill(line, ColorIndex(REG_BACKGROUND_COLOR));
        Array.Clear(lineForeground);

        RenderGraphics(rasterLine, field);
        RenderSprites(detectCollisions);
    }

    byte ReadMemory(int address) => Memory?.Read(address & 0x3FFF) ?? 0xFF;
    byte ReadColor(int address) => Memory?.ReadColor(address & 0x07FF) ?? 0x0F;
    byte ReadRam(int address) => Memory?.ReadRam(address) ?? 0xFF;

    // Outside the 200 display lines (or in a frame without bad lines) the graphics sequencer is "idle": it repeats
    // the byte at the last address of the bank with the color data at zero
    void RenderGraphics(int rasterLine, int field)
    {
        byte control1 = registers[REG_CONTROL_1];
        byte control2 = registers[REG_CONTROL_2];
        byte controlB = NewMode ? registers[REG_CONTROL_B] : (byte)0;

        int vpos = NewMode ? (sbyte)registers[REG_VPOS] : 0;                 // VPOS: the display moves down by that many lines
        int graphicsLine = rasterLine - (FIRST_TEXT_LINE + (control1 & 0x07) + vpos); // YSCROLL
        bool idle = !badLinesEnabled || graphicsLine < 0 || graphicsLine >= 200;
        if (idle && (controlB & B_BPM) != 0) return; // bitplanes: only the background shows

        bool wide = (controlB & B_H640) != 0;
        int columns = wide ? 80 : 40;
        int units = wide ? 1 : 2;              // dots per pixel
        int row = graphicsLine >> 3;
        int charLine = graphicsLine & 0x07;
        int xStart = DISPLAY_LEFT + 2 * (control2 & 0x07) + (NewMode ? (sbyte)registers[REG_HPOS] : 0); // XSCROLL, HPOS (in dots)

        if ((controlB & B_BPM) != 0)
        {
            RenderBitplanes(graphicsLine, field, xStart, wide);
            return;
        }

        bool ecm = (control1 & 0x40) != 0;
        bool bmm = (control1 & 0x20) != 0;
        bool mcm = (control2 & 0x10) != 0;
        bool attributes = (controlB & B_ATTR) != 0;

        byte memoryPointers = registers[REG_MEMORY_POINTERS];
        int screenBase = wide ? (memoryPointers & 0xE0) << 6 : (memoryPointers & 0xF0) << 6;
        int charBase = (memoryPointers & 0x0E) << 10;
        int bitmapBase = (memoryPointers & 0x08) << 10;

        byte bg0 = ColorIndex(REG_BACKGROUND_COLOR);
        byte bg1 = ColorIndex(REG_BACKGROUND_COLOR_1);
        byte bg2 = ColorIndex(REG_BACKGROUND_COLOR_2);
        byte bg3 = ColorIndex(REG_BACKGROUND_COLOR_3);

        byte idleBits = ReadMemory(ecm ? 0x39FF : 0x3FFF);
        bool blinkOff = ((frames / 25) & 1) != 0; // about 1 Hz

        for (int column = 0; column < columns; column++)
        {
            int cell = row * columns + column;
            byte screenCode = idle ? (byte)0 : ReadMemory(screenBase + cell);
            byte colorByte = idle ? (byte)0 : ReadColor(cell);
            byte color = (byte)(colorByte & 0x0F);
            int x = xStart + column * 8 * units;

            if (idle)
            {
                if (ecm && (bmm || mcm)) PutInvalid(x, idleBits, mcm, units);
                else if (bmm && mcm) PutMulticolor(x, idleBits, bg0, 0, 0, 0, units);
                else PutHires(x, idleBits, 0, bmm ? (byte)0 : bg0, units);
            }
            else if (bmm)
            {
                byte bits;
                if (wide) bits = ReadRam(((Memory?.Bank ?? 0) & 2) << 14 | (memoryPointers & 0x08) << 11 | (cell * 8 + charLine));
                else bits = ReadMemory(bitmapBase + cell * 8 + charLine);
                if (ecm)
                    PutInvalid(x, bits, mcm, units);
                else if (mcm)
                    PutMulticolor(x, bits, bg0, (byte)(screenCode >> 4), (byte)(screenCode & 0x0F), color, units);
                else
                    PutHires(x, bits, (byte)(screenCode >> 4), (byte)(screenCode & 0x0F), units);
            }
            else if (ecm)
            {
                byte bits = ReadMemory(charBase + (screenCode & 0x3F) * 8 + charLine);
                if (mcm)
                    PutInvalid(x, bits, true, units);
                else
                {
                    byte background = (screenCode >> 6) switch { 0 => bg0, 1 => bg1, 2 => bg2, _ => bg3 };
                    PutHires(x, bits, color, background, units);
                }
            }
            else
            {
                byte bits = ReadMemory(charBase + screenCode * 8 + charLine);
                if (mcm && (color & 0x08) != 0)
                    PutMulticolor(x, bits, bg0, bg1, bg2, (byte)(color & 0x07), units);
                else if (attributes)
                    PutAttributed(x, bits, colorByte, bg0, charLine, blinkOff, units);
                else
                    PutHires(x, bits, color, bg0, units);
            }
        }
    }

    // A character with attributes (bits 4-7 of the Color RAM): blink, reverse, highlight (+16 to the colors), underline.
    // Blink alone flashes the character; with other attributes it switches them on and off
    void PutAttributed(int x, byte bits, byte attributes, byte background, int charLine, bool blinkOff, int units)
    {
        bool blink = (attributes & 0x10) != 0, reverse = (attributes & 0x20) != 0;
        bool highlight = (attributes & 0x40) != 0, underline = (attributes & 0x80) != 0;
        bool others = reverse || highlight || underline;

        byte foreground = (byte)(attributes & 0x0F);
        if (blink && blinkOff)
        {
            if (!others) bits = 0;                     // flashes: the background only
            else { reverse = highlight = underline = false; } // the other attributes are off in this phase
        }
        if (underline && charLine == 7) bits = 0xFF;   // the underline is reversed with the character
        if (reverse) bits = (byte)~bits;
        if (highlight)
        {
            foreground += 16;
            background += 16;
        }
        PutHires(x, bits, foreground, background, units);
    }

    // Bitplane mode: up to 8 planes (4 in 640 mode) whose bits give the palette index of each pixel. The planes use
    // the same layout as a bitmap: cells of 8 bytes. Bitplane 2 is the foreground for sprites
    void RenderBitplanes(int graphicsLine, int field, int xStart, bool wide)
    {
        int columns = wide ? 80 : 40;
        int units = wide ? 1 : 2;
        int planes = registers[REG_BITPLANE_ENABLE] & (wide ? 0x0F : 0xFF);
        int complement = registers[REG_BITPLANE_COMPLEMENT];
        int cellRow = (graphicsLine >> 3) * columns * 8 + (graphicsLine & 7);

        Span<int> bases = stackalloc int[8];
        for (int p = 0; p < 8; p++) bases[p] = PlaneBase(p, field, wide) + cellRow;

        Span<int> bytes = stackalloc int[8];
        for (int column = 0; column < columns; column++)
        {
            for (int p = 0; p < 8; p++)
            {
                int value = (planes & (1 << p)) != 0 ? ReadRam(bases[p] + column * 8) : 0;
                bytes[p] = (complement & (1 << p)) != 0 ? ~value & 0xFF : value;
            }
            int x = xStart + column * 8 * units;
            for (int bit = 0; bit < 8; bit++)
            {
                int index = 0;
                for (int p = 0; p < 8; p++) index |= ((bytes[p] >> (7 - bit)) & 1) << p;
                if (wide && (registers[REG_CONTROL_B] & B_H1280) != 0) index &= 0x7F; // 1280: bit plane 7 is the pixel clock; the early pixel
                bool foreground = ((bytes[2] >> (7 - bit)) & 1) != 0;
                for (int k = 0; k < units; k++)
                {
                    int position = x + bit * units + k;
                    if (position >= SCREEN_WIDTH) return;
                    line[position] = (byte)index;
                    lineForeground[position] = foreground;
                }
            }
        }
    }

    // 8 pixels: bit 1 = foreground, bit 0 = background
    void PutHires(int x, byte bits, byte foreground, byte background, int units)
    {
        for (int i = 0; i < 8; i++)
        {
            bool set = (bits & (0x80 >> i)) != 0;
            for (int k = 0; k < units; k++, x++)
            {
                if (x >= SCREEN_WIDTH) return;
                line[x] = set ? foreground : background;
                lineForeground[x] = set;
            }
        }
    }

    // 4 bit pairs, each 2 pixels wide: 00 and 01 are background, 10 and 11 foreground
    void PutMulticolor(int x, byte bits, byte color00, byte color01, byte color10, byte color11, int units)
    {
        for (int pair = 0; pair < 4; pair++)
        {
            int value = (bits >> (6 - pair * 2)) & 0x03;
            byte color = value switch { 0 => color00, 1 => color01, 2 => color10, _ => color11 };
            for (int k = 0; k < 2 * units; k++, x++)
            {
                if (x >= SCREEN_WIDTH) return;
                line[x] = color;
                lineForeground[x] = value >= 2;
            }
        }
    }

    // Invalid modes (ECM together with BMM or MCM): black screen, but foreground pixels count for collisions
    void PutInvalid(int x, byte bits, bool multicolor, int units)
    {
        for (int i = 0; i < 8; i++)
        {
            bool set = multicolor ? ((bits >> (6 - (i & 6))) & 0x02) != 0 : (bits & (0x80 >> i)) != 0;
            for (int k = 0; k < units; k++, x++)
            {
                if (x >= SCREEN_WIDTH) return;
                line[x] = 0;
                lineForeground[x] = set;
            }
        }
    }

    // ---- Sprites ----------------------------------------------------------------------------------------
    // The visible sprites and the data row of each come from the DMA logic (CSG4567.Sprites.cs). Sprites are not
    // affected by the 640 mode: a sprite pixel is always 2 dots (4 when expanded)
    void RenderSprites(bool detectCollisions)
    {
        byte visible = spriteVisible;
        if (visible == 0) return;

        Array.Clear(lineSprites);
        bool wide = NewMode && (registers[REG_CONTROL_B] & B_H640) != 0;
        int screenBase = wide ? (registers[REG_MEMORY_POINTERS] & 0xE0) << 6 : (registers[REG_MEMORY_POINTERS] & 0xF0) << 6;
        int pointers = wide ? 0x7F8 : 0x3F8;

        // The lowest-numbered sprite wins: a dot already taken is not drawn again
        for (int n = 0; n < 8; n++)
        {
            int bit = 1 << n;
            if ((visible & bit) == 0) continue;

            int pointer = ReadMemory(screenBase + pointers + n);
            int dataAddress = pointer * 64 + spriteData[n];
            int data = ReadMemory(dataAddress) << 16 | ReadMemory(dataAddress + 1) << 8 | ReadMemory(dataAddress + 2);
            if (data == 0) continue;

            int spriteX = registers[REG_SPRITE0_X + n * 2] | ((registers[REG_SPRITE_X_MSB] & bit) != 0 ? 0x100 : 0);
            if (spriteX >= LINE_X_POSITIONS) continue;
            int x = 2 * ((spriteX >= LINE_X_POSITIONS - SPRITE_X_TO_SCREEN ? spriteX - LINE_X_POSITIONS : spriteX) + SPRITE_X_TO_SCREEN);
            int dotWidth = (registers[REG_SPRITE_X_EXPAND] & bit) != 0 ? 4 : 2;
            bool behindForeground = (registers[REG_SPRITE_PRIORITY] & bit) != 0;
            byte spriteColor = ColorIndex(REG_SPRITE0_COLOR + n);

            if ((registers[REG_SPRITE_MULTICOLOR] & bit) != 0)
            {
                byte multicolor0 = ColorIndex(REG_SPRITE_MULTICOLOR_0);
                byte multicolor1 = ColorIndex(REG_SPRITE_MULTICOLOR_1);
                for (int pair = 0; pair < 12; pair++)
                {
                    int value = (data >> (22 - pair * 2)) & 0x03;
                    if (value == 0) continue;
                    byte color = value switch { 1 => multicolor0, 2 => spriteColor, _ => multicolor1 };
                    PutSpriteDots(x + pair * 2 * dotWidth, 2 * dotWidth, color, bit, behindForeground);
                }
            }
            else
            {
                for (int i = 0; i < 24; i++)
                {
                    if ((data & (0x800000 >> i)) != 0)
                        PutSpriteDots(x + i * dotWidth, dotWidth, spriteColor, bit, behindForeground);
                }
            }
        }

        if (detectCollisions) DetectCollisions();
    }

    void PutSpriteDots(int x, int count, byte color, int bit, bool behindForeground)
    {
        for (int k = 0; k < count; k++, x++)
        {
            if (x < 0 || x >= SCREEN_WIDTH) continue;
            bool claimedByLowerSprite = lineSprites[x] != 0;
            lineSprites[x] |= (byte)bit;
            if (claimedByLowerSprite) continue;
            if (behindForeground && lineForeground[x]) continue;
            line[x] = color;
        }
    }

    // Collisions: the registers accumulate bits until they are read; the interrupt triggers only when the register
    // goes from zero to non-zero
    void DetectCollisions()
    {
        int spriteSprite = 0;
        int spriteBackground = 0;
        for (int x = 0; x < SCREEN_WIDTH; x++)
        {
            int mask = lineSprites[x];
            if (mask == 0) continue;
            if ((mask & (mask - 1)) != 0) spriteSprite |= mask;
            if (lineForeground[x]) spriteBackground |= mask;
        }

        if (spriteSprite != 0)
        {
            if (registers[REG_SPRITE_SPRITE_COLL] == 0) registers[REG_INTERRUPT_STATUS] |= 0x04;
            registers[REG_SPRITE_SPRITE_COLL] |= (byte)spriteSprite;
        }
        if (spriteBackground != 0)
        {
            if (registers[REG_SPRITE_DATA_COLL] == 0) registers[REG_INTERRUPT_STATUS] |= 0x02;
            registers[REG_SPRITE_DATA_COLL] |= (byte)spriteBackground;
        }
        if ((spriteSprite | spriteBackground) != 0) CheckInterrupts();
    }

    // ---- Border -----------------------------------------------------------------------------------------
    // Two flip-flops, as in the VIC-II: vertical (set at the bottom line, cleared at the top one if DEN is on) and
    // main (set at the right compare, cleared at the left one if the vertical one is clear)
    bool mainBorder = true, verticalBorder = true;

    (int Top, int Bottom) BorderRows() => (registers[REG_CONTROL_1] & 0x08) != 0 ? (0x33, 0xFB) : (0x37, 0xF7);

    void ApplyBorder(int rasterLine, int from, int to, bool visible, bool dual)
    {
        bool columns40 = (registers[REG_CONTROL_2] & 0x08) != 0;
        int left = DISPLAY_LEFT + (columns40 ? 0 : 14);
        int right = DISPLAY_LEFT + (columns40 ? 640 : 622);
        byte border = ColorIndex(REG_BORDER_COLOR);
        int position = from;

        void Fill(int end)
        {
            if (visible && mainBorder && end > position)
            {
                Array.Fill(lineTop, border, position, end - position);
                if (dual) Array.Fill(lineBottom, border, position, end - position);
            }
            position = Math.Max(position, end);
        }

        if (left >= from && left < to)
        {
            Fill(left);
            var (top, bottom) = BorderRows();
            if (rasterLine == bottom) verticalBorder = true;
            else if (rasterLine == top && (registers[REG_CONTROL_1] & 0x10) != 0) verticalBorder = false;
            if (!verticalBorder) mainBorder = false;
        }
        if (right >= from && right < to)
        {
            Fill(right);
            mainBorder = true;
        }
        Fill(to);
    }

    void EndOfLineBorder(int rasterLine)
    {
        var (top, bottom) = BorderRows();
        if (rasterLine == bottom) verticalBorder = true;
        else if (rasterLine == top && (registers[REG_CONTROL_1] & 0x10) != 0) verticalBorder = false;
    }

    // ---- Output -----------------------------------------------------------------------------------------
    // Copies the last frame into the window's 32-bit (BGRA) bitmap, through the palette
    public unsafe void Render(byte* scan0)
    {
        var colors = ResolvePalette();
        uint* pixels = (uint*)scan0;
        for (int i = 0; i < frame.Length; i++)
            pixels[i] = colors[frame[i]];
    }

    // Palette entry -> BGRA. The first 16 entries come from the palette ROM (the C64 colors) unless PAL is set
    uint[] ResolvePalette()
    {
        var colors = new uint[256];
        bool ram = !NewMode || (registers[REG_CONTROL_A] & A_PAL) != 0;
        bool mono = NewMode && (registers[REG_CONTROL_B] & B_MONO) != 0;
        for (int i = 0; i < 256; i++)
        {
            int r, g, b;
            if (i < 16 && !ram)
            {
                r = RomPalette[i][0];
                g = RomPalette[i][1];
                b = RomPalette[i][2];
            }
            else
            {
                r = paletteRam[0][i] & 0x0F;
                g = paletteRam[1][i] & 0x0F;
                b = paletteRam[2][i] & 0x0F;
            }
            r *= 17; g *= 17; b *= 17;
            if (mono) r = g = b = (r * 30 + g * 59 + b * 11) / 100;
            colors[i] = 0xFF000000u | (uint)(r << 16 | g << 8 | b);
        }
        return colors;
    }

    // The 16 colors of the palette ROM (red, green, blue: 4 bits each)
    static readonly int[][] RomPalette =
    [
        [0x0, 0x0, 0x0], // black
        [0xF, 0xF, 0xF], // white
        [0xF, 0x0, 0x0], // red
        [0x0, 0xF, 0xF], // cyan
        [0xF, 0x0, 0xF], // purple
        [0x0, 0xF, 0x0], // green
        [0x0, 0x0, 0xF], // blue
        [0xF, 0xF, 0x0], // yellow
        [0xF, 0x6, 0x0], // orange
        [0xA, 0x4, 0x0], // brown
        [0xF, 0x7, 0x7], // pink
        [0x5, 0x5, 0x5], // dark grey
        [0x8, 0x8, 0x8], // medium grey
        [0x9, 0xF, 0x9], // light green
        [0x9, 0x9, 0xF], // light blue
        [0xB, 0xB, 0xB], // light grey
    ];
}
