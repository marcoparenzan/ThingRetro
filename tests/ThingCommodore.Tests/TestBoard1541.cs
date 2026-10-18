namespace ThingCommodore.Tests;

[TestClass]
public sealed class Board1541Tests
{
    static readonly string RomFile = Path.Combine(AppContext.BaseDirectory, "roms", "c-drive-1541.rom");

    static byte[] LoadRom()
    {
        if (!File.Exists(RomFile)) Assert.Inconclusive($"ROM missing: {RomFile}");
        return File.ReadAllBytes(RomFile);
    }

    // A ROM of NOPs with the reset vector at $C000 and a program, to test the board without the DOS
    static byte[] ProgramRom(params byte[] program)
    {
        var rom = new byte[Board1541.ROM_SIZE];
        Array.Fill(rom, (byte)0xEA);
        program.CopyTo(rom, 0);
        rom[0x3FFC] = 0x00;
        rom[0x3FFD] = 0xC0;
        return rom;
    }

    static void Run(Board1541 board, int cycles)
    {
        for (int i = 0; i < cycles; i++) board.Tick();
    }

    [TestMethod]
    public void Memory_ShouldMirrorTheRamAndTheRom()
    {
        var board = new Board1541(ProgramRom(0xA9, 0x5A, 0x8D, 0x34, 0x12)); // LDA #$5A; STA $1234
        Run(board, 12);

        Assert.AreEqual(0x5A, board.Ram(0x1234), "$1234 is $0234 of the RAM");
        Assert.AreEqual(0xA9, board.Read(0xC000));
        Assert.AreEqual(0xA9, board.Read(0x8000), "the ROM is seen twice");
    }

    [TestMethod]
    public void Rom_WithTheWrongSize_ShouldBeRefused()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new Board1541(new byte[100]));
    }

    [TestMethod]
    public void Via1_ShouldPullDataDownThroughPb1AndReleaseIt()
    {
        var cable = new IecBus();
        var board = new Board1541(ProgramRom(
            0xA9, 0x02, 0x8D, 0x02, 0x18,   // DDRB = PB1 output
            0x8D, 0x00, 0x18,               // ORB = 2: DATA down
            0xA9, 0x00, 0x8D, 0x00, 0x18)); // ORB = 0: DATA up
        board.Plug(cable);

        Run(board, 15);
        Assert.IsTrue(cable.Data.IsLow);
        Run(board, 10);
        Assert.IsFalse(cable.Data.IsLow);
    }

    [TestMethod]
    public void AttentionGate_ShouldPullDataDownUntilTheDriveAcknowledges()
    {
        var cable = new IecBus();
        bool computerAtn = false;
        cable.Atn.Connect(() => computerAtn);
        var board = new Board1541(ProgramRom(
            0xA9, 0x12, 0x8D, 0x02, 0x18,   // DDRB: PB1 and PB4 outputs
            0xA9, 0x10, 0x8D, 0x00, 0x18)); // ATNA = 1 (acknowledged state with ATN high would pull)
        board.Plug(cable);
        Run(board, 5);
        Assert.IsFalse(cable.Data.IsLow, "idle: ATN high, ATNA low");

        computerAtn = true;

        Assert.IsTrue(cable.Data.IsLow, "attention: the gate pulls DATA down");
    }

    [TestMethod]
    public void Via1_ShouldReadTheLinesInvertedAndInterruptOnAtn()
    {
        var cable = new IecBus();
        bool computerAtn = false, computerClk = false;
        cable.Atn.Connect(() => computerAtn);
        cable.Clk.Connect(() => computerClk);
        var board = new Board1541(ProgramRom(
            0xA9, 0x82, 0x8D, 0x0E, 0x18,   // IER: CA1 enabled
            0xA9, 0x01, 0x8D, 0x0C, 0x18,   // PCR: CA1 on the rising edge (ATN down)
            0xAD, 0x00, 0x18, 0x85, 0x10,   // LDA ORB; STA $10
            0x4C, 0x0A, 0xC0));             // loop
        board.Plug(cable);
        Run(board, 40);
        Assert.AreEqual(0x00, board.Ram(0x10) & 0x85, "all lines high: reads 0");

        computerClk = true;
        Run(board, 10);
        Assert.AreEqual(0x04, board.Ram(0x10) & 0x85);
        computerAtn = true;
        Run(board, 10);
        Assert.AreEqual(0x85, board.Ram(0x10) & 0x85, "ATN down, and the gate pulls DATA down too");
    }

    [TestMethod]
    public void Dos_ShouldBootAndWaitForTheComputer()
    {
        var board = new Board1541(LoadRom());
        var cable = new IecBus();
        board.Plug(cable);

        Run(board, 3_000_000);   // the self test and the initialization

        Assert.IsFalse(cable.Atn.IsLow || cable.Clk.IsLow, "idle on the bus");
    }
}
