using Thing64.Asm;
using Thing64.Graphics;
using static Thing64.Tests.TestHelpers;

namespace Thing64.Tests;

// Thing64.Graphics: a text screen taken from the C64's video memory, turned into the files of a Thing64 project, with
// the characters shared between successive captures
[TestClass]
public sealed class CaptureTests
{
    static readonly byte[] Blank = new byte[8];
    static readonly byte[] Wall = [0xAA, 0x55, 0xAA, 0x55, 0xAA, 0x55, 0xAA, 0x55];
    static readonly byte[] Brick = [0xFF, 0x81, 0xBD, 0xA5, 0xA5, 0xBD, 0x81, 0xFF];
    static readonly byte[] Solid = [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];

    // Video memory as Board.CaptureVideo() hands it over: screen at $0400, characters at $1000 (the usual $D018 = $14)
    static VideoCapture Capture(Func<int, (int Code, int Color)> cells, Dictionary<int, byte[]> characters,
        bool multicolor = true, int d011 = 0x1B)
    {
        var data = new byte[VideoCapture.Size];
        foreach (var (code, bytes) in characters) bytes.CopyTo(data, 0x1000 + code * 8);
        for (int cell = 0; cell < VideoCapture.Cells; cell++)
        {
            var (code, color) = cells(cell);
            data[0x0400 + cell] = (byte)code;
            data[VideoCapture.BankSize + cell] = (byte)color;
        }
        void Register(int register, int value) => data[VideoCapture.BankSize + VideoCapture.ColorSize + register] = (byte)value;
        Register(0x11, d011);
        Register(0x16, multicolor ? 0x18 : 0x08);
        Register(0x18, 0x14);
        Register(0x20, 0);
        Register(0x21, 5);
        Register(0x22, 0x0E);
        Register(0x23, 1);
        return new VideoCapture(data);
    }

    static VideoCapture Room() => Capture(
        cell => cell < 40 ? (0x41, 0x0A) : cell < 80 ? (0x42, 0x09) : (0x20, 5),
        new() { [0x41] = Wall, [0x42] = Brick, [0x20] = Blank });

    [TestMethod]
    public void Capture_ShouldKnowWhereTheScreenIs()
    {
        var capture = Room();
        Assert.AreEqual(0x0400, capture.ScreenAddress);
        Assert.AreEqual(0x1000, capture.CharsetAddress);
        Assert.IsTrue(capture.MulticolorMode);
        Assert.AreEqual(5, capture.Background);
        Assert.AreEqual(0x0E, capture.Multicolor1);
        Assert.AreEqual(1, capture.Multicolor2);
        Assert.AreEqual(0x41, capture.Code(0));
        Assert.AreEqual(0x0A, capture.Color(0));
        Assert.IsTrue(capture.IsMulticolorCell(0));
        Assert.IsFalse(capture.IsMulticolorCell(80));
        CollectionAssert.AreEqual(Brick, capture.Character(0x42));
    }

    [TestMethod]
    public void Capture_ShouldRenderTheScreenAsTheC64ShowsIt()
    {
        var image = Room().Render(C64Palette.Pepto);
        // Cell 0: Wall in multicolor, color $0A: its first row is $AA (the pairs 10: multicolor 2), the second $55 (01: multicolor 1)
        Assert.AreEqual(C64Palette.Pepto.Argb(1), image[0, 0]);
        Assert.AreEqual(C64Palette.Pepto.Argb(0x0E), image[0, 1]);
        // A blank cell is the background
        Assert.AreEqual(C64Palette.Pepto.Argb(5), image[0, 8 * 3]);
    }

    [TestMethod]
    public void Board_ShouldHandOverWhatTheVicShows()
    {
        var board = NewBoard();
        board.RunCycles(3 * 985248);                            // the KERNAL's READY. screen
        var capture = new VideoCapture(board.CaptureVideo());
        Assert.AreEqual(0x0400, capture.ScreenAddress);
        Assert.AreEqual(0x1000, capture.CharsetAddress);
        Assert.IsFalse(capture.MulticolorMode);
        Assert.AreEqual(board.Peek(0xD021) & 15, capture.Background);
        // "READY." on the sixth row: R is screen code 18, light blue, and its bytes are the ROM's, where the VIC reads them
        Assert.AreEqual(18, capture.Code(5 * 40));
        Assert.AreEqual(board.Peek(0xD800 + 5 * 40) & 15, capture.Color(5 * 40));
        CollectionAssert.AreEqual(Enumerable.Range(0, 8).Select(i => board.Chargen((ushort)(18 * 8 + i))).ToArray(), capture.Character(18));
    }

