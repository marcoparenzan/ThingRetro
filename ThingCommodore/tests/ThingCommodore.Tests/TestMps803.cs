using System.IO.Compression;
using System.Text;
using Thing64.C64;
using ThingCommodore.Software;
using ThingCommodore.Drives;
using ThingCommodore.Printers;

namespace ThingCommodore.Tests;

// The MPS-803 alone (codes in, dots and PDF out), and then on the IEC cable of a Thing64 whose KERNAL prints with OPEN and PRINT#
[TestClass]
public sealed class Mps803Tests
{
    const long ComputerHz = 985_248;

    static byte[] Rom(string name)
    {
        var file = Path.Combine(AppContext.BaseDirectory, "roms", name);
        if (!File.Exists(file)) Assert.Inconclusive($"ROM missing: {file}");
        return File.ReadAllBytes(file);
    }

    static Mps803 NewPrinter(int device = 4) => new(Rom("c-64-chargen.rom"), device);

    static void Print(Mps803 printer, string text)
    {
        foreach (char c in text) printer.Print((byte)c);
    }

    // Does the sheet have a dot in this character cell (column 0-79, text line 0, ...)?
    static int Dots(DotPaper paper, int sheet, int column, int line, int cell = 24)
    {
        int count = 0;
        for (int row = 0; row < 8; row++)
            for (int dot = 0; dot < cell; dot++)
                if (paper[sheet, column * cell + dot, line * 12 + row]) count++;
        return count;
    }

    [TestMethod]
    public void Text_ShouldDrawTheGlyphsOfTheCharacterGenerator()
    {
        var chargen = Rom("c-64-chargen.rom");
        var printer = NewPrinter();

        Print(printer, "H");

        // the H is screen code 8: every dot of its 8 x 8 matrix is 3 dots of the paper wide
        for (int row = 0; row < 8; row++)
            for (int column = 0; column < 8; column++)
            {
                bool expected = (chargen[8 * 8 + row] & 0x80 >> column) != 0;
                Assert.AreEqual(expected, printer.Paper[0, column * 3 + 1, row], $"row {row} column {column}");
            }
    }

    [TestMethod]
    public void Characters_ShouldAdvanceOneCellAndBreakTheLineAfter80()
    {
        var printer = NewPrinter();

        Print(printer, new string('A', 80) + "B");

        Assert.IsTrue(Dots(printer.Paper, 0, 79, 0) > 0, "the 80th column has a letter");
        Assert.IsTrue(Dots(printer.Paper, 0, 0, 1) > 0, "the 81st letter went on the next line");
    }

    [TestMethod]
    public void CarriageReturn_ShouldFeedALine()
    {
        var printer = NewPrinter();

        Print(printer, "A\rB");

        Assert.IsTrue(Dots(printer.Paper, 0, 0, 1) > 0);
        Assert.AreEqual(Dots(printer.Paper, 0, 0, 0) > 0, Dots(printer.Paper, 0, 0, 1) > 0);
    }

    [TestMethod]
    public void DoubleWidth_ShouldDrawTwiceAsWide()
    {
        var normal = NewPrinter();
        var wide = NewPrinter();
        Print(normal, "A");
        wide.Print(14);
        Print(wide, "A");

        int normalDots = Dots(normal.Paper, 0, 0, 0, 24);
        int wideDots = Dots(wide.Paper, 0, 0, 0, 24) + Dots(wide.Paper, 0, 1, 0, 24);

        Assert.AreEqual(normalDots * 2, wideDots);
    }

    [TestMethod]
    public void Reverse_ShouldInvertTheCell()
    {
        var plain = NewPrinter();
        var inverted = NewPrinter();
        Print(plain, "A");
        inverted.Print(18);
        Print(inverted, "A");

        Assert.AreEqual(8 * 24, Dots(plain.Paper, 0, 0, 0) + Dots(inverted.Paper, 0, 0, 0));
    }

    [TestMethod]
    public void BitImage_ShouldPrintColumnsOfSevenDots()
    {
        var printer = NewPrinter();

        printer.Print(8);
        printer.Print(128 + 1);          // the top dot
        printer.Print(128 + 64);         // the bottom one
        printer.Print(128 + 127);        // all seven

        var paper = printer.Paper;
        Assert.IsTrue(paper[0, 0, 0] && !paper[0, 0, 6], "first column: top only");
        Assert.IsTrue(!paper[0, 4, 0] && paper[0, 4, 6], "second column: bottom only");
        for (int row = 0; row < 7; row++) Assert.IsTrue(paper[0, 8, row], $"third column, row {row}");
        Assert.IsFalse(paper[0, 8, 7]);
    }

