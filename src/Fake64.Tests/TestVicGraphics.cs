using static Fake64.Tests.TestHelpers;

namespace Fake64.Tests;

[TestClass]
public sealed class VicGraphicsTests
{
    const int FRAME_CYCLES = 312 * 63;

    // Angolo in alto a sinistra dell'area di display nel frame buffer (X sprite 24, riga raster $33)
    const int LEFT = 42;
    const int TOP = 42;

    const byte BLACK = 0, WHITE = 1, RED = 2, CYAN = 3, PURPLE = 4, GREEN = 5, BLUE = 6, YELLOW = 7;

    // Board con CPU ferma in un loop e display standard: testo, 25 righe, 40 colonne, YSCROLL 3
    private static Board NewDisplay()
    {
        var board = NewBoard();
        board.ParkCpu();
        board.Poke(0xD011, 0x1B);
        board.Poke(0xD016, 0x08);
        board.Poke(0xD018, 0x14); // schermo $0400, caratteri $1000 (ROM)
        board.Poke(0xD020, RED);
        board.Poke(0xD021, BLUE);
        for (ushort a = 0x0400; a < 0x07E8; a++) board.Poke(a, 0x20); // spazi
        return board;
    }

    private static void RenderFrame(Board board) => board.RunCycles(FRAME_CYCLES);

    private static byte Pixel(Board board, int x, int y) => board.VICII.PixelAt(x, y);

    #region Text modes

    [TestMethod]
    public void StandardText_ShouldDrawCharacterWithColorRam()
    {
        var board = NewDisplay();
        board.Poke(0x0400, 0xA0); // spazio inverso: tutti i pixel accesi
        board.Poke(0xD800, GREEN);
        board.Poke(0x0401, 0x20);

        RenderFrame(board);

        Assert.AreEqual(RED, Pixel(board, 0, 0));                  // bordo
        Assert.AreEqual(RED, Pixel(board, LEFT - 1, TOP));         // bordo a sinistra del display
        Assert.AreEqual(GREEN, Pixel(board, LEFT, TOP));           // primo pixel del carattere
        Assert.AreEqual(GREEN, Pixel(board, LEFT + 7, TOP + 7));   // ultimo pixel del carattere
        Assert.AreEqual(BLUE, Pixel(board, LEFT + 8, TOP));        // carattere successivo: sfondo
    }

    [TestMethod]
    public void ScreenMemory_ShouldFollowD018()
    {
        var board = NewDisplay();
        for (ushort a = 0x0800; a < 0x0BE8; a++) board.Poke(a, 0x20);
        board.Poke(0x0400, 0x20);
        board.Poke(0x0800, 0xA0);
        board.Poke(0xD800, WHITE);
        board.Poke(0xD018, 0x24); // schermo $0800

        RenderFrame(board);

        Assert.AreEqual(WHITE, Pixel(board, LEFT, TOP));
    }

    [TestMethod]
    public void CharacterSet_ShouldBeReadFromRamOutsideRomArea()
    {
        var board = NewDisplay();
        board.Poke(0xD018, 0x1C); // caratteri a $3000 (RAM)
        board.Poke(0x3008, 0x81); // carattere 1, prima riga: pixel 0 e 7
        board.Poke(0x0400, 0x01);
        board.Poke(0xD800, YELLOW);

        RenderFrame(board);

        Assert.AreEqual(YELLOW, Pixel(board, LEFT, TOP));
        Assert.AreEqual(BLUE, Pixel(board, LEFT + 1, TOP));
        Assert.AreEqual(YELLOW, Pixel(board, LEFT + 7, TOP));
    }

    [TestMethod]
    public void VicBank_ShouldFollowCia2PortA_WithoutCharacterRomInBank1()
    {
        var board = NewDisplay();
        board.Poke(0xDD02, 0x03);
        board.Poke(0xDD00, 0x02); // banco 1: $4000-$7FFF
        board.Poke(0xD018, 0x10); // schermo $4400, caratteri $4000
        for (ushort a = 0x4400; a < 0x47E8; a++) board.Poke(a, 0x00);
        board.Poke(0x4000, 0xF0); // carattere 0, prima riga: 4 pixel accesi
        board.Poke(0xD800, CYAN);

        RenderFrame(board);

        Assert.AreEqual(CYAN, Pixel(board, LEFT + 3, TOP));
        Assert.AreEqual(BLUE, Pixel(board, LEFT + 4, TOP));
    }

