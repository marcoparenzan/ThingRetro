using ThingCommodore.Graphics;
using static Thing64.Tests.TestHelpers;

namespace Thing64.Tests;

// ThingCommodore.Graphics: the memory of the C64 as pictures (a screen, a charset, sprites, a bitmap), to find them in a dump
[TestClass]
public sealed class MemoryViewTests
{
    static readonly C64Palette Palette = C64Palette.Pepto;
    static readonly PaintColors Colors = new(Background: 6, Multicolor1: 14, Multicolor2: 1, Ink: 7);

    static uint Argb(int color) => Palette.Argb(color);

    [TestMethod]
    public void Charset_ShouldDrawHiresCharactersEightBytesEach()
    {
        var ram = new byte[65536];
        // Two characters at $2000: the first has its left pixel on in the first row, the second its right one
        ram[0x2000] = 0x80;
        ram[0x2008] = 0x01;
        var image = MemoryRender.Charset(ram, 0x2000, count: 2, perRow: 2, multicolor: false, Colors);
        Assert.AreEqual(16, image.Width);
        Assert.AreEqual(8, image.Height);
        Assert.AreEqual(Argb(7), image[0, 0]);                  // the ink
        Assert.AreEqual(Argb(6), image[1, 0]);                  // the background
        Assert.AreEqual(Argb(7), image[8 + 7, 0]);
        Assert.AreEqual(Argb(6), image[8, 0]);
    }

    [TestMethod]
    public void Charset_ShouldDrawMulticolorCharactersInPairsOfPixels()
    {
        var ram = new byte[65536];
        ram[0x2000] = 0b00_01_10_11;                            // background, multicolor 1, multicolor 2, the ink
        var image = MemoryRender.Charset(ram, 0x2000, count: 1, perRow: 1, multicolor: true, Colors);
        Assert.AreEqual(Argb(6), image[0, 0]);
        Assert.AreEqual(Argb(6), image[1, 0]);                  // each pair is two pixels wide
        Assert.AreEqual(Argb(14), image[2, 0]);
        Assert.AreEqual(Argb(1), image[4, 0]);
        Assert.AreEqual(Argb(7), image[6, 0]);
        Assert.AreEqual(Argb(7), image[7, 0]);
    }

    [TestMethod]
    public void Charset_ShouldWrapAroundTheEndOfMemory()
    {
        var ram = new byte[65536];
        ram[0xFFF8] = 0x80;
        ram[0x0000] = 0x01;
        var image = MemoryRender.Charset(ram, 0xFFF8, count: 2, perRow: 2, multicolor: false, Colors);
        Assert.AreEqual(Argb(7), image[0, 0]);
        Assert.AreEqual(Argb(7), image[15, 0]);                 // the second character is the bytes from $0000
    }

    [TestMethod]
    public void Sprites_ShouldDrawThreeBytesAreRowAndTheirOwnStride()
    {
        var ram = new byte[65536];
        ram[0x2400] = 0x80;                                     // the first sprite: its top left pixel
        ram[0x2400 + 62] = 0x01;                                // its bottom right pixel (byte 62: row 20, third byte)
        ram[0x2440] = 0x80;                                     // the next one, 64 bytes on
        var image = MemoryRender.Sprites(ram, 0x2400, count: 2, perRow: 2, multicolor: false, Colors);
        Assert.AreEqual(48, image.Width);
        Assert.AreEqual(21, image.Height);
        Assert.AreEqual(Argb(7), image[0, 0]);
        Assert.AreEqual(Argb(7), image[23, 20]);
        Assert.AreEqual(Argb(7), image[24, 0]);
        Assert.AreEqual(Argb(6), image[1, 0]);

        // A stride of 63: the sprites follow each other with no padding byte
        ram[0x2400 + 63] = 0x80;
        var tight = MemoryRender.Sprites(ram, 0x2400, count: 2, perRow: 2, multicolor: false, Colors, stride: 63);
        Assert.AreEqual(Argb(7), tight[24, 0]);
    }

    [TestMethod]
    public void Sprites_ShouldUseTheSpriteColorsOfTheMulticolorPairs()
    {
        var ram = new byte[65536];
        ram[0x2400] = 0b01_10_11_00;                            // multicolor 1, its own color, multicolor 2, transparent
        var image = MemoryRender.Sprites(ram, 0x2400, count: 1, perRow: 1, multicolor: true, Colors);
        Assert.AreEqual(Argb(14), image[0, 0]);
        Assert.AreEqual(Argb(7), image[2, 0]);
        Assert.AreEqual(Argb(1), image[4, 0]);
        Assert.AreEqual(Argb(6), image[6, 0]);
    }

