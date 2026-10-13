using Thing64.Crt;
using Thing64.Prg;
using Thing64.Basic;
using Thing64.Cli;
using static Thing64.Tests.TestHelpers;

namespace Thing64.Tests;

// The samples of samples/ run in the emulator: if the generator or the emulator breaks, the tests notice
[TestClass]
public sealed class SampleTests
{
    const long CYCLES_PER_SECOND = 985248;
    const int FRAME_CYCLES = 312 * 63;
    const int LEFT = 42, TOP = 42; // top left corner of the display area in the frame buffer

    static byte[] Get(string fileName) => SampleFile(fileName);

    // Boot, load and RUN, as the app does with a .prg dropped on the window
    static Board Run(string fileName, double seconds = 1)
    {
        var board = NewBoard();
        board.RunCycles(3 * CYCLES_PER_SECOND);
        board.LoadPrg(Get(fileName));
        board.TypeText("RUN\r");
        board.RunCycles((long)(seconds * CYCLES_PER_SECOND));
        return board;
    }

    static Board Insert(string fileName, double seconds = 2)
    {
        var board = NewBoard();
        board.InsertCartridge(CrtFile.Load(Get(fileName)));
        board.RunCycles((long)(seconds * CYCLES_PER_SECOND));
        return board;
    }

    static string Screen(Board board) => string.Join("\n", board.ScreenLines());

    // Colors of the pixels of a screen line (in frame buffer pixels)
    static HashSet<byte> RowColors(Board board, int y) =>
        Enumerable.Range(LEFT, 320).Select(x => board.Vic.PixelAt(x, y)).ToHashSet();

    // Values taken by a register when sampled over some time
    static HashSet<int> Sample(Board board, ushort address, double seconds, int samples = 40)
    {
        var values = new HashSet<int>();
        for (int i = 0; i < samples; i++)
        {
            board.RunCycles((long)(seconds * CYCLES_PER_SECOND / samples));
            values.Add(board.Peek(address) & 0x0F);
        }
        return values;
    }

    #region BASIC tokenizer

    [TestMethod]
    public void Tokenizer_ShouldProduceTheSameBytesAsTheC64()
    {
        // 10 PRINT 6*7: PRINT = $99, * = $AC, link to the next line at $080B
        var program = BasicTokenizer.Tokenize("10 PRINT 6*7");

        CollectionAssert.AreEqual(new byte[]
        {
            0x01, 0x08, 0x0B, 0x08, 0x0A, 0x00, 0x99, 0x20, 0x36, 0xAC, 0x37, 0x00, 0x00, 0x00,
        }, program);
    }

    [TestMethod]
    public void Tokenizer_ShouldKeepStringsRemAndDataAsText()
    {
        var program = BasicTokenizer.Tokenize("10 PRINT \"TO\":REM GOTO\n20 DATA AND,1:PRINT");

        // Line 10: PRINT "TO" : REM GOTO (TO and GOTO stay text)
        CollectionAssert.AreEqual(
            new byte[] { 0x99, 0x20, 0x22, 0x54, 0x4F, 0x22, 0x3A, 0x8F, 0x20, 0x47, 0x4F, 0x54, 0x4F },
            program[6..19]);
        // Line 20: DATA AND,1 : PRINT (after the colon tokenizing starts again)
        int line20 = program[2] | program[3] << 8;
        int offset = line20 - 0x0801 + 2 + 4;
        CollectionAssert.AreEqual(new byte[] { 0x83, 0x20, 0x41, 0x4E, 0x44, 0x2C, 0x31, 0x3A, 0x99, 0x00 }, program[offset..(offset + 10)]);
    }

    #endregion

    #region BASIC

    [TestMethod]
    public void Hello_ShouldPrintFifteenColoredLines()
    {
        var board = Run("hello.prg");

        // Colors 0-15 except 6 (blue like the background); the color is also the starting column
        var screen = board.ScreenLines();
        for (int line = 0; line < 15; line++)
        {
            int color = line < 6 ? line : line + 1;
            Assert.AreEqual(new string(' ', color) + "HELLO, THING64!", screen[line]);
            Assert.AreEqual(color, board.Peek((ushort)(0xD800 + line * 40 + color)) & 0x0F);
        }
    }

