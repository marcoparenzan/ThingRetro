using static Thing65.Tests.TestHelpers;

namespace Thing65.Tests;

[TestClass]
public sealed class VicGraphicsTests
{
    const int FRAME_CYCLES = 312 * 63;

    // Top left corner of the display area in the frame buffer (sprite X 24, raster line $33)
    const int LEFT = 42;
    const int TOP = 42;

    const byte BLACK = 0, WHITE = 1, RED = 2, CYAN = 3, PURPLE = 4, GREEN = 5, BLUE = 6, YELLOW = 7;

    // Board with the CPU parked in a loop and a standard display: text, 25 rows, 40 columns, YSCROLL 3
    private static Board NewDisplay()
    {
        var board = NewBoard();
        board.ParkCpu();
        board.Poke(0xD011, 0x1B);
        board.Poke(0xD016, 0x08);
        board.Poke(0xD018, 0x14); // screen $0400, characters $1000 (ROM)
        board.Poke(0xD020, RED);
        board.Poke(0xD021, BLUE);
        for (ushort a = 0x0400; a < 0x07E8; a++) board.Poke(a, 0x20); // spaces
        return board;
    }

    private static void RenderFrame(Board board) => board.RunCycles(FRAME_CYCLES);

    private static byte Pixel(Board board, int x, int y) => board.Vic.PixelAt(x, y);

    #region Text modes

    [TestMethod]
    public void StandardText_ShouldDrawCharacterWithColorRam()
    {
        var board = NewDisplay();
        board.Poke(0x0400, 0xA0); // reversed space: all pixels on
        board.Poke(0xD800, GREEN);
        board.Poke(0x0401, 0x20);

        RenderFrame(board);

        Assert.AreEqual(RED, Pixel(board, 0, 0));                  // border
        Assert.AreEqual(RED, Pixel(board, LEFT - 1, TOP));         // border left of the display
        Assert.AreEqual(GREEN, Pixel(board, LEFT, TOP));           // first pixel of the character
        Assert.AreEqual(GREEN, Pixel(board, LEFT + 7, TOP + 7));   // last pixel of the character
        Assert.AreEqual(BLUE, Pixel(board, LEFT + 8, TOP));        // next character: background
    }

    [TestMethod]
    public void ScreenMemory_ShouldFollowD018()
    {
        var board = NewDisplay();
        for (ushort a = 0x0800; a < 0x0BE8; a++) board.Poke(a, 0x20);
        board.Poke(0x0400, 0x20);
        board.Poke(0x0800, 0xA0);
        board.Poke(0xD800, WHITE);
        board.Poke(0xD018, 0x24); // screen $0800

        RenderFrame(board);

        Assert.AreEqual(WHITE, Pixel(board, LEFT, TOP));
    }

    [TestMethod]
    public void CharacterSet_ShouldBeReadFromRamOutsideRomArea()
    {
        var board = NewDisplay();
        board.Poke(0xD018, 0x1C); // characters at $3000 (RAM)
        board.Poke(0x3008, 0x81); // character 1, first row: pixels 0 and 7
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
        board.Poke(0xDD00, 0x02); // bank 1: $4000-$7FFF
        board.Poke(0xD018, 0x10); // screen $4400, characters $4000
        for (ushort a = 0x4400; a < 0x47E8; a++) board.Poke(a, 0x00);
        board.Poke(0x4000, 0xF0); // character 0, first row: 4 pixels on
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
        board.Poke(0x0400, 0x80); // character 0 ('@') with background $D023
        board.Poke(0xD800, WHITE);

        RenderFrame(board);

        Assert.AreEqual(PURPLE, Pixel(board, LEFT, TOP)); // the first row of '@' has pixel 0 off
    }

    #endregion

    #region Bitmap modes