    [TestMethod]
    public void BitImage_Repeat_ShouldCopyTheLastColumn()
    {
        var printer = NewPrinter();

        printer.Print(8);
        printer.Print(128 + 5);
        printer.Print(26);
        printer.Print(3);

        for (int column = 0; column < 4; column++)
            Assert.IsTrue(printer.Paper[0, column * 4, 0] && printer.Paper[0, column * 4, 2], $"column {column}");
        Assert.IsFalse(printer.Paper[0, 16, 0], "and no more");
    }

    [TestMethod]
    public void Tab_ShouldMoveToAColumn()
    {
        var printer = NewPrinter();

        printer.Print(16);
        Print(printer, "10");
        Print(printer, "A");

        Assert.AreEqual(0, Dots(printer.Paper, 0, 9, 0));
        Assert.IsTrue(Dots(printer.Paper, 0, 10, 0) > 0);
    }

    [TestMethod]
    public void FormFeed_ShouldStartANewSheet()
    {
        var printer = NewPrinter();
        Print(printer, "A");

        printer.Print(12);
        Print(printer, "B");

        Assert.AreEqual(2, printer.Paper.Sheets);
        Assert.IsTrue(Dots(printer.Paper, 1, 0, 0) > 0);
    }

    [TestMethod]
    public void LongText_ShouldFlowOntoANewSheet()
    {
        var printer = NewPrinter();

        for (int line = 0; line < 70; line++) Print(printer, "LINE\r");

        Assert.AreEqual(2, printer.Paper.Sheets);
    }

    [TestMethod]
    public void ToPdf_ShouldWriteAReadableFile()
    {
        var printer = NewPrinter();
        Print(printer, "HELLO\r");
        printer.Print(12);
        Print(printer, "PAGE TWO\r");

        var pdf = printer.ToPdf();

        var text = Encoding.Latin1.GetString(pdf);
        StringAssert.StartsWith(text, "%PDF-1.4");
        StringAssert.Contains(text, "/Count 2");
        StringAssert.Contains(text, "%%EOF");
        // the cross-reference table points at the objects
        int xref = int.Parse(text[(text.LastIndexOf("startxref", StringComparison.Ordinal) + 10)..].Split('\n')[0]);
        Assert.AreEqual("xref", text.Substring(xref, 4));
        // the first content stream inflates to rectangles
        int start = text.IndexOf("stream\n", StringComparison.Ordinal) + 7;
        int end = text.IndexOf("\nendstream", start, StringComparison.Ordinal);
        using var inflater = new ZLibStream(new MemoryStream(pdf, start, end - start), CompressionMode.Decompress);
        using var reader = new StreamReader(inflater);
        string content = reader.ReadToEnd();
        StringAssert.StartsWith(content, "0 g");
        StringAssert.Contains(content, " re");
    }

    // ---- on a Thing64 -------------------------------------------------------------------------------------------------------

    static Board NewComputer(Mps803 printer)
    {
        var board = new Board(new SystemRoms(Rom("c-64-kernal.rom"), Rom("c-64-basic.rom"), Rom("c-64-chargen.rom")), startClock: false);
        var cable = new ThingRetroEmu.Buses.IecBus();
        cable.Attach(() => board.Iec.AtnLow(), () => board.Iec.ClkLow(), () => board.Iec.DataLow(),
            input => board.Iec.ClkInputLow = input, input => board.Iec.DataInputLow = input);
        printer.Plug(cable);
        board.AfterCycle = printer.Tick;
        board.RunCycles(3 * ComputerHz);
        return board;
    }

    static void Enter(Board board, string line, long settleCycles)
    {
        for (int i = 0; i < line.Length; i += 9)
        {
            board.TypeText(line.Substring(i, Math.Min(9, line.Length - i)));
            board.RunCycles(ComputerHz / 10);
        }
        board.TypeText("\r");
        board.RunCycles(settleCycles);
    }

    static string Screen(Board board)
    {
        var text = new StringBuilder();
        for (int row = 0; row < 25; row++)
        {
            for (int column = 0; column < 40; column++)
            {
                byte code = (byte)(board.Address((ushort)(0x0400 + row * 40 + column)) & 0x7F);
                text.Append(code is >= 1 and <= 26 ? (char)('A' + code - 1) : code is >= 0x20 and < 0x60 ? (char)code : '.');
            }
            text.AppendLine();
        }
        return text.ToString();
    }

    [TestMethod]
    [Timeout(600_000)]
    public void Open_WithoutAPrinter_ShouldSayDeviceNotPresent()
    {
        var board = new Board(new SystemRoms(Rom("c-64-kernal.rom"), Rom("c-64-basic.rom"), Rom("c-64-chargen.rom")), startClock: false);
        board.RunCycles(3 * ComputerHz);

        Enter(board, "OPEN1,4:PRINT#1,\"HI\":CLOSE1", 3 * ComputerHz);

        StringAssert.Contains(Screen(board), "DEVICE NOT PRESENT");
    }

