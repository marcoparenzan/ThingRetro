using Thing64.C64;
using ThingCommodore.Software;
using ThingCommodore.Tape;
using ThingRetroEmu.Media;
using ThingRetroEmu.Ports;

namespace ThingCommodore.Tests;

// The C2N alone, with a fake cassette connector, and then on a Thing64 whose KERNAL saves and loads through it
[TestClass]
public sealed class DatasetteTests
{
    const long ComputerHz = 985_248;

    static byte[] Rom(string name)
    {
        var file = Path.Combine(AppContext.BaseDirectory, "roms", name);
        if (!File.Exists(file)) Assert.Inconclusive($"ROM missing: {file}");
        return File.ReadAllBytes(file);
    }

    static TapImage TapeOf(params int[] cycles)
    {
        var tape = new TapImage();
        foreach (int c in cycles) tape.Append(c);
        return tape;
    }

    [TestMethod]
    public void TapImage_ShouldRoundTripThroughTheFile()
    {
        var tape = TapeOf(400, 600, 800, 2000, 300_000);

        var again = new TapImage(tape.ToBytes());

        CollectionAssert.AreEqual(new[] { 400, 600, 800, 2000, 300_000 }, again.Pulses.ToArray());
    }

    [TestMethod]
    public void TapImage_Version0_ShouldReadZeroAsAnOverflow()
    {
        var file = new byte[20 + 3];
        System.Text.Encoding.ASCII.GetBytes("C64-TAPE-RAW").CopyTo(file, 0);
        BitConverter.GetBytes(3).CopyTo(file, 16);
        file[20] = 50; file[21] = 0; file[22] = 60;

        var tape = new TapImage(file);

        CollectionAssert.AreEqual(new[] { 400, 2048, 480 }, tape.Pulses.ToArray());
    }

    [TestMethod]
    public void TapImage_WithoutTheHeader_ShouldBeRefused()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => new TapImage(new byte[40]));
    }

    [TestMethod]
    public void Keys_ShouldPullSenseLow()
    {
        var port = new CassettePort();
        var recorder = new Datasette { Tape = TapeOf(100) };
        recorder.Plug(port);
        Assert.IsFalse(port.SenseLow());

        recorder.Play();

        Assert.IsTrue(port.SenseLow());
        recorder.Stop();
        Assert.IsFalse(port.SenseLow());
    }

    [TestMethod]
    public void Play_WithTheMotorOff_ShouldNotMoveTheTape()
    {
        var port = new CassettePort { MotorOn = () => false };
        var recorder = new Datasette { Tape = TapeOf(100, 100) };
        recorder.Plug(port);
        recorder.Play();

        for (int i = 0; i < 1000; i++) recorder.Tick();

        Assert.AreEqual(0, recorder.Position);
        Assert.IsTrue(port.ReadHigh());
    }

    [TestMethod]
    public void Play_ShouldSendAFallingEdgePerPulse()
    {
        var port = new CassettePort { MotorOn = () => true };
        var recorder = new Datasette { Tape = TapeOf(500, 700, 1000) };
        recorder.Plug(port);
        recorder.Play();
        var edges = new List<int>();
        bool wasHigh = true;

        for (int cycle = 1; cycle <= 3000; cycle++)
        {
            recorder.Tick();
            bool high = port.ReadHigh();
            if (wasHigh && !high) edges.Add(cycle);
            wasHigh = high;
        }

        CollectionAssert.AreEqual(new[] { 500, 1200, 2200 }, edges);
        Assert.AreEqual(DatasetteKey.Stop, recorder.Key, "at the end of the tape the key springs up");
    }

    [TestMethod]
    public void Record_ShouldMeasureTheTimeBetweenRisingEdges()
    {
        bool write = true;
        var port = new CassettePort { MotorOn = () => true, WriteHigh = () => write };
        var tape = new TapImage();
        var recorder = new Datasette { Tape = tape };
        recorder.Plug(port);
        recorder.Record();

        // the line goes low at 100, 500, 1100 and 1900 and back high 50 cycles later: rising edges 400, 600 and 800 apart
        var edges = new HashSet<int> { 100, 500, 1100, 1900 };
        for (int cycle = 1; cycle <= 2000; cycle++)
        {
            if (edges.Contains(cycle)) write = false;
            else if (edges.Contains(cycle - 50)) write = true;
            recorder.Tick();
        }

        CollectionAssert.AreEqual(new[] { 400, 600, 800 }, tape.Pulses.ToArray());
    }

    [TestMethod]
    public void Rewind_ShouldGoBackToTheStartAndSpringUp()
    {
        var port = new CassettePort();
        var recorder = new Datasette { Tape = TapeOf(Enumerable.Repeat(100, 1000).ToArray()) };
        recorder.Plug(port);
        recorder.FastForward();
        for (int i = 0; i < 10; i++) recorder.Tick();
        Assert.IsTrue(recorder.Position > 0);

        recorder.Rewind();
        for (int i = 0; i < 10 && recorder.Key == DatasetteKey.Rewind; i++) recorder.Tick();

        Assert.AreEqual(0, recorder.Position);
        Assert.AreEqual(DatasetteKey.Stop, recorder.Key);
    }

    // ---- on a Thing64 -----------------------------------------------------------------------------------------------------

    static Board NewComputer(Datasette recorder)
    {
        var board = new Board(new SystemRoms(Rom("c-64-kernal.rom"), Rom("c-64-basic.rom"), Rom("c-64-chargen.rom")), startClock: false);
        recorder.Plug(board.Cassette);
        board.AfterCycle = recorder.Tick;
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
        var text = new System.Text.StringBuilder();
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
    public void Save_WithoutAKeyDown_ShouldAskToPressRecordAndPlay()
    {
        var recorder = new Datasette { Tape = new TapImage() };
        var computer = NewComputer(recorder);
        Enter(computer, "10 PRINT\"HI\"", ComputerHz / 2);

        Enter(computer, "SAVE\"X\"", 2 * ComputerHz);

        StringAssert.Contains(Screen(computer), "PRESS RECORD & PLAY ON TAPE");
    }

    [TestMethod]
    [Timeout(900_000)]
    public void SaveThenLoad_ThroughTheTape_ShouldBringTheProgramBack()
    {
        var recorder = new Datasette { Tape = new TapImage() };
        var computer = NewComputer(recorder);
        Enter(computer, "10 PRINT\"HELLO FROM TAPE\"", ComputerHz / 2);
        Enter(computer, "SAVE\"X\"", 2 * ComputerHz);
        StringAssert.Contains(Screen(computer), "PRESS RECORD & PLAY ON TAPE");
        recorder.Record();
        computer.RunCycles(90 * ComputerHz);
        recorder.Stop();

        StringAssert.Contains(Screen(computer), "SAVING X");
        Assert.IsTrue(recorder.Tape!.Count > 1000, "the header and the program were recorded as pulses");

        // The tape goes through a file and back, as it would between sessions
        var tape = new TapImage(recorder.Tape.ToBytes());
        var playback = new Datasette { Tape = tape };
        var second = NewComputer(playback);

        Enter(second, "LOAD", 2 * ComputerHz);
        StringAssert.Contains(Screen(second), "PRESS PLAY ON TAPE");
        playback.Play();
        second.RunCycles(60 * ComputerHz);
        playback.Stop();
        Enter(second, "LIST", ComputerHz);

        var screen = Screen(second);
        StringAssert.Contains(screen, "FOUND X");
        StringAssert.Contains(screen, "10 PRINT");
        StringAssert.Contains(screen, "HELLO FROM TAPE");
    }
}