    [TestMethod]
    public void Screen_ShouldDrawEachCodeWithItsCharacterAndColor()
    {
        var ram = new byte[65536];
        var colorRam = new byte[1024];
        ram[0x0400] = 1;                                        // the first cell: code 1, white
        ram[0x0401] = 2;                                        // the second: code 2, red
        colorRam[0] = 1;
        colorRam[1] = 2;
        // Character 1 is a solid top row, character 2 a solid bottom row
        byte Row(int code, int row) => code == 1 && row == 0 || code == 2 && row == 7 ? (byte)0xFF : (byte)0;
        var image = MemoryRender.Screen(ram, colorRam, 0x0400, columns: 2, rows: 1, Row, CellMode.Hires, Colors);
        Assert.AreEqual(16, image.Width);
        Assert.AreEqual(Argb(1), image[0, 0]);
        Assert.AreEqual(Argb(6), image[0, 1]);
        Assert.AreEqual(Argb(2), image[8, 7]);
        Assert.AreEqual(Argb(6), image[8, 0]);

        var fixedColor = MemoryRender.Screen(ram, colorRam, 0x0400, 2, 1, Row, CellMode.Hires, Colors, fixedColor: 5);
        Assert.AreEqual(Argb(5), fixedColor[0, 0]);
    }

    [TestMethod]
    public void Screen_ShouldMakeACellMulticolorWhenItsColorIsEightOrMore()
    {
        var ram = new byte[65536];
        var colorRam = new byte[1024];
        ram[0x0400] = 1;
        ram[0x0401] = 1;
        colorRam[0] = 0x0A;                                     // multicolor, color 2
        colorRam[1] = 0x02;                                     // hires, color 2
        byte Row(int code, int row) => 0b11_10_01_00;
        var image = MemoryRender.Screen(ram, colorRam, 0x0400, 2, 1, Row, CellMode.MulticolorByColor, Colors);
        Assert.AreEqual(Argb(2), image[0, 0]);                  // 11 in a multicolor cell: the color 0-7
        Assert.AreEqual(Argb(1), image[2, 0]);                  // 10: multicolor 2
        Assert.AreEqual(Argb(14), image[4, 0]);                 // 01: multicolor 1
        Assert.AreEqual(Argb(2), image[8, 0]);                  // hires cell: bit 7 is on (0b11...), the ink
        Assert.AreEqual(Argb(2), image[10, 0]);                 // bit 5 is on too
        Assert.AreEqual(Argb(6), image[11, 0]);                 // bit 4 is off: the background
    }

    [TestMethod]
    public void Bitmap_ShouldTakeTheColorsOfACellFromTheScreenAndColorRam()
    {
        var ram = new byte[65536];
        var colorRam = new byte[1024];
        ram[0x2000] = 0b10_01_11_00;                            // the first row of the first cell
        ram[0x0400] = 0x25;                                     // screen RAM: high nibble 2, low nibble 5
        colorRam[0] = 0x0D;
        var multicolor = MemoryRender.Bitmap(ram, colorRam, bitmapOffset: 0x2000, screenOffset: 0x0400, multicolor: true, background: 6);
        Assert.AreEqual(320, multicolor.Width);
        Assert.AreEqual(Argb(5), multicolor[0, 0]);             // 10: the low nibble
        Assert.AreEqual(Argb(2), multicolor[2, 0]);             // 01: the high nibble
        Assert.AreEqual(Argb(13), multicolor[4, 0]);            // 11: Color RAM
        Assert.AreEqual(Argb(6), multicolor[6, 0]);             // 00: the background

        ram[0x2000] = 0b1000_0000;
        var hires = MemoryRender.Bitmap(ram, colorRam, 0x2000, 0x0400, multicolor: false, background: 6);
        Assert.AreEqual(Argb(2), hires[0, 0]);                  // a set bit: the high nibble
        Assert.AreEqual(Argb(5), hires[1, 0]);                  // a clear one: the low nibble
    }

    [TestMethod]
    public void Board_ShouldHandOverItsMemory()
    {
        var board = NewBoard();
        board.RunCycles(3 * 985248);
        board.Poke(0xA000, 0x55);                               // goes to the RAM under BASIC
        board.Poke(0x0400, 0x01);
        var memory = new MemoryCapture(board.CaptureMemory());

        Assert.AreEqual(0x55, memory.Ram[0xA000], "the RAM under the ROM");
        Assert.AreNotEqual(0x55, board.Peek(0xA000), "the CPU reads the ROM there");
        Assert.AreEqual(0x01, memory.Ram[0x0400]);
        Assert.AreEqual(0x0400, memory.ScreenAddress);
        Assert.AreEqual(0x1000, memory.CharsetAddress);
        Assert.IsTrue(memory.CharsetIsRom);
        Assert.IsFalse(memory.BitmapMode);
        Assert.AreEqual(board.Peek(0xD021) & 15, memory.Background);
        Assert.AreEqual(board.Peek(0xD800) & 15, memory.ColorRam[0]);
        // The character ROM, and a character as the VIC-II reads it
        Assert.AreEqual(board.Chargen(18 * 8 + 3), memory.CharsetRom[18 * 8 + 3]);
        Assert.AreEqual(board.Chargen(18 * 8 + 3), memory.CharacterRow(18, 3));
        Assert.AreEqual(board.Peek(1), memory.CpuPort);
        Assert.AreEqual(0, memory.VicBank);
    }

    [TestMethod]
    public void Memory_ShouldRefuseTheWrongSize()
    {
        Assert.ThrowsExactly<FormatException>(() => new MemoryCapture(new byte[100]));
    }
}
