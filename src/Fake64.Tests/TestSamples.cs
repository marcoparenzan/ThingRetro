using Fake64.Crt;
using Fake64.Prg;
using Fake64.Samples;
using static Fake64.Tests.TestHelpers;

namespace Fake64.Tests;

// Gli esempi di samples/ eseguiti nell'emulatore: se il generatore o l'emulatore si rompono, se ne accorgono i test
[TestClass]
public sealed class SampleTests
{
    const long CYCLES_PER_SECOND = 985248;
    const int FRAME_CYCLES = 312 * 63;
    const int LEFT = 42, TOP = 42; // angolo in alto a sinistra dell'area di display nel frame buffer

    static Sample Get(string fileName) => SampleSet.All.Single(sample => sample.FileName == fileName);

    // Avvio, caricamento e RUN, come fa l'app con un .prg trascinato sulla finestra
    static Board Run(string fileName, double seconds = 1)
    {
        var board = NewBoard();
        board.RunCycles(3 * CYCLES_PER_SECOND);
        board.LoadPrg(Get(fileName).Content);
        board.TypeText("RUN\r");
        board.RunCycles((long)(seconds * CYCLES_PER_SECOND));
        return board;
    }

    static Board Insert(string fileName, double seconds = 2)
    {
        var board = NewBoard();
        board.InsertCartridge(CrtFile.Load(Get(fileName).Content));
        board.RunCycles((long)(seconds * CYCLES_PER_SECOND));
        return board;
    }

    static string Screen(Board board) => string.Join("\n", board.ScreenLines());

    // Colori dei pixel di una riga di schermo (in pixel del frame buffer)
    static HashSet<byte> RowColors(Board board, int y) =>
        Enumerable.Range(LEFT, 320).Select(x => board.Vic.PixelAt(x, y)).ToHashSet();

    // Valori assunti da un registro campionandolo per un certo tempo
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

    #region Tokenizzatore BASIC

    [TestMethod]
    public void Tokenizer_ShouldProduceTheSameBytesAsTheC64()
    {
        // 10 PRINT 6*7: PRINT = $99, * = $AC, collegamento alla riga successiva a $080B
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

        // Riga 10: PRINT "TO" : REM GOTO (TO e GOTO restano testo)
        CollectionAssert.AreEqual(
            new byte[] { 0x99, 0x20, 0x22, 0x54, 0x4F, 0x22, 0x3A, 0x8F, 0x20, 0x47, 0x4F, 0x54, 0x4F },
            program[6..19]);
        // Riga 20: DATA AND,1 : PRINT (dopo i due punti si tokenizza di nuovo)
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

        // Colori 0-15 tranne il 6 (blu come lo sfondo); il colore è anche la colonna di partenza
        var screen = board.ScreenLines();
        for (int line = 0; line < 15; line++)
        {
            int color = line < 6 ? line : line + 1;
            Assert.AreEqual(new string(' ', color) + "HELLO, FAKE64!", screen[line]);
            Assert.AreEqual(color, board.Peek((ushort)(0xD800 + line * 40 + color)) & 0x0F);
        }
    }

    [TestMethod]
    public void Math_ShouldPrintResults()
    {
        var screen = Screen(Run("math.prg"));

        // 4*ATN(1) in virgola mobile del BASIC: le ultime cifre dipendono dall'arrotondamento dell'arcotangente
        StringAssert.Contains(screen, " 42\n 1024\nPI GRECO 3.14159");
        StringAssert.Contains(screen, "RADICE DI 2 1.41421356");
    }

    [TestMethod]
    public void Sprite_ShouldBeEnabledAndMove()
    {
        var board = Run("sprite.prg", seconds: 2);

        Assert.AreEqual(0x01, board.Peek(0xD015) & 0x01);
        Assert.AreEqual(13, (int)board.Peek(0x07F8));
        int x = board.Peek(0xD000), y = board.Peek(0xD001);
        board.RunCycles(CYCLES_PER_SECOND / 2);
        Assert.IsTrue(board.Peek(0xD000) != x || board.Peek(0xD001) != y, "lo sprite non si muove");
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
        Press(C64Key.CursorDown, C64Key.LeftShift); // cursore su

        CollectionAssert.IsSubsetOf(new[] { "F1", "F8", "CURSORE SU" }, board.ScreenLines());
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

        Assert.IsTrue(board.Peek(0xD000) > x, "lo sprite non si è spostato a destra");
        Assert.AreEqual(13, board.Peek(0xD027) & 0x0F); // verde chiaro con il fuoco premuto
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
        StringAssert.Contains(Screen(board), "POSIZIONE 55");
    }

