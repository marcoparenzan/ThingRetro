namespace Fake64;

// Rendering del VIC-II: ogni riga raster viene disegnata alla sua fine in un frame buffer di indici
// colore, così i cambi di registro a metà frame (split raster) sono visibili con risoluzione di riga
public partial class MOS6569
{
    // Area visibile: 42 pixel di bordo a sinistra/sopra, 320x200 di display, 41/42 a destra/sotto
    public const int SCREEN_WIDTH = 403;
    public const int SCREEN_HEIGHT = 284;
    const int FIRST_VISIBLE_LINE = 9;        // riga raster mostrata nella prima riga del frame buffer
    const int DISPLAY_LEFT = 42;             // colonna del frame buffer che corrisponde alla X sprite 24
    const int SPRITE_X_TO_SCREEN = DISPLAY_LEFT - 24;
    const int FIRST_TEXT_LINE = 0x30;        // la riga di testo 0 inizia alla riga raster $30 + YSCROLL

    readonly byte[] frame = new byte[SCREEN_WIDTH * SCREEN_HEIGHT];

    // Buffer di lavoro per una riga
    readonly byte[] lineColor = new byte[SCREEN_WIDTH];
    readonly bool[] lineForeground = new bool[SCREEN_WIDTH]; // pixel di primo piano (priorità e collisioni sprite)
    readonly byte[] lineSprites = new byte[SCREEN_WIDTH];    // maschera degli sprite presenti in ogni pixel

    // Indice colore (0-15) di un pixel dell'ultimo frame disegnato
    internal byte PixelAt(int x, int y) => frame[y * SCREEN_WIDTH + x];

    private void RenderLine(int line)
    {
        int y = line - FIRST_VISIBLE_LINE;
        if (y < 0 || y >= SCREEN_HEIGHT) return;

        byte control1 = registers[REG_CONTROL_1];
        byte control2 = registers[REG_CONTROL_2];
        int bank = board.VicBank;

        Array.Fill(lineColor, (byte)(registers[REG_BACKGROUND_COLOR] & 0x0F));
        Array.Clear(lineForeground);

        RenderGraphics(line, bank, control1, control2);
        RenderSprites(line, bank);
        RenderBorder(line, control1, control2);

        Array.Copy(lineColor, 0, frame, y * SCREEN_WIDTH, SCREEN_WIDTH);
    }

    private void RenderGraphics(int line, int bank, byte control1, byte control2)
    {
        int graphicsLine = line - (FIRST_TEXT_LINE + (control1 & 0x07)); // YSCROLL
        if (graphicsLine < 0 || graphicsLine >= 200) return;

        int row = graphicsLine >> 3;
        int charLine = graphicsLine & 0x07;
        int xStart = DISPLAY_LEFT + (control2 & 0x07); // XSCROLL

        bool ecm = (control1 & 0x40) != 0;
        bool bmm = (control1 & 0x20) != 0;
        bool mcm = (control2 & 0x10) != 0;

        byte memoryPointers = registers[REG_MEMORY_POINTERS];
        int screenBase = (memoryPointers & 0xF0) << 6;   // $0000-$3C00 a passi di $0400
        int charBase = (memoryPointers & 0x0E) << 10;    // $0000-$3800 a passi di $0800
        int bitmapBase = (memoryPointers & 0x08) << 10;  // $0000 o $2000

        byte bg0 = (byte)(registers[REG_BACKGROUND_COLOR] & 0x0F);
        byte bg1 = (byte)(registers[REG_BACKGROUND_COLOR_1] & 0x0F);
        byte bg2 = (byte)(registers[REG_BACKGROUND_COLOR_2] & 0x0F);
        byte bg3 = (byte)(registers[REG_BACKGROUND_COLOR_3] & 0x0F);

        for (int column = 0; column < 40; column++)
        {
            int cell = row * 40 + column;
            byte screenCode = board.VicRead(bank, screenBase + cell);
            byte color = board.VicColorRam(cell);
            int x = xStart + column * 8;

            if (bmm)
            {
                byte bits = board.VicRead(bank, bitmapBase + cell * 8 + charLine);
                if (ecm)
                    PutInvalid(x, bits, mcm);
                else if (mcm)
                    PutMulticolor(x, bits, bg0, (byte)(screenCode >> 4), (byte)(screenCode & 0x0F), color);
                else
                    PutHires(x, bits, (byte)(screenCode >> 4), (byte)(screenCode & 0x0F));
            }
            else if (ecm)
            {
                // Extended color: i bit 6-7 del codice scelgono lo sfondo, solo 64 caratteri
                byte bits = board.VicRead(bank, charBase + (screenCode & 0x3F) * 8 + charLine);
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
                byte bits = board.VicRead(bank, charBase + screenCode * 8 + charLine);
                if (mcm && (color & 0x08) != 0)
                    PutMulticolor(x, bits, bg0, bg1, bg2, (byte)(color & 0x07));
                else
                    PutHires(x, bits, color, bg0);
            }
        }
    }