// A tape built by hand in the KERNAL's own format, to check the read side alone
[TestClass]
public sealed class DatasetteSyntheticTape
{
    const long ComputerHz = 985_248;
    const int S = 0x30 * 8, M = 0x42 * 8, L = 0x56 * 8;

    static void Byte(TapImage t, int value, bool last)
    {
        t.Append(L); t.Append(M);                                    // new byte marker
        int ones = 0;
        for (int i = 0; i < 8; i++)
        {
            bool bit = (value >> i & 1) != 0;
            if (bit) ones++;
            if (bit) { t.Append(M); t.Append(S); } else { t.Append(S); t.Append(M); }
        }
        bool parity = ones % 2 == 0;                                   // odd parity over the 9 bits
        if (parity) { t.Append(M); t.Append(S); } else { t.Append(S); t.Append(M); }
        if (last) { t.Append(L); t.Append(S); }                         // end of data marker
    }

    static void Block(TapImage t, byte[] data, int leader)
    {
        for (int copy = 0; copy < 2; copy++)
        {
            if (copy == 0) for (int i = 0; i < leader; i++) t.Append(S);
            else for (int i = 0; i < 80; i++) t.Append(S);
            int sync = copy == 0 ? 0x89 : 0x09;
            for (int i = 0; i < 9; i++) Byte(t, sync - i, false);
            int check = 0;
            foreach (var b in data) { Byte(t, b, false); check ^= b; }
            Byte(t, check, true);
        }
    }

    public static TapImage Build(string name, byte[] program, ushort start)
    {
        var tape = new TapImage();
        var header = new byte[192];
        Array.Fill(header, (byte)0x20);
        header[0] = 0x01;
        header[1] = (byte)(start & 0xFF); header[2] = (byte)(start >> 8);
        ushort end = (ushort)(start + program.Length);
        header[3] = (byte)(end & 0xFF); header[4] = (byte)(end >> 8);
        for (int i = 0; i < name.Length; i++) header[5 + i] = (byte)name[i];
        Block(tape, header, 2500);
        Block(tape, program, 1200);
        return tape;
    }

    static byte[] Rom(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "roms", name));

    [TestMethod]
    [Timeout(900_000)]
    public void Load_FromATapeBuiltByHand_ShouldFindTheFile()
    {
        // 10 PRINT"HELLO FROM TAPE"
        byte[] program = [0x1D, 0x08, 0x0A, 0x00, 0x99, 0x22, 0x48, 0x45, 0x4C, 0x4C, 0x4F, 0x20, 0x46, 0x52, 0x4F, 0x4D, 0x20, 0x54, 0x41, 0x50, 0x45, 0x22, 0x00, 0x00, 0x00];
        var tape = Build("X", program, 0x0801);
        var rec = new Datasette { Tape = tape };
        var board = new Board(new SystemRoms(Rom("c-64-kernal.rom"), Rom("c-64-basic.rom"), Rom("c-64-chargen.rom")), startClock: false);
        rec.Plug(board.Cassette);
        board.AfterCycle = rec.Tick;
        board.RunCycles(3 * ComputerHz);
        foreach (var ch in "LOAD\r") { board.TypeText(ch.ToString()); board.RunCycles(ComputerHz / 5); }
        board.RunCycles(2 * ComputerHz);
        rec.Play();
        board.RunCycles(60 * ComputerHz);
        var text = new System.Text.StringBuilder();
        for (int row = 0; row < 25; row++)
        {
            for (int column = 0; column < 40; column++)
            {
                byte code = (byte)(board.Address((ushort)(0x0400 + row * 40 + column)) & 0x7F);
                text.Append(code is >= 1 and <= 26 ? (char)('A' + code - 1) : code is >= 0x20 and < 0x60 ? (char)code : '.');
            }
            text.AppendLine();
        }
        StringAssert.Contains(text.ToString(), "FOUND X");
    }
}