    [TestMethod]
    public void Capture_ShouldRefuseBitmapAndExtendedColorScreens()
    {
        StringAssert.Contains(Assert.ThrowsExactly<NotSupportedException>(() => Capture(_ => (0, 0), [], d011: 0x3B)).Message, "bitmap");
        StringAssert.Contains(Assert.ThrowsExactly<NotSupportedException>(() => Capture(_ => (0, 0), [], d011: 0x5B)).Message, "extended");
    }

    [TestMethod]
    public void Charset_ShouldGiveEachDistinctCharacterOneCode()
    {
        var charset = new CapturedCharset("charset1");
        var first = charset.Add("room1", Room());
        // Wall, Brick and Blank, in the order the screen shows them
        Assert.AreEqual(3, charset.Characters.Count);
        CollectionAssert.AreEqual(Wall, charset.Characters[0]);
        CollectionAssert.AreEqual(Brick, charset.Characters[1]);
        CollectionAssert.AreEqual(Blank, charset.Characters[2]);
        Assert.AreEqual(0, first.Codes[0]);
        Assert.AreEqual(1, first.Codes[40]);
        Assert.AreEqual(2, first.Codes[80]);

        // A second screen: Wall under another screen code, a new character; what is known keeps its code
        var second = charset.Add("room2", Capture(
            cell => cell < 40 ? (0x61, 0x0A) : cell < 80 ? (0x50, 0x0A) : (0x20, 5),
            new() { [0x61] = Wall, [0x50] = Solid, [0x20] = Blank }));
        Assert.AreEqual(4, charset.Characters.Count);
        Assert.AreEqual(0, second.Codes[0]);
        Assert.AreEqual(3, second.Codes[40]);
        Assert.AreEqual(2, second.Codes[80]);
    }

    [TestMethod]
    public void Charset_ShouldStartAtTheFirstCode()
    {
        var charset = new CapturedCharset("charset1", first: 0x70);
        var screen = charset.Add("room1", Room());
        Assert.AreEqual(0x70, screen.Codes[0]);
        Assert.AreEqual(0x71, screen.Codes[40]);
        StringAssert.Contains(charset.CharsetText(), "charset1__FIRST = $70");
        StringAssert.Contains(charset.CharsetText(), ";@ charset first=charset1__FIRST");
        StringAssert.Contains(charset.ScreenText(screen), "first=charset1__FIRST");
    }

    [TestMethod]
    public void Charset_ShouldRefuseMoreCharactersThanItHolds()
    {
        var charset = new CapturedCharset("charset1", first: 0xFE);
        StringAssert.Contains(Assert.ThrowsExactly<InvalidOperationException>(() => charset.Add("room1", Room())).Message, "only 2 fit");
        Assert.AreEqual(0, charset.Characters.Count);          // nothing was added
    }

    [TestMethod]
    public void Charset_ShouldBeMulticolorWhenEveryCharacterIs()
    {
        var charset = new CapturedCharset("charset1");
        charset.Add("room1", Capture(c => c < 40 ? (0x41, 0x0A) : (0x20, 5), new() { [0x41] = Wall, [0x20] = Blank }));
        Assert.IsTrue(charset.Multicolor, "the blank character in hires cells does not count");
        StringAssert.Contains(charset.CharsetText(), "multicolor=1");

        // A solid character in a hires cell looks the same both ways
        charset.Add("room2", Capture(c => c < 40 ? (0x41, 0x0A) : (0x43, 7), new() { [0x41] = Wall, [0x43] = Solid }));
        Assert.IsTrue(charset.Multicolor);

        // A wall in a hires cell does not
        charset.Add("room3", Capture(c => (0x41, 7), new() { [0x41] = Wall }));
        Assert.IsFalse(charset.Multicolor);
        Assert.IsFalse(charset.CharsetText().Contains("multicolor=1"));
    }

    [TestMethod]
    public void Charset_ShouldNotBeMulticolorForAHiresScreen()
    {
        var charset = new CapturedCharset("charset1");
        charset.Add("room1", Capture(c => (0x41, 7), new() { [0x41] = Wall }, multicolor: false));
        Assert.IsFalse(charset.Multicolor);
    }