    #endregion

    #region Interrupt e CPU

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

        // 20 NMI al secondo, un colore ogni 10: circa 2 colori al secondo
        int colors = Sample(board, 0xD020, seconds: 3).Count;
        Assert.IsTrue(colors >= 5, $"colori del bordo: {colors}");
    }

    [TestMethod]
    public void Decimal_ShouldCountFiftyPerSecondInBcd()
    {
        var board = Run("decimal.prg");

        int Counter()
        {
            var digits = Enumerable.Range(0, 4).Select(i => board.Peek((ushort)(0x0400 + i)) - 0x30).ToArray();
            Assert.IsTrue(digits.All(d => d is >= 0 and <= 9), "cifre non decimali: " + string.Join(",", digits));
            return digits[0] * 1000 + digits[1] * 100 + digits[2] * 10 + digits[3];
        }

        int before = Counter();
        board.RunCycles(2 * CYCLES_PER_SECOND);
        int after = Counter();

        Assert.IsTrue(after - before is >= 98 and <= 102, $"{before} -> {after}");
        StringAssert.StartsWith(board.ScreenLines()[0][4..], "  <- CONTATORE BCD");
    }

    #endregion

    #region VIC-II

    [TestMethod]
    public void Multicolor_ShouldShowAllFourColorSources()
    {
        var board = Run("multicolor.prg");

        Assert.AreEqual(0x10, board.Peek(0xD016) & 0x10);
        var colors = RowColors(board, TOP + 8 * 3); // riga di testo 3: color RAM = 3 (ciano)
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
            Assert.IsTrue(RowColors(board, TOP + 8 * row).Contains(expected), $"sfondo della riga {row}");
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
        Assert.AreEqual("COLLISIONI: 11000000", board.ScreenLines()[22]);
        Assert.AreEqual(0x30, (int)board.Peek(0xD01C));
    }

    [TestMethod]
    public void Scroll_ShouldMoveTheTextLine()
    {
        var board = Run("scroll.prg", seconds: 3);

        string first = board.ScreenLines()[12];
        board.RunCycles(CYCLES_PER_SECOND / 2);
        string second = board.ScreenLines()[12];

        StringAssert.Contains(first + second, "FAKE64");
        Assert.AreNotEqual(first, second);
    }

    [TestMethod]
    public void VicBank_ShouldMoveScreenAndCharacters()
    {
        var board = Run("vicbank.prg");

        Assert.AreEqual(1, board.VicBank);
        Assert.AreEqual(0x44, (int)board.Peek(0x0288));
        Assert.AreEqual(0x18 ^ 0xFF, (int)board.Peek(0x6008)); // prima riga della A invertita (nella ROM: $18)
        var bankScreen = Enumerable.Range(0, 1000).Select(i => board.Peek((ushort)(0x4400 + i))).ToArray();
        Assert.IsTrue(bankScreen.Contains((byte)18), "il testo non è nello schermo a $4400"); // R di READY
    }

    #endregion

    #region Cartucce

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
        Assert.AreEqual("QUESTO TESTO E' NEL BANCO 0", screen[0]);
        StringAssert.StartsWith(screen[1], "E QUESTO NEL BANCO 1");
        Assert.AreEqual("Magic Desk", board.Cartridge!.TypeName);
    }

    #endregion

    [TestMethod]
    public void SamplesFolder_ShouldMatchTheGenerator()
    {
        // I file in samples/ devono essere quelli prodotti dal generatore (dotnet run --project src/Fake64.Samples)
        string folder = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples");
        if (!Directory.Exists(folder)) Assert.Inconclusive("Cartella samples non trovata");

        foreach (var sample in SampleSet.All)
            CollectionAssert.AreEqual(sample.Content, File.ReadAllBytes(Path.Combine(folder, sample.FileName)), sample.FileName);
    }
}