    [TestMethod]
    public void MulticolorText_ShouldUseColorPairsWhenColorBit3IsSet()
    {
        var board = NewDisplay();
        board.Poke(0xD016, 0x18); // MCM
        board.Poke(0xD022, CYAN);
        board.Poke(0xD023, PURPLE);
        board.Poke(0xD018, 0x1C);
        board.Poke(0x3008, 0x1B); // 00 01 10 11
        board.Poke(0x0400, 0x01);
        board.Poke(0xD800, 0x08 | YELLOW);

        RenderFrame(board);

        Assert.AreEqual(BLUE, Pixel(board, LEFT + 1, TOP));   // 00 -> $D021
        Assert.AreEqual(CYAN, Pixel(board, LEFT + 2, TOP));   // 01 -> $D022
        Assert.AreEqual(PURPLE, Pixel(board, LEFT + 4, TOP)); // 10 -> $D023
        Assert.AreEqual(YELLOW, Pixel(board, LEFT + 7, TOP)); // 11 -> color RAM & 7
    }

    [TestMethod]
    public void ExtendedColorText_ShouldSelectBackgroundFromCodeBits()
    {
        var board = NewDisplay();
        board.Poke(0xD011, 0x5B); // ECM
        board.Poke(0xD023, PURPLE);
        board.Poke(0x0400, 0x80); // carattere 0 ('@') con sfondo $D023
        board.Poke(0xD800, WHITE);

        RenderFrame(board);

        Assert.AreEqual(PURPLE, Pixel(board, LEFT, TOP)); // la prima riga di '@' ha il pixel 0 spento
    }

    #endregion

    #region Bitmap modes

    [TestMethod]
    public void StandardBitmap_ShouldTakeColorsFromScreenMemory()
    {
        var board = NewDisplay();
        board.Poke(0xD011, 0x3B); // BMM
        board.Poke(0xD018, 0x18); // bitmap $2000, colori a $0400
        board.Poke(0x2000, 0x80);
        board.Poke(0x0400, (byte)(WHITE << 4 | RED));

        RenderFrame(board);

        Assert.AreEqual(WHITE, Pixel(board, LEFT, TOP));
        Assert.AreEqual(RED, Pixel(board, LEFT + 1, TOP));
    }

    [TestMethod]
    public void MulticolorBitmap_ShouldUseFourColorSources()
    {
        var board = NewDisplay();
        board.Poke(0xD011, 0x3B);
        board.Poke(0xD016, 0x18);
        board.Poke(0xD018, 0x18);
        board.Poke(0x2000, 0x1B); // 00 01 10 11
        board.Poke(0x0400, (byte)(CYAN << 4 | PURPLE));
        board.Poke(0xD800, YELLOW);

        RenderFrame(board);

        Assert.AreEqual(BLUE, Pixel(board, LEFT, TOP));       // 00 -> $D021
        Assert.AreEqual(CYAN, Pixel(board, LEFT + 2, TOP));   // 01 -> nibble alto
        Assert.AreEqual(PURPLE, Pixel(board, LEFT + 4, TOP)); // 10 -> nibble basso
        Assert.AreEqual(YELLOW, Pixel(board, LEFT + 6, TOP)); // 11 -> color RAM
    }

    [TestMethod]
    public void InvalidMode_ShouldDrawBlack()
    {
        var board = NewDisplay();
        board.Poke(0xD011, 0x7B); // ECM + BMM
        board.Poke(0xD018, 0x18);
        board.Poke(0x2000, 0xFF);

        RenderFrame(board);

        Assert.AreEqual(BLACK, Pixel(board, LEFT, TOP));
        Assert.AreEqual(BLACK, Pixel(board, LEFT + 20, TOP + 20));
    }

    #endregion

    #region Border and scrolling