    [TestMethod]
    public void Charset_ShouldStartFromOneTheProjectHas()
    {
        var charset = new CapturedCharset("charset1", first: 0x70);
        charset.Seed([(Brick, "wall"), (Solid, "block")], multicolor: true);
        var screen = charset.Add("room1", Room());              // Wall is new, Brick is known, Blank is new
        Assert.AreEqual(4, charset.Characters.Count);
        Assert.AreEqual(0x72, screen.Codes[0]);                  // Wall: after the two it had
        Assert.AreEqual(0x70, screen.Codes[40]);                 // Brick keeps its code
        Assert.AreEqual("wall", charset.Descriptions[0]);
        Assert.IsTrue(charset.Multicolor);
        Assert.AreEqual(1, charset.Screens.Count);
    }

    [TestMethod]
    public void Charset_ShouldKeepTheCodesOfTheCharsetTheProgramWasBuiltFrom()
    {
        // The program's charset has the blank character twice (codes 0 and 2): a screen that uses the second one
        // keeps it, instead of falling on the first
        var charset = new CapturedCharset("charset1");
        charset.Seed([(Blank, "blank"), (Wall, "wall"), (Blank, "another blank"), (Brick, "brick")], multicolor: false);
        var screen = charset.Add("room1", Capture(
            cell => cell < 40 ? (2, 5) : cell < 80 ? (3, 5) : (1, 5),
            new() { [0] = Blank, [1] = Wall, [2] = Blank, [3] = Brick }));
        Assert.AreEqual(4, charset.Characters.Count);            // nothing new
        Assert.AreEqual(2, screen.Codes[0]);
        Assert.AreEqual(3, screen.Codes[40]);
        Assert.AreEqual(1, screen.Codes[80]);
        // A character the charset has at another code is found by its bytes
        var other = charset.Add("room2", Capture(c => (9, 5), new() { [9] = Brick }));
        Assert.AreEqual(3, other.Codes[0]);
    }

    [TestMethod]
    public void Charset_ShouldFillTheEmptyPlacesAtTheEndOfOneItStartsFrom()
    {
        // A full charset: 256 characters, the last ones empty (as the project's charset1 is)
        var existing = new List<(byte[], string)> { (Blank, "space"), (Brick, "wall") };
        for (int i = 2; i < 256; i++) existing.Add((Blank, ""));
        var charset = new CapturedCharset("charset1");
        charset.Seed(existing, multicolor: true);
        Assert.AreEqual(256, charset.Characters.Count);

        // Wall and Solid are new: they take the first two empty places, and the size stays 256
        var screen = charset.Add("room1", Capture(
            cell => cell < 40 ? (0x41, 0x0A) : cell < 80 ? (0x42, 0x0A) : (0x43, 5),
            new() { [0x41] = Wall, [0x42] = Solid, [0x43] = Blank }));
        Assert.AreEqual(256, charset.Characters.Count);
        Assert.AreEqual(2, screen.Codes[0]);
        Assert.AreEqual(3, screen.Codes[40]);
        Assert.AreEqual(0, screen.Codes[80], "a blank cell uses the blank character the charset has, not an empty place");
        CollectionAssert.AreEqual(Wall, charset.Characters[2]);
        CollectionAssert.AreEqual(Solid, charset.Characters[3]);
        StringAssert.Contains(charset.Descriptions[2], "room1");
        Assert.AreEqual("", charset.Descriptions[4]);            // the rest is still empty

        // Nothing is left to ask for once all 256 are taken
        var full = new CapturedCharset("charset2");
        full.Seed(Enumerable.Range(0, 256).Select(i => (new byte[] { (byte)(i + 1), 0, 0, 0, 0, 0, 0, 0 }, "")), multicolor: false);
        StringAssert.Contains(Assert.ThrowsExactly<InvalidOperationException>(() => full.Add("room1", Room())).Message, "only 256 fit");
    }