    [TestMethod]
    public void Math_ShouldPrintResults()
    {
        var screen = Screen(Run("math.prg"));

        // 4*ATN(1) in BASIC floating point: the last digits depend on the rounding of the arctangent
        StringAssert.Contains(screen, " 42\n 1024\nPI 3.14159");
        StringAssert.Contains(screen, "SQUARE ROOT OF 2 1.41421356");
    }

    [TestMethod]
    public void Sprite_ShouldBeEnabledAndMove()
    {
        var board = Run("sprite.prg", seconds: 2);

        Assert.AreEqual(0x01, board.Peek(0xD015) & 0x01);
        Assert.AreEqual(13, (int)board.Peek(0x07F8));
        int x = board.Peek(0xD000), y = board.Peek(0xD001);
        board.RunCycles(CYCLES_PER_SECOND / 2);
        Assert.IsTrue(board.Peek(0xD000) != x || board.Peek(0xD001) != y, "the sprite does not move");
    }

    [TestMethod]
    public void Keys_ShouldNameFunctionAndCursorKeys()
    {
        var board = Run("keys.prg");

        void Press(params C64Key[] keys)
        {
            board.Keyboard.SetPressedKeys(keys);
            board.RunCycles(3 * FRAME_CYCLES);
            board.Keyboard.ReleaseAll();
            board.RunCycles(10 * FRAME_CYCLES);
        }

        Press(C64Key.F1);
        Press(C64Key.F7, C64Key.LeftShift);         // F8
        Press(C64Key.CursorDown, C64Key.LeftShift); // cursor up

        CollectionAssert.IsSubsetOf(new[] { "F1", "F8", "CURSOR UP" }, board.ScreenLines());
    }

    [TestMethod]
    public void Joystick_ShouldMoveTheSpriteAndChangeColorOnFire()
    {
        var board = Run("joystick.prg");
        var joystick = new Joystick();
        board.ControlPort2.Plug(joystick);
        int x = board.Peek(0xD000);

        joystick.Right = true;
        board.RunCycles(CYCLES_PER_SECOND / 2);
        joystick.Right = false;
        joystick.Fire = true;
        board.RunCycles(CYCLES_PER_SECOND / 4);

        Assert.IsTrue(board.Peek(0xD000) > x, "the sprite did not move to the right");
        Assert.AreEqual(13, board.Peek(0xD027) & 0x0F); // light green with fire pressed
    }

    [TestMethod]
    public void Paddle_ShouldPlaceTheSpriteFromTheKnob()
    {
        var board = Run("paddle.prg");
        var paddles = new Paddles { PositionA = 55 };
        board.ControlPort1.Plug(paddles);

        board.RunCycles(CYCLES_PER_SECOND / 2);
        Assert.AreEqual(24 + 255 - 55, (int)board.Peek(0xD000));

        paddles.FireA = true;
        board.RunCycles(CYCLES_PER_SECOND / 4);
        Assert.AreEqual(13, board.Peek(0xD027) & 0x0F);
        StringAssert.Contains(Screen(board), "POSITION 55");
    }

    #endregion

    #region SID

    // A speaker that keeps the samples it receives
    sealed class Recorder : ISpeaker
    {
        public readonly List<short> Samples = [];
        public int SampleRate => 44100;
        public void Play(ReadOnlySpan<short> samples) => Samples.AddRange(samples.ToArray());

        // Level (RMS) of a stretch, in seconds from the start of the recording
        public double Level(double from, double to)
        {
            var part = Samples.Skip((int)(from * SampleRate)).Take((int)((to - from) * SampleRate)).ToArray();
            return Math.Sqrt(part.Average(s => (double)s * s));
        }
    }

    static (Board Board, Recorder Speaker) RunWithSpeaker(string fileName, double seconds)
    {
        var board = NewBoard();
        board.RunCycles(3 * CYCLES_PER_SECOND);
        board.LoadPrg(Get(fileName));
        var speaker = new Recorder();
        _ = new AudioCable(board.AudioOut, speaker);
        board.TypeText("RUN\r");
        board.RunCycles((long)(seconds * CYCLES_PER_SECOND));
        return (board, speaker);
    }

    [TestMethod]
    public void Sound_ShouldPlayEightNotes()
    {
        var (board, speaker) = RunWithSpeaker("sound.prg", seconds: 4);

        CollectionAssert.Contains(board.ScreenLines(), "C D E F G A B C");
        Assert.IsTrue(speaker.Level(0.5, 1.5) > 1000, $"level {speaker.Level(0.5, 1.5):F0}");
    }

