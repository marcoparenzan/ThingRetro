namespace Fake64.Mos;

// VIC-II rendering into a frame buffer of color indexes. The VIC-II outputs 8 pixels per cycle: when the CPU
// writes a register in the middle of a line, the pixels already "under the beam" are drawn with the old values
// (CatchUp) and the rest of the line with the new ones. So a raster split falls at the right point of the line,
// with the precision of a cycle, instead of moving whole lines
public partial class MOS6569
{
    // Visible area: 42 pixels of border left/top, 320x200 of display, 41/42 right/bottom
    public const int SCREEN_WIDTH = 403;
    public const int SCREEN_HEIGHT = 284;
    const int FIRST_VISIBLE_LINE = 9;        // raster line shown on the first line of the frame buffer
    const int DISPLAY_LEFT = 42;             // frame buffer column matching sprite X 24
    const int SPRITE_X_TO_SCREEN = DISPLAY_LEFT - 24;
    const int FIRST_TEXT_LINE = 0x30;        // text row 0 starts at raster line $30 + YSCROLL
    const int LINE_X_POSITIONS = 504;        // X coordinates in a line: 63 cycles x 8 pixels

    readonly byte[] frame = new byte[SCREEN_WIDTH * SCREEN_HEIGHT];

    // Working buffers for one line
    readonly byte[] lineColor = new byte[SCREEN_WIDTH];
    readonly bool[] lineForeground = new bool[SCREEN_WIDTH]; // foreground pixels (sprite priority and collisions)
    readonly byte[] lineSprites = new byte[SCREEN_WIDTH];    // mask of the sprites present in each pixel

    // Pixels of the current line already drawn into the frame buffer
    int renderedTo;

    // Color index (0-15) of a pixel of the last frame drawn
    internal byte PixelAt(int x, int y) => frame[y * SCREEN_WIDTH + x];

    // Frame buffer column output in the current cycle: 8 pixels per cycle, sprite X 24
    // (column 42) comes out in cycle 16 (counting from 0)
    int BeamX => Math.Clamp(8 * currentCycle - 86, 0, SCREEN_WIDTH);

    // Before a register write: draws the pixels already output on this line with the old values.
    // The border advances on lines outside the frame buffer too, because its state carries over between lines
    private void CatchUp()
    {
        int x = BeamX;
        if (x <= renderedTo) return;
        DrawUpTo(x, detectCollisions: false);
    }

    // End of line: draws the remaining pixels, detects sprite collisions, then the border check of cycle 63
    private void RenderLine(int line)
    {
        DrawUpTo(SCREEN_WIDTH, detectCollisions: true);
        EndOfLineBorder(line);
        renderedTo = 0;
    }

    private void DrawUpTo(int x, bool detectCollisions)
    {
        int y = rasterLine - FIRST_VISIBLE_LINE;
        bool visible = y >= 0 && y < SCREEN_HEIGHT;
        if (visible) ComposeLine(rasterLine, detectCollisions);
        ApplyBorder(rasterLine, renderedTo, x, visible);
        if (visible) Array.Copy(lineColor, renderedTo, frame, y * SCREEN_WIDTH + renderedTo, x - renderedTo);
        renderedTo = x;
    }

    // Computes the whole line (graphics and sprites, without border) with the current registers into the working buffers
    private void ComposeLine(int line, bool detectCollisions)
    {
        byte control1 = registers[REG_CONTROL_1];
        byte control2 = registers[REG_CONTROL_2];

        Array.Fill(lineColor, (byte)(registers[REG_BACKGROUND_COLOR] & 0x0F));
        Array.Clear(lineForeground);

        RenderGraphics(line, control1, control2);
        RenderSprites(detectCollisions);
    }

    // Video bus reads; with nothing connected the bus stays high
    private byte ReadMemory(int address) => Memory?.Read(address & 0x3FFF) ?? 0xFF;
    private byte ReadColor(int address) => (byte)((Memory?.ReadColor(address & 0x03FF) ?? 0x0F) & 0x0F);