    [TestMethod]
    [Timeout(600_000)]
    public void Print_FromBasic_ShouldPutTheTextOnPaper()
    {
        var printer = NewPrinter();
        var chargen = Rom("c-64-chargen.rom");
        var board = NewComputer(printer);

        Enter(board, "OPEN1,4:PRINT#1,\"HELLO FROM THE C64\":CLOSE1", 3 * ComputerHz);

        var screen = Screen(board);
        Assert.IsFalse(screen.Contains("DEVICE NOT PRESENT"), screen);
        Assert.IsFalse(screen.Contains("ERROR"), screen);
        var paper = printer.Paper;
        // the first letter is an H: screen code 8
        for (int row = 0; row < 8; row++)
            for (int column = 0; column < 8; column++)
            {
                bool expected = (chargen[8 * 8 + row] & 0x80 >> column) != 0;
                Assert.AreEqual(expected, paper[0, column * 3 + 1, row], $"row {row} column {column}\n{screen}");
            }
        Assert.IsTrue(Dots(paper, 0, 17, 0) > 0, "the last letter (the 18th)");
        Assert.AreEqual(0, Dots(paper, 0, 18, 0), "and nothing after it");

        // the page as a PDF, for looking at
        var pdf = printer.ToPdf();
        File.WriteAllBytes(Path.Combine(AppContext.BaseDirectory, "mps803-hello.pdf"), pdf);
    }

    [TestMethod]
    [Timeout(600_000)]
    public void Print_WithTheLowerCaseChannel_ShouldUseTheSecondSet()
    {
        var printer = NewPrinter();
        var chargen = Rom("c-64-chargen.rom");
        var board = NewComputer(printer);

        Enter(board, "OPEN1,4,7:PRINT#1,\"A\":CLOSE1", 3 * ComputerHz);

        // channel 7: an unshifted A comes out as the lower case a (screen code 1 in the second set)
        for (int row = 0; row < 8; row++)
            for (int column = 0; column < 8; column++)
            {
                bool expected = (chargen[0x800 + 8 + row] & 0x80 >> column) != 0;
                Assert.AreEqual(expected, printer.Paper[0, column * 3 + 1, row], $"row {row} column {column}");
            }
    }
    [TestMethod]
    [Timeout(900_000)]
    public void List_ToThePrinter_ShouldPrintTheProgram()
    {
        var printer = NewPrinter();
        var board = NewComputer(printer);

        Enter(board, "10 PRINT\"HELLO\"", ComputerHz / 2);
        Enter(board, "20 PRINT\"WORLD\"", ComputerHz / 2);
        Enter(board, "OPEN1,4:CMD1:LIST", 4 * ComputerHz);
        Enter(board, "PRINT#1:CLOSE1", 3 * ComputerHz);

        var screen = Screen(board);
        Assert.IsFalse(screen.Contains("DEVICE NOT PRESENT"), screen);
        // "10 PRINT"HELLO"" and "20 PRINT"WORLD"": two lines of text, with the blank line the LIST starts with
        int inked = 0;
        for (int line = 0; line < 5; line++)
            if (Enumerable.Range(0, 20).Sum(column => Dots(printer.Paper, 0, column, line)) > 0) inked++;
        Assert.AreEqual(2, inked, "two lines have ink: " + screen);
    }

    [TestMethod]
    [Timeout(900_000)]
    public void Printer_AndDrive_ShouldShareTheCable()
    {
        var printer = NewPrinter();
        var board = new Board(new SystemRoms(Rom("c-64-kernal.rom"), Rom("c-64-basic.rom"), Rom("c-64-chargen.rom")), startClock: false);
        var cable = new ThingRetroEmu.Buses.IecBus();
        cable.Attach(() => board.Iec.AtnLow(), () => board.Iec.ClkLow(), () => board.Iec.DataLow(),
            input => board.Iec.ClkInputLow = input, input => board.Iec.DataInputLow = input);
        printer.Plug(cable);
        var drive = new Board1541(Rom("c-drive-1541.rom")) { DeviceNumber = 8, Disk = ThingRetroEmu.Media.D64Writer.Create("BOTH", []) };
        drive.Plug(cable);
        var driveClock = new DriveClock(ComputerHz, drive.Tick);
        board.AfterCycle = () => { printer.Tick(); driveClock.OnComputerCycle(); };
        board.RunCycles(3 * ComputerHz);

        Enter(board, "OPEN1,4:PRINT#1,\"X\":CLOSE1", 3 * ComputerHz);
        Enter(board, "LOAD\"$\",8", 8 * ComputerHz);
        Enter(board, "LIST", ComputerHz);

        var screen = Screen(board);
        Assert.IsFalse(screen.Contains("NOT PRESENT"), screen);
        StringAssert.Contains(screen, "BOTH");
        Assert.IsTrue(Dots(printer.Paper, 0, 0, 0) > 0, "the printer printed");
        Assert.AreEqual(0, Dots(printer.Paper, 0, 1, 0), "only one letter");
    }
}
