using ThingCommodore.Graphics;

namespace Thing64.Tests;

// ThingCommodore.Graphics: several pictures converted into screens that share one charset
[TestClass]
public sealed class ImageSetTests
{
    static readonly C64Palette Palette = C64Palette.Pepto;

    // A picture painted with C64 colors by a function of the pixel
    static RgbImage Paint(int width, int height, Func<int, int, int> color)
    {
        var image = new RgbImage(width, height);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++) image[x, y] = Palette.Argb(color(x, y));
        return image;
    }

    // 8x8 blocks of black and white in a chessboard, 40x25 characters in hires: two characters in all
    static RgbImage Chessboard(int shift = 0) => Paint(320, 200, (x, y) => ((x + shift) / 8 + y / 8) % 2 == 0 ? 0 : 1);

    // A different character in every cell: stripes whose width depends on the cell
    static RgbImage Distinct() => Paint(320, 200, (x, y) => x / 8 % 5 == 0 ? y / 8 % 2 : (x + y / 8 * 3) % (x / 8 % 7 + 2) < 1 ? 1 : 0);

    static ImageSetAsset Hires(int columns = 40, int rows = 25) =>
        new() { Multicolor = false, Columns = columns, Rows = rows, Dither = Dither.None, Background = 0 };

    static (string, RgbImage)[] Pictures(params (string Name, RgbImage Image)[] pictures) => pictures;

    [TestMethod]
    public void ConvertSet_ShouldShareTheCharsetBetweenPictures()
    {
        var set = Hires();

        var conversion = Converter.ConvertSet(Pictures(("a.png", Chessboard()), ("b.png", Chessboard())), set, "story");
        var single = Converter.ConvertSet(Pictures(("a.png", Chessboard())), set, "story");

        Assert.AreEqual(2, conversion.Screens.Count);
        Assert.AreEqual(single.Characters.Count, conversion.Characters.Count, "the same pictures add no new characters");
        CollectionAssert.AreEqual(conversion.Screens[0].Codes, conversion.Screens[1].Codes);
        Assert.AreEqual(40 * 25, conversion.Screens[0].Codes.Length);
    }

    [TestMethod]
    public void ConvertSet_ShouldAddOnlyTheCharactersThatAreNew()
    {
        var set = Hires();
        var first = Converter.ConvertSet(Pictures(("a.png", Chessboard())), set, "story");

        // A second picture of the same kind in another phase: still the same two characters; one with a new pattern adds some
        var shifted = Converter.ConvertSet(Pictures(("a.png", Chessboard()), ("b.png", Chessboard(3))), set, "story");

        Assert.IsTrue(shifted.Characters.Count > first.Characters.Count, "a shifted board has characters the first one does not");
        Assert.IsTrue(shifted.Characters.Count < 2 * first.Characters.Count + 64);
        CollectionAssert.AreEqual(first.Screens[0].Codes, shifted.Screens[0].Codes, "the first picture does not change");
    }

    [TestMethod]
    public void ConvertSet_ShouldRespectTheCharacterLimit()
    {
        var set = Hires() with { MaxCharacters = 5 };

        var conversion = Converter.ConvertSet(Pictures(("a.png", Distinct()), ("b.png", Chessboard(2))), set, "story");

        Assert.IsTrue(conversion.Characters.Count <= 5, $"{conversion.Characters.Count} characters");
        Assert.IsTrue(conversion.Screens.All(s => s.Codes.All(c => c < conversion.Characters.Count)));
    }

    [TestMethod]
    public void ConvertSet_ShouldKeepTheCharactersThatWereThere()
    {
        var set = Hires();
        byte[] mine = [1, 2, 3, 4, 5, 6, 7, 8];
        byte[] solid = [0, 0, 0, 0, 0, 0, 0, 0];

        // The chessboard needs a blank character and a solid one: the blank one is already the second of the charset
        var conversion = Converter.ConvertSet(Pictures(("a.png", Chessboard())), set, "story", [(mine, "mine"), (solid, "blank")]);

        CollectionAssert.AreEqual(mine, conversion.Characters[0], "an existing character stays where it was");
        Assert.AreEqual("mine", conversion.Descriptions[0]);
        Assert.AreEqual(2, conversion.Frozen);
        Assert.IsTrue(conversion.Screens[0].Codes.Contains(1), "the blank character was found and used");
        Assert.AreEqual(3, conversion.Characters.Count, "only the solid character is new");
    }

    [TestMethod]
    public void ConvertSet_ShouldRefuseAnExistingCharsetAboveTheLimit()
    {
        var set = Hires() with { MaxCharacters = 2 };
        var existing = Enumerable.Range(0, 3).Select(i => (new byte[] { (byte)i, 0, 0, 0, 0, 0, 0, 0 }, "")).ToList();

        Assert.ThrowsExactly<InvalidDataException>(() => Converter.ConvertSet(Pictures(("a.png", Distinct())), set, "story", existing));
    }

    [TestMethod]
    public void ConvertSet_ShouldMakeScreensOfTheSizeAsked()
    {
        var conversion = Converter.ConvertSet(Pictures(("a.png", Chessboard())), Hires(columns: 20, rows: 10), "story");

        Assert.AreEqual(20, conversion.Columns);
        Assert.AreEqual(10, conversion.Rows);
        Assert.AreEqual(200, conversion.Screens[0].Codes.Length);
        Assert.AreEqual(200, conversion.Screens[0].Colors.Length);
        Assert.AreEqual(160, conversion.Screens[0].Preview.Width);
        Assert.AreEqual(80, conversion.Screens[0].Preview.Height);
    }

    [TestMethod]
    public void ConvertSet_ShouldChooseTheSharedColorsFromAllThePictures()
    {
        var red = Paint(320, 200, (x, y) => 2);
        var blue = Paint(320, 200, (x, y) => 6);
        var set = Hires() with { Background = null };

        var conversion = Converter.ConvertSet(Pictures(("a.png", red), ("b.png", red), ("c.png", blue)), set, "story");

        Assert.AreEqual(2, conversion.Background, "red is what most pictures use");
    }

    [TestMethod]
    public void ConvertSet_ShouldRefuseAnEmptySet() =>
        Assert.ThrowsExactly<InvalidDataException>(() => Converter.ConvertSet([], Hires(), "story"));

    [TestMethod]
    public void Files_ShouldBeRegionsTheEditorsKnow()
    {
        var conversion = Converter.ConvertSet(Pictures(("title screen.png", Chessboard()), ("castle.png", Chessboard())),
            Hires(columns: 8, rows: 2) with { First = 0x40 }, "my story");

        string charset = conversion.CharsetText();
        string screen = conversion.ScreenText(conversion.Screens[0]);

        StringAssert.Contains(charset, ";@ charset first=my_story__FIRST");
        StringAssert.Contains(charset, "my_story__characters:");
        StringAssert.Contains(charset, "my_story__FIRST       = $40");
        StringAssert.Contains(charset, "from title screen.png");
        StringAssert.Contains(screen, ";@ screen width=8 height=2 screen=title_screen__screen colors=title_screen__colors");
        StringAssert.Contains(screen, "charset=my_story__characters first=my_story__FIRST");
        Assert.AreEqual("title_screen.asm", conversion.ScreenFileName(conversion.Screens[0]));
        Assert.AreEqual("my_story.asm", conversion.CharsetFileName);
        // 2 rows of 8 codes for the screen and 2 for the colors, from the first code
        Assert.AreEqual(4, screen.Split('\n').Count(l => l.TrimStart().StartsWith(".byte")));
        Assert.IsTrue(screen.Contains("$40") || screen.Contains("$41"), "the codes start from the first character's code");
        Assert.AreEqual(2, conversion.Screens.Select(s => s.Name).Distinct().Count());
    }

    [TestMethod]
    public void Files_ShouldNameTwoPicturesWithTheSameFileNameApart()
    {
        var conversion = Converter.ConvertSet(Pictures(("a/pic.png", Chessboard()), ("b/pic.png", Chessboard())), Hires(8, 2), "story");

        CollectionAssert.AreEqual(new[] { "pic", "pic_2" }, conversion.Screens.Select(s => s.Name).ToArray());
    }

    [TestMethod]
    public void Multicolor_ShouldShareTheMulticolorsAndSetBitThreeOfTheColors()
    {
        var picture = Paint(320, 200, (x, y) => x / 4 % 3 == 0 ? 2 : x / 4 % 3 == 1 ? 5 : 1);
        var set = new ImageSetAsset { Multicolor = true, Dither = Dither.None };

        var conversion = Converter.ConvertSet(Pictures(("a.png", picture), ("b.png", picture)), set, "story");

        Assert.IsTrue(conversion.Multicolor);
        Assert.IsTrue(conversion.Screens.All(s => s.Colors.All(c => (c & 8) != 0)), "every cell is multicolor: bit 3 set");
        StringAssert.Contains(conversion.CharsetText(), "multicolor=1");
        StringAssert.Contains(conversion.ScreenText(conversion.Screens[0]), "multicolor1=story__MULTICOLOR_1");
    }

    [TestMethod]
    public void Downsampling_ShouldFollowTheParametersChosen()
    {
        var picture = Paint(320, 200, (x, y) => (x + y) % 2 == 0 ? 0 : 1);   // a fine checker: smaller than a pixel of a 20x10 screen
        var box = Hires(20, 10);

        var smooth = Converter.ConvertSet(Pictures(("a.png", picture)), box, "s");
        var nearest = Converter.ConvertSet(Pictures(("a.png", picture)), box with { Resample = Resample.Nearest }, "s");

        // A box averages the checker to grey; nearest keeps a hard black or white pixel
        CollectionAssert.AreNotEqual(smooth.Screens[0].Preview.Pixels, nearest.Screens[0].Preview.Pixels);
    }

    [TestMethod]
    public void Tones_ShouldChangeTheColorsChosen()
    {
        var grey = Paint(320, 200, (x, y) => 11);    // dark grey
        var normal = Converter.Convert(grey, new GraphicsAsset { Mode = GraphicsMode.HiresBitmap, Dither = Dither.None });
        var brighter = Converter.Convert(grey, new GraphicsAsset { Mode = GraphicsMode.HiresBitmap, Dither = Dither.None, Brightness = 60 });
        var darker = Converter.Convert(grey, new GraphicsAsset { Mode = GraphicsMode.HiresBitmap, Dither = Dither.None, Brightness = -60 });

        int Average(Conversion c) => (int)c.Preview.Pixels.Average(p => (p >> 16 & 0xFF) + (p >> 8 & 0xFF) + (p & 0xFF));
        Assert.IsTrue(Average(brighter) > Average(normal));
        Assert.IsTrue(Average(darker) < Average(normal));
    }

    [TestMethod]
    public void Asset_ShouldReadAndWriteItsJson()
    {
        var set = new ImageSetAsset { Name = "story", Sources = ["a.png", "b.png"], First = 16, Columns = 30, Resample = Resample.Bilinear, Contrast = 20 };

        var again = ImageSetAsset.Parse(set.ToJson());

        CollectionAssert.AreEqual(set.Sources, again.Sources);
        Assert.AreEqual(16, again.First);
        Assert.AreEqual(Resample.Bilinear, again.Resample);
        Assert.AreEqual(20, again.Contrast);
        Assert.AreEqual(240, again.CharacterLimit);
        Assert.AreEqual("story", ImageSetAsset.NameOf("folder/story.imgset.json"));
        StringAssert.Contains(set.ToJson(), "\"resample\": \"bilinear\"");
    }
}