    // Outside the 200 display lines (or in a frame without bad lines) the graphics sequencer is "idle": it repeats
    // the byte at the last address of the bank ($3FFF, $39FF in extended color) with the color data at zero.
    // Normally the border covers it; it shows when the border is open, which is why demos clear $3FFF
    private void RenderGraphics(int line, byte control1, byte control2)
    {
        int graphicsLine = line - (FIRST_TEXT_LINE + (control1 & 0x07)); // YSCROLL
        bool idle = !badLinesEnabled || graphicsLine < 0 || graphicsLine >= 200;

        int row = graphicsLine >> 3;
        int charLine = graphicsLine & 0x07;
        int xStart = DISPLAY_LEFT + (control2 & 0x07); // XSCROLL

        bool ecm = (control1 & 0x40) != 0;
        bool bmm = (control1 & 0x20) != 0;
        bool mcm = (control2 & 0x10) != 0;

        byte memoryPointers = registers[REG_MEMORY_POINTERS];
        int screenBase = (memoryPointers & 0xF0) << 6;   // $0000-$3C00 in steps of $0400
        int charBase = (memoryPointers & 0x0E) << 10;    // $0000-$3800 in steps of $0800
        int bitmapBase = (memoryPointers & 0x08) << 10;  // $0000 o $2000

        byte bg0 = (byte)(registers[REG_BACKGROUND_COLOR] & 0x0F);
        byte bg1 = (byte)(registers[REG_BACKGROUND_COLOR_1] & 0x0F);
        byte bg2 = (byte)(registers[REG_BACKGROUND_COLOR_2] & 0x0F);
        byte bg3 = (byte)(registers[REG_BACKGROUND_COLOR_3] & 0x0F);

        byte idleBits = ReadMemory(ecm ? 0x39FF : 0x3FFF);

        for (int column = 0; column < 40; column++)
        {
            int cell = row * 40 + column;
            byte screenCode = idle ? (byte)0 : ReadMemory(screenBase + cell);
            byte color = idle ? (byte)0 : ReadColor(cell);
            int x = xStart + column * 8;

            if (idle)
            {
                if (ecm && (bmm || mcm)) PutInvalid(x, idleBits, mcm);
                else if (bmm && mcm) PutMulticolor(x, idleBits, bg0, 0, 0, 0);
                else PutHires(x, idleBits, 0, bmm ? (byte)0 : bg0);
            }
            else if (bmm)
            {
                byte bits = ReadMemory(bitmapBase + cell * 8 + charLine);
                if (ecm)
                    PutInvalid(x, bits, mcm);
                else if (mcm)
                    PutMulticolor(x, bits, bg0, (byte)(screenCode >> 4), (byte)(screenCode & 0x0F), color);
                else
                    PutHires(x, bits, (byte)(screenCode >> 4), (byte)(screenCode & 0x0F));
            }
            else if (ecm)
            {
                // Extended color: bits 6-7 of the code select the background, only 64 characters
                byte bits = ReadMemory(charBase + (screenCode & 0x3F) * 8 + charLine);
                if (mcm)
                    PutInvalid(x, bits, true);
                else
                {
                    byte background = (screenCode >> 6) switch { 0 => bg0, 1 => bg1, 2 => bg2, _ => bg3 };
                    PutHires(x, bits, color, background);
                }
            }
            else
            {
                byte bits = ReadMemory(charBase + screenCode * 8 + charLine);
                if (mcm && (color & 0x08) != 0)
                    PutMulticolor(x, bits, bg0, bg1, bg2, (byte)(color & 0x07));
                else
                    PutHires(x, bits, color, bg0);
            }
        }
    }

    // 8 pixels: bit 1 = foreground, bit 0 = background
    private void PutHires(int x, byte bits, byte foreground, byte background)
    {
        for (int i = 0; i < 8; i++, x++)
        {
            if (x >= SCREEN_WIDTH) return;
            bool set = (bits & (0x80 >> i)) != 0;
            lineColor[x] = set ? foreground : background;
            lineForeground[x] = set;
        }
    }