    [TestMethod]
    public void StandardBitmap_ShouldTakeColorsFromScreenMemory()
    {
        var board = NewDisplay();
        board.Poke(0xD011, 0x3B); // BMM
        board.Poke(0xD018, 0x18); // bitmap $2000, colors at $0400
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
        Assert.AreEqual(CYAN, Pixel(board, LEFT + 2, TOP));   // 01 -> high nibble
        Assert.AreEqual(PURPLE, Pixel(board, LEFT + 4, TOP)); // 10 -> low nibble
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
        board.Poke(0xD011, 0x13); // 24 rows
        board.Poke(0xD016, 0x00); // 38 columns

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
        board.RunCycles(100 * 63); // start of line 100
        board.Poke(0xD020, WHITE);
        board.RunCycles(FRAME_CYCLES - 100 * 63);

        Assert.AreEqual(RED, Pixel(board, 0, 99 - 9));
        Assert.AreEqual(WHITE, Pixel(board, 0, 100 - 9));
    }

    [TestMethod]
    public void RasterSplit_MidLine_ShouldChangeColorAtBeamPosition()
    {
        var board = NewDisplay();
        board.RunCycles(100 * 63 + 13); // cycle 13 of line 100: the beam is in the left border
        board.Poke(0xD020, WHITE);
        board.RunCycles(FRAME_CYCLES - (100 * 63 + 13));

        int row = 100 - 9;
        int beam = 8 * 13 - 86;          // 8 pixels per cycle: column output in cycle 13
        Assert.AreEqual(RED, Pixel(board, beam - 1, row));
        Assert.AreEqual(WHITE, Pixel(board, beam, row));
        Assert.AreEqual(WHITE, Pixel(board, 0, row + 1));
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
        PlaceSprite(board, 0, 24, 50, YELLOW, 0x0340, 0x80); // first pixel of the first row

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

        Assert.AreEqual(BLUE, Pixel(board, LEFT, TOP));        // 00 transparent
        Assert.AreEqual(CYAN, Pixel(board, LEFT + 2, TOP));    // 01 -> $D025
        Assert.AreEqual(YELLOW, Pixel(board, LEFT + 4, TOP));  // 10 -> sprite color
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

        Assert.AreEqual(WHITE, Pixel(board, LEFT, TOP));       // the character covers the sprite
        Assert.AreEqual(YELLOW, Pixel(board, LEFT + 8, TOP));  // over the background the sprite is visible
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
        PlaceSprite(board, 0, 10, 50, YELLOW, 0x0340, 0xFF, 0xFF, 0xFF); // partly over the left border

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
        Assert.AreEqual(0x00, (int)board.Peek(0xD01E)); // reading clears the register
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

    #region Open borders and sprite DMA

    // A board just created is at line 0, cycle 0: advances to the given cycle (from 0) of a line of the first frame
    private static void RunTo(Board board, int line, int cycle) => board.RunCycles(line * 63 + cycle - board.TotalCycles);

    const int FIRST_VISIBLE_LINE = 9;   // raster line of the first line of the frame buffer

    [TestMethod]
    public void Sprite_ChangingYWhileDisplayed_ShouldNotMoveIt()
    {
        // The DMA follows its own counter: once started, the sprite finishes its 21 lines even if Y changes,
        // and with the new Y it is shown a second time further down (that is how multiplexers work)
        var board = NewDisplay();
        PlaceSprite(board, 0, 100, 60, YELLOW, 0x0340, Enumerable.Repeat((byte)0x80, 63).ToArray());
        int x = 100 - 24 + LEFT;

        RunTo(board, 70, 0);
        board.Poke(0xD001, 150);
        RunTo(board, 312, 0);

        Assert.AreEqual(YELLOW, Pixel(board, x, 61 - FIRST_VISIBLE_LINE));
        Assert.AreEqual(YELLOW, Pixel(board, x, 81 - FIRST_VISIBLE_LINE));
        Assert.AreEqual(BLUE, Pixel(board, x, 82 - FIRST_VISIBLE_LINE));
        Assert.AreEqual(YELLOW, Pixel(board, x, 151 - FIRST_VISIBLE_LINE));
        Assert.AreEqual(YELLOW, Pixel(board, x, 171 - FIRST_VISIBLE_LINE));
    }

    [TestMethod]
    public void TopAndBottomBorder_ShouldOpenWhenRowsChangeBetweenLines247And251()
    {
        var board = NewDisplay();
        board.Poke(0x3FFF, 0x00);   // idle sequencer byte: clean background in the open border
        // Y 0 matches lines 0 and 256 (the compare uses 8 bits): the second time the sprite is in the bottom border
        PlaceSprite(board, 0, 100, 0, YELLOW, 0x0340, 0x80);

        RunTo(board, 249, 0);
        board.Poke(0xD011, 0x13);   // 24 rows: the bottom becomes 247, already gone by, and 251 is no longer compared
        RunTo(board, 270, 0);
        board.Poke(0xD011, 0x1B);   // 25 rows again: 251 has gone by, the border stays open
        RunTo(board, 312, 0);

        int y = 260 - FIRST_VISIBLE_LINE;
        Assert.AreEqual(BLUE, Pixel(board, LEFT + 50, y));                           // background instead of the border
        Assert.AreEqual(RED, Pixel(board, LEFT - 1, y));                             // the side borders stay
        Assert.AreEqual(YELLOW, Pixel(board, 100 - 24 + LEFT, 257 - FIRST_VISIBLE_LINE)); // the sprite in the border
    }

    [TestMethod]
    public void BottomBorder_ShouldStayClosedWithoutTheTrick()
    {
        var board = NewDisplay();
        board.Poke(0x3FFF, 0x00);

        RunTo(board, 245, 0);
        board.Poke(0xD011, 0x13);   // too early: line 247 is still to come and closes the border
        RunTo(board, 312, 0);

        Assert.AreEqual(RED, Pixel(board, LEFT + 50, 260 - FIRST_VISIBLE_LINE));
    }

    [TestMethod]
    [DataRow(55, true)]     // cycle 56 (from 1): X 335 has gone by, X 344 not yet
    [DataRow(54, false)]    // cycle 55: the compare with X 335 is still to come
    public void SideBorders_ShouldOpenWhenColumnsChangeInCycle56(int cycle, bool open)
    {
        const int line = 100;
        var board = NewDisplay();
        PlaceSprite(board, 0, 500, line, YELLOW, 0x0340, 0x80);   // X 500: left of X 0, in the border

        RunTo(board, line, cycle);
        board.Poke(0xD016, 0x00);   // 38 columns
        RunTo(board, line + 1, 30);
        board.Poke(0xD016, 0x08);   // 40 columns, before the right compare of the next line
        RunTo(board, 312, 0);

        int y = line - FIRST_VISIBLE_LINE;
        byte expected = open ? BLUE : RED;
        Assert.AreEqual(expected, Pixel(board, LEFT + 330, y));        // right border of the line
        Assert.AreEqual(expected, Pixel(board, 5, y + 1));             // and left border of the next one
        Assert.AreEqual(open ? YELLOW : RED, Pixel(board, 500 - 504 + LEFT - 24, y + 1)); // sprite at X 500
        Assert.AreEqual(RED, Pixel(board, LEFT + 330, y + 1));         // then the border closes again
        Assert.AreEqual(RED, Pixel(board, 5, y + 2));
    }

    #endregion

    [TestMethod]
    public void Boot_ShouldRenderReadyScreen()
    {
        var board = NewBoard();
        board.RunCycles(3 * 985248);

        const byte LIGHT_BLUE = 14;
        Assert.AreEqual(LIGHT_BLUE, Pixel(board, 0, 0));          // border
        Assert.AreEqual(BLUE, Pixel(board, LEFT, TOP));           // background

        // "READY." is on text row 5: the 'R' has foreground pixels in its cell
        int foreground = 0;
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                if (Pixel(board, LEFT + x, TOP + 5 * 8 + y) == LIGHT_BLUE) foreground++;
        Assert.IsTrue(foreground > 10, $"pixels on in the 'R': {foreground}");
    }
}