    [TestMethod]
    public void Music_ShouldPlayFromTheInterruptAndKeepBasicAlive()
    {
        var (board, speaker) = RunWithSpeaker("music.prg", seconds: 3);

        StringAssert.StartsWith(board.ScreenLines()[0], "MUSIC FROM THE SID, PLAYED BY THE IRQ");
        Assert.IsTrue(speaker.Level(1, 3) > 1000, $"level {speaker.Level(1, 3):F0}");
        Assert.AreEqual(0x08, (int)board.Peek(0x0315));                         // the IRQ vector points to the program
        board.TypeText("?1+1\r");
        board.RunCycles(CYCLES_PER_SECOND / 2);
        StringAssert.Contains(Screen(board), "?1+1\n 2\n");
    }

    #endregion

    #region Interrupts and CPU

    [TestMethod]
    public void Irq_ShouldToggleTheBackgroundAndKeepBasicAlive()
    {
        var board = Run("irq.prg");

        CollectionAssert.AreEquivalent(new[] { 0, 6 }, Sample(board, 0xD021, seconds: 2).ToArray());
        board.TypeText("?1+1\r");
        board.RunCycles(CYCLES_PER_SECOND / 2);
        StringAssert.Contains(Screen(board), "?1+1\n 2\n");
    }

    [TestMethod]
    public void Nmi_ShouldCycleTheBorderColor()
    {
        var board = Run("nmi.prg");

        // 20 NMIs per second, one color every 10: about 2 colors per second
        int colors = Sample(board, 0xD020, seconds: 3).Count;
        Assert.IsTrue(colors >= 5, $"border colors: {colors}");
    }

    [TestMethod]
    public void Decimal_ShouldCountFiftyPerSecondInBcd()
    {
        var board = Run("decimal.prg");

        int Counter()
        {
            var digits = Enumerable.Range(0, 4).Select(i => board.Peek((ushort)(0x0400 + i)) - 0x30).ToArray();
            Assert.IsTrue(digits.All(d => d is >= 0 and <= 9), "non-decimal digits: " + string.Join(",", digits));
            return digits[0] * 1000 + digits[1] * 100 + digits[2] * 10 + digits[3];
        }

        int before = Counter();
        board.RunCycles(2 * CYCLES_PER_SECOND);
        int after = Counter();

        Assert.IsTrue(after - before is >= 98 and <= 102, $"{before} -> {after}");
        StringAssert.StartsWith(board.ScreenLines()[0][4..], "  <- BCD COUNTER");
    }

    #endregion

    #region VIC-II

    [TestMethod]
    public void Multicolor_ShouldShowAllFourColorSources()
    {
        var board = Run("multicolor.prg");

        Assert.AreEqual(0x10, board.Peek(0xD016) & 0x10);
        var colors = RowColors(board, TOP + 8 * 3); // text row 3: color RAM = 3 (cyan)
        CollectionAssert.IsSubsetOf(new byte[] { 6, 2, 7, 3 }, colors.ToArray());
    }

    [TestMethod]
    public void Ecm_ShouldUseFourBackgrounds()
    {
        var board = Run("ecm.prg");

        Assert.AreEqual(0x40, board.Peek(0xD011) & 0x40);
        for (int row = 2, background = 0; row <= 8; row += 2, background++)
        {
            byte expected = new byte[] { 6, 2, 5, 0 }[background];
            Assert.IsTrue(RowColors(board, TOP + 8 * row).Contains(expected), $"background of row {row}");
        }
    }

    [TestMethod]
    public void Bitmap_ShouldDrawAPatternInHiresMode()
    {
        var board = Run("bitmap.prg");
        board.RunCycles(FRAME_CYCLES);

        Assert.AreEqual(0x3B, board.Peek(0xD011) & 0x7F);
        CollectionAssert.AreEquivalent(new byte[] { 6, 14 }, RowColors(board, TOP + 100).ToArray());
    }

    [TestMethod]
    public void McBitmap_ShouldDrawFourStripesPerCell()
    {
        var board = Run("mcbitmap.prg");

        CollectionAssert.AreEquivalent(new byte[] { 0 }, RowColors(board, TOP).ToArray());          // %00
        CollectionAssert.AreEquivalent(new byte[] { 2 }, RowColors(board, TOP + 2).ToArray());      // %01
        CollectionAssert.AreEquivalent(new byte[] { 7 }, RowColors(board, TOP + 4).ToArray());      // %10
        Assert.IsTrue(RowColors(board, TOP + 6).Count >= 10);                                       // %11: color RAM
    }

