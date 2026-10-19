using ThingMOS.Asm;
using ThingCommodore.Graphics;

namespace Thing64.Tests;

// ThingCommodore.Graphics: PNG, the conversion of images to the C64's modes with their color limits, and the files a
// graphics asset produces
[TestClass]
public sealed class GraphicsTests
{
    static readonly C64Palette Palette = C64Palette.Pepto;

    // An image painted with C64 colors by a function of the pixel
    static RgbImage Paint(int width, int height, Func<int, int, int> color)
    {
        var image = new RgbImage(width, height);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int c = color(x, y);
                image[x, y] = c < 0 ? 0u : Palette.Argb(c);
            }
        return image;
    }

    [TestMethod]
    public void Png_ShouldReadBackWhatItWrites()
    {
        var image = new RgbImage(5, 3);
        for (int i = 0; i < image.Pixels.Length; i++) image.Pixels[i] = RgbImage.Argb(i * 15, i * 7, 255 - i * 9, i * 3);

        var read = Png.Read(Png.Write(image));

        Assert.AreEqual(5, read.Width);
        Assert.AreEqual(3, read.Height);
        CollectionAssert.AreEqual(image.Pixels, read.Pixels);
    }

    [TestMethod]
    public void Png_ShouldRefuseOtherFiles() =>
        Assert.ThrowsExactly<InvalidDataException>(() => Png.Read([1, 2, 3, 4, 5, 6, 7, 8, 9]));

    [TestMethod]
    public void HiresBitmap_ShouldKeepTwoColorsPerCell()
    {
        // Vertical stripes of red and cyan, 1 pixel wide: every cell has exactly those two colors
        var image = Paint(320, 200, (x, y) => x % 2 == 0 ? 2 : 3);

        var conversion = Converter.Convert(image, new GraphicsAsset { Mode = GraphicsMode.HiresBitmap, Dither = Dither.None });

        var bitmap = Part(conversion, "bitmap");
        var screen = Part(conversion, "screen");
        Assert.AreEqual(8000, bitmap.Length);
        Assert.AreEqual(1000, screen.Length);
        // The bits 1 are the high nybble of the screen: the pattern is 10101010 or 01010101 depending on the order
        int foreground = screen[0] >> 4, background = screen[0] & 15;
        CollectionAssert.AreEquivalent(new[] { 2, 3 }, new[] { foreground, background });
        Assert.AreEqual(foreground == 2 ? 0xAA : 0x55, bitmap[0]);
        CollectionAssert.AreEqual(image.Pixels, conversion.Preview.Pixels);
    }

    [TestMethod]
    public void MulticolorBitmap_ShouldReproduceFourColorsInACell()
    {
        // Background black everywhere, and in each 4x8 cell three more colors in double-wide pixels
        var image = Paint(320, 200, (x, y) => (x / 2 % 4) switch { 0 => 0, 1 => 5, 2 => 7, _ => 14 });

        var conversion = Converter.Convert(image, new GraphicsAsset { Mode = GraphicsMode.MulticolorBitmap, Dither = Dither.None, Background = 0 });

        Assert.AreEqual(0, conversion.Constants["BACKGROUND"]);
        CollectionAssert.AreEqual(image.Pixels, conversion.Preview.Pixels);
        var screen = Part(conversion, "screen");
        var colors = Part(conversion, "colors");
        int[] used = [screen[0] >> 4, screen[0] & 15, colors[0]];
        CollectionAssert.AreEquivalent(new[] { 5, 7, 14 }, used);
    }

    [TestMethod]
    public void HiresCharset_ShouldKeepEachCharacterOnce()
    {
        // A checkerboard of 8x8 squares: two characters (empty and full) for the whole screen
        var image = Paint(320, 200, (x, y) => (x / 8 + y / 8) % 2 == 0 ? 6 : 1);

        var conversion = Converter.Convert(image, new GraphicsAsset { Mode = GraphicsMode.HiresCharset, Dither = Dither.None, Background = 6 });

        Assert.AreEqual(2, conversion.Constants["CHARACTERS"]);
        Assert.AreEqual(16, Part(conversion, "charset").Length);
        Assert.AreEqual(1000, Part(conversion, "screen").Length);
        CollectionAssert.AreEqual(image.Pixels, conversion.Preview.Pixels);
    }

    [TestMethod]
    public void Charset_ShouldMergeSimilarCharactersDownToTheLimit()
    {
        // Every cell a different dot position: 64 characters, at most 16 allowed
        var image = Paint(64, 64, (x, y) => (x % 8 == (x / 8 + y / 8 * 8) % 8 && y % 8 == (x / 8 + y / 8 * 8) / 8) ? 1 : 0);

        var conversion = Converter.Convert(image, new GraphicsAsset
        {
            Mode = GraphicsMode.HiresCharset, Dither = Dither.None, Background = 0, Columns = 8, Rows = 8, Fit = Fit.Stretch, MaxCharacters = 16,
        });

        Assert.AreEqual(16, conversion.Constants["CHARACTERS"]);
        Assert.AreEqual(16 * 8, Part(conversion, "charset").Length);
        Assert.IsTrue(Part(conversion, "screen").All(code => code < 16));
    }

    [TestMethod]
    public void Tiles_ShouldMapTheScreenInBlocks()
    {
        var image = Paint(320, 192, (x, y) => (x / 16 + y / 16) % 2 == 0 ? 6 : 1);

        var conversion = Converter.Convert(image, new GraphicsAsset
        {
            Mode = GraphicsMode.HiresCharset, Dither = Dither.None, Background = 6, Columns = 40, Rows = 24, Fit = Fit.Stretch, Tile = new TileSize(2, 2),
        });

        Assert.AreEqual(2, conversion.Constants["TILES"]);
        Assert.AreEqual(20 * 12, Part(conversion, "map").Length);
        Assert.AreEqual(2 * 4, Part(conversion, "tiles").Length);
    }

    [TestMethod]
    public void HiresSprite_ShouldLeaveTheTransparentPixelsAtZero()
    {
        // A 24x21 sprite: a white frame, transparent inside
        var image = Paint(24, 21, (x, y) => x == 0 || y == 0 || x == 23 || y == 20 ? 1 : -1);

        var conversion = Converter.Convert(image, new GraphicsAsset { Mode = GraphicsMode.HiresSprites, Dither = Dither.None });

        var sprite = Part(conversion, "sprites");
        Assert.AreEqual(64, sprite.Length);
        CollectionAssert.AreEqual(new byte[] { 0xFF, 0xFF, 0xFF }, sprite[0..3]);
        CollectionAssert.AreEqual(new byte[] { 0x80, 0x00, 0x01 }, sprite[3..6]);
        Assert.AreEqual(1, Part(conversion, "spritecolors")[0]);
    }

    [TestMethod]
    public void Dithering_ShouldMixTwoColorsForAColorInBetween()
    {
        // A grey halfway between black and white, on a hires bitmap: ordered dithering makes a pattern of both
        var image = new RgbImage(320, 200);
        Array.Fill(image.Pixels, RgbImage.Argb(255, 128, 128, 128));

        var flat = Converter.Convert(image, new GraphicsAsset { Mode = GraphicsMode.HiresBitmap, Dither = Dither.None });
        var dithered = Converter.Convert(image, new GraphicsAsset { Mode = GraphicsMode.HiresBitmap, Dither = Dither.Ordered });

        Assert.AreEqual(1, flat.Preview.Pixels.Distinct().Count());
        Assert.IsTrue(dithered.Preview.Pixels.Distinct().Count() >= 2);
    }

    [TestMethod]
    public void Asset_ShouldWriteDataAndAnIncludeThatAssembles()
    {
        string folder = Path.Combine(Path.GetTempPath(), "thing64-gfx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllBytes(Path.Combine(folder, "logo.png"), Png.Write(Paint(160, 100, (x, y) => (x + y) % 16)));
            File.WriteAllText(Path.Combine(folder, "logo.gfx.json"), new GraphicsAsset
            {
                Source = "logo.png", Mode = GraphicsMode.MulticolorBitmap, Background = 0,
                Addresses = new() { ["bitmap"] = "$2000" },
            }.ToJson());

            var result = GraphicsBuilder.Build(Path.Combine(folder, "logo.gfx.json"));

            CollectionAssert.AreEquivalent(new[] { "logo.bitmap.bin", "logo.screen.bin", "logo.colors.bin", "logo.inc.asm" }, result.Files.ToArray());
            var program = Assembler.Assemble("""
                * = $1000
                        LDA #LOGO_BACKGROUND
                        STA $D021
                        RTS
                !source "logo.inc.asm"
                """, "main.asm", folder);
            Assert.AreEqual(0x2000, program.Symbols["logo_bitmap"]);
            Assert.AreEqual(0x2000 + 8000, program.Symbols["logo_bitmap_end"]);
            Assert.AreEqual(0x2000 + 10000, program.Symbols["logo_colors_end"]);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public void Asset_ShouldWriteTheDataAsTextWhenAsked()
    {
        string folder = Path.Combine(Path.GetTempPath(), "thing64-gfx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllBytes(Path.Combine(folder, "logo.png"), Png.Write(Paint(160, 100, (x, y) => (x + y) % 16)));
            File.WriteAllText(Path.Combine(folder, "logo.gfx.json"), new GraphicsAsset
            {
                Source = "logo.png", Mode = GraphicsMode.MulticolorCharset, Background = 0, Columns = 20, Rows = 10, Text = true,
                Addresses = new() { ["charset"] = "$2000" },
            }.ToJson());

            var result = GraphicsBuilder.Build(Path.Combine(folder, "logo.gfx.json"));

            CollectionAssert.AreEqual(new[] { "logo.inc.asm" }, result.Files.ToArray(), "no .bin files");
            Assert.IsFalse(Directory.EnumerateFiles(folder, "*.bin").Any());
            string include = File.ReadAllText(Path.Combine(folder, "logo.inc.asm"));
            StringAssert.Contains(include, ".byte $");
            Assert.IsFalse(include.Contains("!binary"));
            // The same labels as with binary files, and the same bytes
            var program = Assembler.Assemble("""
                * = $1000
                        RTS
                !source "logo.inc.asm"
                """, "main.asm", folder);
            Assert.AreEqual(0x2000, program.Symbols["logo_charset"]);
            int length = Part(result.Conversion, "charset").Length;
            Assert.AreEqual(0x2000 + length, program.Symbols["logo_charset_end"]);
            CollectionAssert.AreEqual(Part(result.Conversion, "charset"), program.Bytes[(0x2000 - program.Origin)..(0x2000 - program.Origin + length)]);
            // A row on each line of the screen and of the colors: 10 rows of 20 values each
            Assert.AreEqual(20, include.Split((char)10).Count(l => l.Split(',').Length == 20));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public void Asset_ShouldKeepTextOutOfItsJsonUntilItIsSet()
    {
        StringAssert.DoesNotMatch(new GraphicsAsset { Source = "a.png" }.ToJson(), new System.Text.RegularExpressions.Regex("\"text\""));
        StringAssert.Contains(new GraphicsAsset { Source = "a.png", Text = true }.ToJson(), "\"text\": true");
    }

    static byte[] Part(Conversion conversion, string name) => conversion.Parts.Single(p => p.Name == name).Data;
}