    [TestMethod]
    public void Border_38Columns24Rows_ShouldCoverEdges()
    {
        var board = NewDisplay();
        board.Poke(0xD011, 0x13); // 24 righe
        board.Poke(0xD016, 0x00); // 38 colonne

        RenderFrame(board);

        Assert.AreEqual(RED, Pixel(board, LEFT + 6, TOP + 10));
        Assert.AreEqual(BLUE, Pixel(board, LEFT + 7, TOP + 10));
        Assert.AreEqual(RED, Pixel(board, LEFT + 311, TOP + 10));
        Assert.AreEqual(RED, Pixel(board, LEFT + 20, TOP + 3));
        Assert.AreEqual(BLUE, Pixel(board, LEFT + 20, TOP + 4));
    }

    [TestMethod]
    public void DisplayDisabled_ShouldShowOnlyBorder()
    {
        var board = NewDisplay();
        board.Poke(0xD011, 0x0B);

        RenderFrame(board);

        Assert.AreEqual(RED, Pixel(board, LEFT + 100, TOP + 100));
    }

    [TestMethod]
    public void FineScroll_ShouldShiftGraphics()
    {
        var board = NewDisplay();
        board.Poke(0xD011, 0x1D); // YSCROLL 5
        board.Poke(0xD016, 0x0A); // XSCROLL 2
        board.Poke(0x0400, 0xA0);
        board.Poke(0xD800, WHITE);

        RenderFrame(board);

        Assert.AreEqual(BLUE, Pixel(board, LEFT + 1, TOP + 2));
        Assert.AreEqual(WHITE, Pixel(board, LEFT + 2, TOP + 2));
        Assert.AreEqual(BLUE, Pixel(board, LEFT + 2, TOP + 1));
    }

    [TestMethod]
    public void RasterSplit_ShouldChangeColorMidFrame()
    {
        var board = NewDisplay();
        board.RunCycles(100 * 63); // inizio della riga 100
        board.Poke(0xD020, WHITE);
        board.RunCycles(FRAME_CYCLES - 100 * 63);

        Assert.AreEqual(RED, Pixel(board, 0, 99 - 9));
        Assert.AreEqual(WHITE, Pixel(board, 0, 100 - 9));
    }

    #endregion

    #region Sprites

    private static void PlaceSprite(Board board, int n, int x, int y, byte color, ushort dataAddress, params byte[] data)
    {
        board.Poke((ushort)(0x07F8 + n), (byte)(dataAddress / 64));
        board.Poke(dataAddress, new byte[63]);
        board.Poke(dataAddress, data);
        board.Poke((ushort)(0xD000 + n * 2), (byte)x);
        board.Poke((ushort)(0xD001 + n * 2), (byte)y);
        if (x > 0xFF) board.Poke(0xD010, (byte)(board.Peek(0xD010) | 1 << n));
        board.Poke((ushort)(0xD027 + n), color);
        board.Poke(0xD015, (byte)(board.Peek(0xD015) | 1 << n));
    }

    [TestMethod]
    public void Sprite_ShouldBeDrawnAtItsCoordinates()
    {
        var board = NewDisplay();
        PlaceSprite(board, 0, 24, 50, YELLOW, 0x0340, 0x80); // primo pixel della prima riga

        RenderFrame(board);

        Assert.AreEqual(YELLOW, Pixel(board, LEFT, TOP));
        Assert.AreEqual(BLUE, Pixel(board, LEFT + 1, TOP));
        Assert.AreEqual(BLUE, Pixel(board, LEFT, TOP + 1));
    }

    [TestMethod]
    public void Sprite_Expansion_ShouldDoublePixels()
    {
        var board = NewDisplay();
        PlaceSprite(board, 0, 24, 50, YELLOW, 0x0340, 0x80);
        board.Poke(0xD01D, 0x01);
        board.Poke(0xD017, 0x01);

        RenderFrame(board);

        Assert.AreEqual(YELLOW, Pixel(board, LEFT + 1, TOP));
        Assert.AreEqual(YELLOW, Pixel(board, LEFT, TOP + 1));
        Assert.AreEqual(BLUE, Pixel(board, LEFT + 2, TOP));
    }