    // 4 bit pairs, each 2 pixels wide: 00 and 01 are background, 10 and 11 foreground
    private void PutMulticolor(int x, byte bits, byte color00, byte color01, byte color10, byte color11)
    {
        for (int pair = 0; pair < 4; pair++)
        {
            int value = (bits >> (6 - pair * 2)) & 0x03;
            byte color = value switch { 0 => color00, 1 => color01, 2 => color10, _ => color11 };
            for (int k = 0; k < 2; k++, x++)
            {
                if (x >= SCREEN_WIDTH) return;
                lineColor[x] = color;
                lineForeground[x] = value >= 2;
            }
        }
    }

    // Invalid modes (ECM together with BMM or MCM): black screen, but foreground pixels count for collisions
    private void PutInvalid(int x, byte bits, bool multicolor)
    {
        for (int i = 0; i < 8; i++, x++)
        {
            if (x >= SCREEN_WIDTH) return;
            lineColor[x] = 0;
            lineForeground[x] = multicolor
                ? ((bits >> (6 - (i & 6))) & 0x02) != 0
                : (bits & (0x80 >> i)) != 0;
        }
    }

    // The visible sprites and the data row of each come from the DMA logic (MOS6569.Sprites.cs)
    private void RenderSprites(bool detectCollisions)
    {
        byte visible = spriteVisible;
        if (visible == 0) return;

        Array.Clear(lineSprites);
        int screenBase = (registers[REG_MEMORY_POINTERS] & 0xF0) << 6;

        // The lowest-numbered sprite wins: a pixel already taken is not drawn again
        for (int n = 0; n < 8; n++)
        {
            int bit = 1 << n;
            if ((visible & bit) == 0) continue;

            int pointer = ReadMemory(screenBase + 0x3F8 + n);
            int dataAddress = pointer * 64 + spriteData[n];
            int data = ReadMemory(dataAddress) << 16 | ReadMemory(dataAddress + 1) << 8 | ReadMemory(dataAddress + 2);
            if (data == 0) continue;

            // On a PAL line X goes from 0 to 503 (positions $1F8-$1FF are never reached); the visible left border
            // starts at X 486, so sprites with X 486-503 appear to the left of X 0
            int spriteX = registers[REG_SPRITE0_X + n * 2] | ((registers[REG_SPRITE_X_MSB] & bit) != 0 ? 0x100 : 0);
            if (spriteX >= LINE_X_POSITIONS) continue;
            int x = (spriteX >= LINE_X_POSITIONS - SPRITE_X_TO_SCREEN ? spriteX - LINE_X_POSITIONS : spriteX) + SPRITE_X_TO_SCREEN;
            int pixelWidth = (registers[REG_SPRITE_X_EXPAND] & bit) != 0 ? 2 : 1;
            bool behindForeground = (registers[REG_SPRITE_PRIORITY] & bit) != 0;
            byte spriteColor = (byte)(registers[REG_SPRITE0_COLOR + n] & 0x0F);

            if ((registers[REG_SPRITE_MULTICOLOR] & bit) != 0)
            {
                byte multicolor0 = (byte)(registers[REG_SPRITE_MULTICOLOR_0] & 0x0F);
                byte multicolor1 = (byte)(registers[REG_SPRITE_MULTICOLOR_1] & 0x0F);
                for (int pair = 0; pair < 12; pair++)
                {
                    int value = (data >> (22 - pair * 2)) & 0x03;
                    if (value == 0) continue;
                    byte color = value switch { 1 => multicolor0, 2 => spriteColor, _ => multicolor1 };
                    PutSpritePixels(x + pair * 2 * pixelWidth, 2 * pixelWidth, color, bit, behindForeground);
                }
            }
            else
            {
                for (int i = 0; i < 24; i++)
                {
                    if ((data & (0x800000 >> i)) != 0)
                        PutSpritePixels(x + i * pixelWidth, pixelWidth, spriteColor, bit, behindForeground);
                }
            }
        }

        if (detectCollisions) DetectCollisions();
    }

