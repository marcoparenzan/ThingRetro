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
    public void RasterBars_ShouldSplitTheBorderAndKeepBasicAlive()
    {
        var board = Run("rasterbars.prg");
        board.RunCycles(FRAME_CYCLES);

        const byte RED = 2, LIGHT_BLUE = 14;
        Assert.AreEqual(LIGHT_BLUE, board.Vic.PixelAt(0, 90 - 9));
        Assert.AreEqual(RED, board.Vic.PixelAt(0, 120 - 9));
        Assert.AreEqual(LIGHT_BLUE, board.Vic.PixelAt(0, 160 - 9));
        Assert.AreEqual("READY.", board.ScreenLines().Last(line => line.Length > 0));
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
        Press(C64Key.F7, C64Key.LeftShift);   // F8
        Press(C64Key.CursorDown, C64Key.LeftShift); // cursore su

        var screen = board.ScreenLines();
        CollectionAssert.IsSubsetOf(new[] { "F1", "F8", "CURSORE SU" }, screen);
    }

    [TestMethod]
    public void Bitmap_ShouldDrawAPatternInHiresMode()
    {
        var board = Run("bitmap.prg");
        board.RunCycles(FRAME_CYCLES);

        Assert.AreEqual(0x3B, board.Peek(0xD011) & 0x7F);
        var colors = new HashSet<byte>();
        for (int x = 42; x < 42 + 320; x++) colors.Add(board.Vic.PixelAt(x, 42 + 100));
        CollectionAssert.AreEquivalent(new byte[] { 6, 14 }, colors.ToArray()); // pixel spenti blu, accesi azzurri
    }

    [TestMethod]
    public void HelloCartridge_ShouldStartByItself()
    {
        var board = NewBoard();
        board.InsertCartridge(CrtFile.Load(Get("hello.crt").Content));

        board.RunCycles(2 * CYCLES_PER_SECOND);

        Assert.AreEqual("HELLO FROM CARTRIDGE", board.ScreenLines()[0]);
        Assert.AreEqual("Normal 8K", board.Cartridge!.TypeName);
    }

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