    [TestMethod]
    public void Sprites_ShouldShowAllFeaturesAndReportTheCollision()
    {
        var board = Run("sprites.prg");

        Assert.AreEqual(0xFF, (int)board.Peek(0xD015));
        Assert.AreEqual("COLLISIONS: 11000000", board.ScreenLines()[22]);
        Assert.AreEqual(0x30, (int)board.Peek(0xD01C));
    }

    [TestMethod]
    public void Scroll_ShouldMoveTheTextLine()
    {
        var board = Run("scroll.prg", seconds: 3);

        string first = board.ScreenLines()[12];
        board.RunCycles(CYCLES_PER_SECOND / 2);
        string second = board.ScreenLines()[12];

        StringAssert.Contains(first + second, "THING64");
        Assert.AreNotEqual(first, second);
    }

    [TestMethod]
    public void VicBank_ShouldMoveScreenAndCharacters()
    {
        var board = Run("vicbank.prg");

        Assert.AreEqual(1, board.VicBank);
        Assert.AreEqual(0x44, (int)board.Peek(0x0288));
        Assert.AreEqual(0x18 ^ 0xFF, (int)board.Peek(0x6008)); // first row of the reversed A (in ROM: $18)
        var bankScreen = Enumerable.Range(0, 1000).Select(i => board.Peek((ushort)(0x4400 + i))).ToArray();
        Assert.IsTrue(bankScreen.Contains((byte)18), "the text is not in the screen at $4400"); // R of READY
    }

    [TestMethod]
    public void Borders_ShouldOpenTheBottomBorderAndShowSprites()
    {
        var board = Run("borders.prg", seconds: 2);

        // Raster line 260, in the bottom border: blue background instead of the light blue border, and the sprites (lines 256-276)
        int y = 260 - 9;
        Assert.AreEqual(6, (int)board.Vic.PixelAt(LEFT + 300, y));
        Assert.AreEqual(14, (int)board.Vic.PixelAt(LEFT - 10, y));     // the side borders stay
        // At least 3 sprites out of 4: the one that scrolls to an X under the (closed) side border is not visible
        Assert.IsTrue(new byte[] { 7, 1, 3, 13 }.Count(RowColors(board, y).Contains) >= 3);

        // The border stays open at every frame and BASIC answers
        board.RunCycles(FRAME_CYCLES * 10);
        Assert.AreEqual(6, (int)board.Vic.PixelAt(LEFT + 300, y));
        board.TypeText("?1+1\r");
        board.RunCycles(CYCLES_PER_SECOND / 2);
        StringAssert.Contains(Screen(board), "?1+1\n 2\n");
    }

    #endregion

    #region Cartridges

    [TestMethod]
    public void HelloCartridge_ShouldStartByItself()
    {
        var board = Insert("hello.crt");

        Assert.AreEqual("HELLO FROM CARTRIDGE", board.ScreenLines()[0]);
        Assert.AreEqual("Normal 8K", board.Cartridge!.TypeName);
    }

    [TestMethod]
    public void UltimaxCartridge_ShouldPaintARainbowWithoutKernal()
    {
        var board = Insert("ultimax.crt", seconds: 1);

        Assert.AreEqual("Ultimax", board.Cartridge!.TypeName);
        var colors = Enumerable.Range(0, 200).Select(y => board.Vic.PixelAt(0, y)).ToHashSet();
        Assert.AreEqual(16, colors.Count);
    }

    [TestMethod]
    public void BanksCartridge_ShouldPrintTextFromBothBanks()
    {
        var board = Insert("banks.crt");

        var screen = board.ScreenLines();
        Assert.AreEqual("THIS TEXT IS IN BANK 0", screen[0]);
        StringAssert.StartsWith(screen[1], "AND THIS IS IN BANK 1");
        Assert.AreEqual("Magic Desk", board.Cartridge!.TypeName);
    }

    #endregion

    [TestMethod]
    public void SamplesFolder_ShouldMatchItsManifest()
    {
        // The files in samples/ must be the ones built from samples/src (thing64 build samples/samples.json)
        var manifest = BuildManifest.Load(Path.Combine(SamplesFolder, "samples.json"));

        Assert.IsTrue(manifest.Outputs.Count >= 20);
        foreach (var output in manifest.Outputs)
            CollectionAssert.AreEqual(manifest.Build(output), SampleFile(output.File), output.File);
    }
}