    private void PutSpritePixels(int x, int count, byte color, int bit, bool behindForeground)
    {
        for (int k = 0; k < count; k++, x++)
        {
            if (x < 0 || x >= SCREEN_WIDTH) continue;
            bool claimedByLowerSprite = lineSprites[x] != 0;
            lineSprites[x] |= (byte)bit;
            if (claimedByLowerSprite) continue;
            if (behindForeground && lineForeground[x]) continue;
            lineColor[x] = color;
        }
    }

    // Collisions: the registers accumulate bits until they are read; the interrupt
    // triggers only when the register goes from zero to non-zero
    private void DetectCollisions()
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

    // The border is made of two flip-flops, as in the chip:
    // - vertical: set when the raster reaches the bottom line (251, or 247 with 24 rows) and cleared at the top
    //   line (51, or 55) if DEN is on. The check happens at cycle 63 and when the beam reaches the left compare;
    // - main (the one you see): set when the beam reaches the right compare (X 344, or 335 with 38 columns) and
    //   cleared at the left one (X 24, or 31), but only if the vertical one is clear.
    // The compares are equalities: if a register changes when the value that would trigger the border has already
    // gone by, the border is not set. Switching to 24 rows between lines 247 and 251 leaves the top and bottom
    // borders open; switching to 38 columns in cycle 56 (between X 335 and 344) opens the side borders of that line
    bool mainBorder = true, verticalBorder = true;

    (int Top, int Bottom) BorderRows() => (registers[REG_CONTROL_1] & 0x08) != 0 ? (0x33, 0xFB) : (0x37, 0xF7);

    // Advances the border over pixels [from, to) of the line, drawing it where it is set
    private void ApplyBorder(int line, int from, int to, bool visible)
    {
        bool columns40 = (registers[REG_CONTROL_2] & 0x08) != 0;
        int left = DISPLAY_LEFT + (columns40 ? 0 : 7);
        int right = DISPLAY_LEFT + (columns40 ? 320 : 311);
        byte border = (byte)(registers[REG_BORDER_COLOR] & 0x0F);
        int position = from;

        void Fill(int end)
        {
            if (visible && mainBorder && end > position) Array.Fill(lineColor, border, position, end - position);
            position = Math.Max(position, end);
        }

        if (left >= from && left < to)
        {
            Fill(left);
            var (top, bottom) = BorderRows();
            if (line == bottom) verticalBorder = true;
            else if (line == top && (registers[REG_CONTROL_1] & 0x10) != 0) verticalBorder = false;
            if (!verticalBorder) mainBorder = false;
        }
        if (right >= from && right < to)
        {
            Fill(right);
            mainBorder = true;
        }
        Fill(to);
    }

    // Cycle 63: vertical check at the end of the line
    private void EndOfLineBorder(int line)
    {
        var (top, bottom) = BorderRows();
        if (line == bottom) verticalBorder = true;
        else if (line == top && (registers[REG_CONTROL_1] & 0x10) != 0) verticalBorder = false;
    }

    // Copies the last frame into the window's 32-bit (BGRA) bitmap
    unsafe public void Render(byte* scan0)
    {
        uint* pixels = (uint*)scan0;
        for (int i = 0; i < frame.Length; i++)
            pixels[i] = palette[frame[i]];
    }

    // http://unusedino.de/ec64/technical/misc/vic656x/colors/ (BGRA little-endian, alfa a 255)
    static readonly uint[] palette =
    [
        0xFF000000, // 0 - Black
        0xFFFFFFFF, // 1 - White
        0xFF68372B, // 2 - Red
        0xFF70A4B2, // 3 - Cyan
        0xFF6F3D86, // 4 - Purple
        0xFF588D43, // 5 - Green
        0xFF352879, // 6 - Blue
        0xFFB8C76F, // 7 - Yellow
        0xFF6F4F25, // 8 - Orange
        0xFF433900, // 9 - Brown
        0xFF9A6759, // A - Light Red
        0xFF444444, // B - Dark Grey
        0xFF6C6C6C, // C - Grey
        0xFF9AD284, // D - Light Green
        0xFF6C5EB5, // E - Light Blue
        0xFF959595, // F - Light Grey
    ];
}