    [TestMethod]
    public void Sprite_Multicolor_ShouldUseSharedAndOwnColors()
    {
        var board = NewDisplay();
        PlaceSprite(board, 0, 24, 50, YELLOW, 0x0340, 0x1B); // 00 01 10 11
        board.Poke(0xD01C, 0x01);
        board.Poke(0xD025, CYAN);
        board.Poke(0xD026, PURPLE);

        RenderFrame(board);

        Assert.AreEqual(BLUE, Pixel(board, LEFT, TOP));        // 00 trasparente
        Assert.AreEqual(CYAN, Pixel(board, LEFT + 2, TOP));    // 01 -> $D025
        Assert.AreEqual(YELLOW, Pixel(board, LEFT + 4, TOP));  // 10 -> colore dello sprite
        Assert.AreEqual(PURPLE, Pixel(board, LEFT + 6, TOP));  // 11 -> $D026
    }

    [TestMethod]
    public void Sprite_BehindForeground_ShouldBeHiddenByCharacterPixels()
    {
        var board = NewDisplay();
        board.Poke(0x0400, 0xA0);
        board.Poke(0xD800, WHITE);
        PlaceSprite(board, 0, 24, 50, YELLOW, 0x0340, 0xFF, 0xFF);
        board.Poke(0xD01B, 0x01);

        RenderFrame(board);

        Assert.AreEqual(WHITE, Pixel(board, LEFT, TOP));       // il carattere copre lo sprite
        Assert.AreEqual(YELLOW, Pixel(board, LEFT + 8, TOP));  // sopra lo sfondo lo sprite è visibile
    }

    [TestMethod]
    public void Sprite_LowerNumberShouldBeOnTop()
    {
        var board = NewDisplay();
        PlaceSprite(board, 1, 24, 50, CYAN, 0x0380, 0x80);
        PlaceSprite(board, 0, 24, 50, YELLOW, 0x0340, 0x80);

        RenderFrame(board);

        Assert.AreEqual(YELLOW, Pixel(board, LEFT, TOP));
    }

    [TestMethod]
    public void Sprite_HiddenByBorder()
    {
        var board = NewDisplay();
        PlaceSprite(board, 0, 10, 50, YELLOW, 0x0340, 0xFF, 0xFF, 0xFF); // in parte sul bordo sinistro

        RenderFrame(board);

        Assert.AreEqual(RED, Pixel(board, LEFT - 1, TOP));
        Assert.AreEqual(YELLOW, Pixel(board, LEFT, TOP));
    }

    [TestMethod]
    public void SpriteSpriteCollision_ShouldSetRegisterAndInterrupt()
    {
        var board = NewDisplay();
        board.Poke(0xD01A, 0x04);
        PlaceSprite(board, 0, 100, 100, YELLOW, 0x0340, 0x80);
        PlaceSprite(board, 1, 100, 100, CYAN, 0x0380, 0x80);
        PlaceSprite(board, 2, 200, 100, CYAN, 0x03C0, 0x80);

        RenderFrame(board);

        Assert.AreEqual(0x84, board.Peek(0xD019) & 0x84);
        Assert.AreEqual(0x03, (int)board.Peek(0xD01E));
        Assert.AreEqual(0x00, (int)board.Peek(0xD01E)); // la lettura azzera il registro
    }

    [TestMethod]
    public void SpriteBackgroundCollision_ShouldSetRegister()
    {
        var board = NewDisplay();
        board.Poke(0x0400, 0xA0);
        PlaceSprite(board, 3, 24, 50, YELLOW, 0x0340, 0x80);

        RenderFrame(board);

        Assert.AreEqual(0x08, (int)board.Peek(0xD01F));
        Assert.AreEqual(0x02, board.Peek(0xD019) & 0x02);
    }

    #endregion

    [TestMethod]
    public void Boot_ShouldRenderReadyScreen()
    {
        var board = NewBoard();
        board.RunCycles(3 * 985248);

        const byte LIGHT_BLUE = 14;
        Assert.AreEqual(LIGHT_BLUE, Pixel(board, 0, 0));          // bordo
        Assert.AreEqual(BLUE, Pixel(board, LEFT, TOP));           // sfondo

        // "READY." è alla riga di testo 5: la 'R' ha pixel di primo piano nella sua cella
        int foreground = 0;
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                if (Pixel(board, LEFT + x, TOP + 5 * 8 + y) == LIGHT_BLUE) foreground++;
        Assert.IsTrue(foreground > 10, $"pixel accesi nella 'R': {foreground}");
    }
}
