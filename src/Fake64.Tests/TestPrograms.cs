using Fake64.Prg;
using static Fake64.Tests.TestHelpers;

namespace Fake64.Tests;

[TestClass]
public sealed class ProgramLoadingTests
{
    const long CYCLES_PER_SECOND = 985248;
    const int FRAME_CYCLES = 312 * 63;

    // 10 PRINT 6*7 (tokenizzato: PRINT = $99, * = $AC), caricato a $0801
    static readonly byte[] BasicPrintProgram =
    [
        0x01, 0x08,                         // indirizzo di caricamento $0801
        0x0A, 0x08, 0x0A, 0x00,             // link alla riga successiva ($080A), numero di riga 10
        0x99, 0x36, 0xAC, 0x37, 0x00,       // PRINT 6*7, fine riga
        0x00, 0x00,                         // fine del programma
    ];

    // Raster bar: un raster interrupt a riga 100 colora il bordo di rosso, uno a riga 150 lo rimette azzurro.
    // Il gestore condivide $0314 con l'IRQ della CIA1, che continua a passare al KERNAL ($EA31)
    static readonly byte[] RasterBarProgram =
    [
        0x00, 0xC0,                         // indirizzo di caricamento $C000
        0x78,                               // C000 SEI
        0xA9, 0x01, 0x8D, 0x1A, 0xD0,       // C001 LDA #$01 : STA $D01A
        0xA9, 0x64, 0x8D, 0x12, 0xD0,       // C006 LDA #100 : STA $D012
        0xAD, 0x11, 0xD0, 0x29, 0x7F,       // C00B LDA $D011 : AND #$7F
        0x8D, 0x11, 0xD0,                   // C010 STA $D011
        0xA9, 0x1F, 0x8D, 0x14, 0x03,       // C013 LDA #<irq : STA $0314
        0xA9, 0xC0, 0x8D, 0x15, 0x03,       // C018 LDA #>irq : STA $0315
        0x58,                               // C01D CLI
        0x60,                               // C01E RTS
        0xAD, 0x19, 0xD0,                   // C01F irq: LDA $D019
        0x30, 0x03,                         // C022 BMI raster
        0x4C, 0x31, 0xEA,                   // C024 JMP $EA31 (IRQ della CIA1)
        0x8D, 0x19, 0xD0,                   // C027 raster: STA $D019 (acknowledge)
        0xAD, 0x12, 0xD0,                   // C02A LDA $D012
        0xC9, 0x78,                         // C02D CMP #120
        0xB0, 0x0D,                         // C02F BCS bottom
        0xA9, 0x02, 0x8D, 0x20, 0xD0,       // C031 LDA #2 : STA $D020
        0xA9, 0x96, 0x8D, 0x12, 0xD0,       // C036 LDA #150 : STA $D012
        0x4C, 0x81, 0xEA,                   // C03B JMP $EA81 (ripristina i registri, RTI)
        0xA9, 0x0E, 0x8D, 0x20, 0xD0,       // C03E bottom: LDA #14 : STA $D020
        0xA9, 0x64, 0x8D, 0x12, 0xD0,       // C043 LDA #100 : STA $D012
        0x4C, 0x81, 0xEA,                   // C048 JMP $EA81
    ];

    private static Board BootedBoard()
    {
        var board = NewBoard();
        board.RunCycles(3 * CYCLES_PER_SECOND);
        return board;
    }

    private static string Screen(Board board) => string.Join("\n", board.ScreenLines());

    [TestMethod]
    public void LoadPrg_ShouldCopyBytesAtLoadAddress()
    {
        var board = NewBoard();

        var (start, end) = board.LoadPrg(new byte[] { 0x00, 0xC0, 0x11, 0x22, 0x33 });

        Assert.AreEqual(0xC000, (int)start);
        Assert.AreEqual(0xC003, (int)end);
        Assert.AreEqual(0x11, (int)board.Peek(0xC000));
        Assert.AreEqual(0x33, (int)board.Peek(0xC002));
    }

    [TestMethod]
    public void LoadPrg_ShouldWriteRamUnderRom()
    {
        var board = NewBoard();

        board.LoadPrg(new byte[] { 0x00, 0xA0, 0x42 });

        board.SetCpuPort(0x36); // BASIC ROM disattivata
        Assert.AreEqual(0x42, (int)board.Peek(0xA000));
    }

    [TestMethod]
    public void LoadPrg_ShouldRejectTooShortOrOverflowingFiles()
    {
        var board = NewBoard();

        Assert.ThrowsException<ArgumentException>(() => board.LoadPrg(new byte[] { 0x01, 0x08 }));
        Assert.ThrowsException<ArgumentException>(() => board.LoadPrg(new byte[] { 0xFF, 0xFF, 0x01, 0x02 }));
    }

    [TestMethod]
    public void LoadPrg_BasicProgram_ShouldUpdateBasicPointers()
    {
        var board = BootedBoard();

        var (start, end) = board.LoadPrg(BasicPrintProgram);

        Assert.IsTrue(board.IsBasicProgram(start));
        Assert.AreEqual((int)end, board.Peek(0x2D) | board.Peek(0x2E) << 8); // VARTAB
        Assert.AreEqual((int)end, board.Peek(0x31) | board.Peek(0x32) << 8); // STREND
    }

    [TestMethod]
    public void LoadPrg_BasicProgram_ShouldListAndRun()
    {
        var board = BootedBoard();
        board.LoadPrg(BasicPrintProgram);

        board.TypeText("LIST\r");
        board.RunCycles(CYCLES_PER_SECOND / 2);
        board.TypeText("RUN\r");
        board.RunCycles(CYCLES_PER_SECOND / 2);

        var screen = Screen(board);
        StringAssert.Contains(screen, "10 PRINT6*7\nREADY.");
        StringAssert.Contains(screen, "RUN\n 42\n");
    }

    [TestMethod]
    public void LoadPrg_MachineCode_ShouldRunWithSysAndSplitBorder()
    {
        var board = BootedBoard();
        var (start, _) = board.LoadPrg(RasterBarProgram);
        Assert.IsFalse(board.IsBasicProgram(start));

        board.TypeText("SYS49152\r");
        board.RunCycles(CYCLES_PER_SECOND / 2);
        board.RunCycles(FRAME_CYCLES); // un frame completo con le raster bar attive

        const byte RED = 2, LIGHT_BLUE = 14;
        byte BorderAt(int rasterLine) => board.Vic.PixelAt(0, rasterLine - 9);
        Assert.AreEqual(LIGHT_BLUE, BorderAt(98));
        Assert.AreEqual(RED, BorderAt(102));
        Assert.AreEqual(RED, BorderAt(148));
        Assert.AreEqual(LIGHT_BLUE, BorderAt(152));

        // Il KERNAL continua a ricevere l'IRQ della CIA1: tastiera e BASIC funzionano ancora
        board.TypeText("?1+1\r");
        board.RunCycles(CYCLES_PER_SECOND / 2);
        StringAssert.Contains(Screen(board), "?1+1\n 2\n");
    }

    [TestMethod]
    public void TypeText_ShouldRejectMoreThanTenCharacters()
    {
        var board = NewBoard();

        Assert.ThrowsException<ArgumentException>(() => board.TypeText("12345678901"));
    }
}