    // 8 pixel: bit a 1 = primo piano, bit a 0 = sfondo
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

    // 4 coppie di bit, ciascuna larga 2 pixel: 00 e 01 sono sfondo, 10 e 11 primo piano
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

    // Modi non validi (ECM insieme a BMM o MCM): schermo nero, ma i pixel di primo piano contano per le collisioni
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

    private void RenderSprites(int line, int bank)
    {
        byte enabled = registers[REG_SPRITE_ENABLE];
        if (enabled == 0) return;

        Array.Clear(lineSprites);
        int screenBase = (registers[REG_MEMORY_POINTERS] & 0xF0) << 6;

        // Lo sprite con numero più basso ha la precedenza: un pixel già occupato non viene ridisegnato
        for (int n = 0; n < 8; n++)
        {
            int bit = 1 << n;
            if ((enabled & bit) == 0) continue;

            bool expandY = (registers[REG_SPRITE_Y_EXPAND] & bit) != 0;
            int spriteLine = line - (registers[REG_SPRITE0_Y + n * 2] + 1); // la coordinata Y 50 allinea lo sprite alla riga $33
            if (spriteLine < 0 || spriteLine >= (expandY ? 42 : 21)) continue;
            if (expandY) spriteLine >>= 1;

            int pointer = board.VicRead(bank, screenBase + 0x3F8 + n);
            int dataAddress = pointer * 64 + spriteLine * 3;
            int data = board.VicRead(bank, dataAddress) << 16 | board.VicRead(bank, dataAddress + 1) << 8 | board.VicRead(bank, dataAddress + 2);
            if (data == 0) continue;

            int spriteX = registers[REG_SPRITE0_X + n * 2] | ((registers[REG_SPRITE_X_MSB] & bit) != 0 ? 0x100 : 0);
            int x = spriteX + SPRITE_X_TO_SCREEN;
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

        DetectCollisions();
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

    // Collisioni: i registri accumulano i bit finché non vengono letti; l'interrupt
    // scatta solo quando il registro passa da zero a non-zero
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

    // Bordo: copre tutto fuori dalla finestra di display (40/38 colonne, 25/24 righe), o tutta la riga con DEN = 0
    private void RenderBorder(int line, byte control1, byte control2)
    {
        bool rows25 = (control1 & 0x08) != 0;
        int top = rows25 ? 0x33 : 0x37;
        int bottom = rows25 ? 0xFB : 0xF7;
        bool columns40 = (control2 & 0x08) != 0;
        int left = DISPLAY_LEFT + (columns40 ? 0 : 7);
        int right = DISPLAY_LEFT + (columns40 ? 320 : 311);

        byte border = (byte)(registers[REG_BORDER_COLOR] & 0x0F);
        bool displayEnabled = (control1 & 0x10) != 0;

        if (!displayEnabled || line < top || line >= bottom)
        {
            Array.Fill(lineColor, border);
            return;
        }
        Array.Fill(lineColor, border, 0, left);
        Array.Fill(lineColor, border, right, SCREEN_WIDTH - right);
    }

    // Copia l'ultimo frame nella bitmap 32 bit (BGRA) della finestra
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