    [TestMethod]
    public void Files_ShouldAssembleToTheCapturedData()
    {
        var charset = new CapturedCharset("charset1", first: 0x10);
        var room = charset.Add("room1", Room());
        string source = "* = $2000\n" + charset.CharsetText() + "\n" + charset.ScreenText(room);
        var program = Assembler.Assemble(source, "capture.asm");
        int At(string label) => program.Symbols[label] - program.Origin;

        Assert.AreEqual(3 * 8, program.Symbols["charset1__charactersEnd"] - program.Symbols["charset1__characters"]);
        CollectionAssert.AreEqual(Wall, program.Bytes[At("charset1__characters")..(At("charset1__characters") + 8)]);
        Assert.AreEqual(0x10, program.Bytes[At("room1__screen")]);
        Assert.AreEqual(0x11, program.Bytes[At("room1__screen") + 40]);
        Assert.AreEqual(0x12, program.Bytes[At("room1__screen") + 999]);
        Assert.AreEqual(1000, program.Symbols["room1__colors"] - program.Symbols["room1__screen"]);
        Assert.AreEqual(0x0A, program.Bytes[At("room1__colors")]);
        Assert.AreEqual(5, program.Bytes[At("room1__colors") + 999]);
        // The values are constants with a name, not numbers in the parameters
        Assert.AreEqual(0x10, program.Symbols["charset1__FIRST"]);
        Assert.AreEqual(5, program.Symbols["room1__BACKGROUND"]);
        Assert.AreEqual(0, program.Symbols["room1__BORDER"]);
        Assert.AreEqual(0x0E, program.Symbols["room1__MULTICOLOR_1"]);
        Assert.AreEqual(1, program.Symbols["room1__MULTICOLOR_2"]);
    }

    [TestMethod]
    public void Files_ShouldCarryTheParametersTheEditorsRead()
    {
        var charset = new CapturedCharset("charset1");
        var room = charset.Add("room1", Room());
        string text = charset.ScreenText(room);
        StringAssert.Contains(text, ";@ screen width=40 height=25 screen=room1__screen colors=room1__colors");
        StringAssert.Contains(text, "charset=charset1__characters first=charset1__FIRST");
        StringAssert.Contains(text, "background=room1__BACKGROUND multicolor=1 multicolor1=room1__MULTICOLOR_1 multicolor2=room1__MULTICOLOR_2");
        StringAssert.Contains(text, "room1__MULTICOLOR_1 = $0E");
        StringAssert.Contains(text, "room1__screen:");
        StringAssert.Contains(text, "room1__colors:");
        StringAssert.Contains(text, "; row 24");
        StringAssert.Contains(charset.CharsetText(), "charset1__charactersEnd:");
        Assert.IsTrue(charset.CharsetText().TrimEnd().EndsWith(";@ end"));

        var hires = new CapturedCharset("charset2");
        var plain = hires.Add("room2", Capture(c => (0x41, 7), new() { [0x41] = Wall }, multicolor: false));
        Assert.IsFalse(hires.ScreenText(plain).Contains("multicolor=1"));
        Assert.IsFalse(hires.ScreenText(plain).Contains("MULTICOLOR_1"), "no multicolors to name on a hires screen");
    }

    [TestMethod]
    public void Charset_ShouldForgetWhatARemovedScreenDid()
    {
        var charset = new CapturedCharset("charset1");
        charset.Add("room1", Capture(c => (0x41, 0x0A), new() { [0x41] = Wall }));
        charset.Add("room2", Capture(c => (0x41, 7), new() { [0x41] = Wall }));
        Assert.IsFalse(charset.Multicolor);                      // room2 uses the wall in hires cells
        charset.Remove("room2");
        Assert.IsTrue(charset.Multicolor);
        Assert.AreEqual(1, charset.Characters.Count);            // the characters stay
        charset.Rename("room1", "start");
        Assert.AreEqual("start", charset.Screens[0].Name);
        Assert.IsTrue(charset.Multicolor);
        Assert.ThrowsExactly<ArgumentException>(() => charset.Rename("nothing", "x"));
    }

    [TestMethod]
    public void Names_ShouldBecomeLabels()
    {
        Assert.AreEqual("my_room_1", CapturedCharset.Identifier(" my room-1 "));
        Assert.AreEqual("_1st", CapturedCharset.Identifier("1st"));
        Assert.ThrowsExactly<ArgumentException>(() => CapturedCharset.Identifier("  "));
        var charset = new CapturedCharset("charset1");
        charset.Add("room1", Room());
        Assert.ThrowsExactly<ArgumentException>(() => charset.Add("room1", Room()));
    }
}
