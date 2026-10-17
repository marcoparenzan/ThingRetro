namespace Thing65.Tests;

// The C65 board, with a ROM made for the test: every byte of physical address A holds Marker(A), a value that tells
// which 4 KB of the ROM it comes from, so that the memory map can be checked without the real ROM
[TestClass]
public sealed class BoardC65Tests
{
    static byte Marker(int physical) => (byte)(0x80 + ((physical >> 12) & 0x1F));

    static BoardC65 NewBoard(bool withCpu = true)
    {
        var image = new byte[SystemRom128.SIZE];
        for (int i = 0; i < image.Length; i++) image[i] = Marker(0x20000 + i);
        image[0xFFFC] = 0x00; // reset vector in the C64 KERNAL area: $E000
        image[0xFFFD] = 0xE0;
        var board = new BoardC65(new SystemRom128(image), startClock: false);
        if (!withCpu) board.CpuSocket.Remove(); // the test drives the bus itself
        return board;
    }

    static void EnterVic3Mode(BoardC65 board)
    {
        board.CpuWrite(0xD02F, 0xA5);
        board.CpuWrite(0xD02F, 0x96);
    }

    static void SetPort(BoardC65 board, int pins)
    {
        board.CpuWrite(0x0000, 0x07);                 // P0-P2 outputs
        board.CpuWrite(0x0001, (byte)pins);
    }

    #region Reset and C64 memory map

    [TestMethod]
    public void Reset_ShouldShowTheKernalAndBasicOfTheC64Mode()
    {
        var board = NewBoard();

        Assert.AreEqual(Marker(0x2E000), board.CpuRead(0xE000)); // C64 KERNAL
        Assert.AreEqual(Marker(0x2A000), board.CpuRead(0xA000)); // C64 BASIC
        board.CpuWrite(0x1000, 0x5A);
        Assert.AreEqual(0x5A, board.CpuRead(0x1000)); // RAM below
    }

    [TestMethod]
    public void Reset_ShouldStartTheCpuFromTheVectorInRom()
    {
        var board = NewBoard();
        var cpu = (MOS4510)board.CpuSocket.Chip!;

        Assert.AreEqual(0xE000, (int)cpu.PC);
    }

    [TestMethod]
    public void Reset_ShouldShowTheIoAtD000()
    {
        var board = NewBoard(withCpu: false);

        board.CpuWrite(0xD020, 0x05);
        board.CpuWrite(0xD800, 0x0B);

        Assert.AreEqual(0x05, board.CpuRead(0xD020) & 0x0F);
        Assert.AreEqual(0x0B, board.Ram(0x1F800), "the Color RAM is the top of the second 64 KB");
    }

    [TestMethod]
    public void Port_ShouldSelectRamKernalBasicAndCharacterRom()
    {
        var board = NewBoard(withCpu: false);
        board.CpuWrite(0xE000, 0x11); // goes under the ROM
        board.CpuWrite(0xA000, 0x22);

        SetPort(board, 0b000); // LORAM = HIRAM = 0: all RAM
        Assert.AreEqual(0x11, board.CpuRead(0xE000));
        Assert.AreEqual(0x22, board.CpuRead(0xA000));

        SetPort(board, 0b010); // HIRAM only: KERNAL, no BASIC
        Assert.AreEqual(Marker(0x2E000), board.CpuRead(0xE000));
        Assert.AreEqual(0x22, board.CpuRead(0xA000));

        SetPort(board, 0b011); // LORAM + HIRAM, CHAREN = 0: BASIC and character ROM instead of I/O
        Assert.AreEqual(Marker(0x2A000), board.CpuRead(0xA000));
        Assert.AreEqual(Marker(0x2D000), board.CpuRead(0xD000));

        SetPort(board, 0b100); // CHAREN only: all RAM, no I/O
        Assert.AreEqual(board.Ram(0xD000), board.CpuRead(0xD000));
    }

    [TestMethod]
    public void WritesToRomAreas_ShouldReachTheRamUnderneath()
    {
        var board = NewBoard(withCpu: false);

        board.CpuWrite(0xE123, 0x55);

        Assert.AreEqual(0x55, board.Ram(0xE123));
        Assert.AreEqual(Marker(0x2E000), board.CpuRead(0xE123)); // still the ROM for reading
    }

    #endregion

    #region VIC-III mode and the ROM bits of control register A

    [TestMethod]
    public void KeyRegister_ShouldNeedA5Then96()
    {
        var board = NewBoard(withCpu: false);

        board.CpuWrite(0xD02F, 0x96);
        board.CpuWrite(0xD02F, 0xA5);
        board.CpuWrite(0xD030, 0x80); // VIC-II mode: no such register
        Assert.IsFalse(board.VicSocket.Chip!.NewMode);
        Assert.AreEqual(0, board.VicSocket.Chip.ControlA);

        EnterVic3Mode(board);
        Assert.IsTrue(board.VicSocket.Chip.NewMode);

        board.CpuWrite(0xD02F, 0x00); // anything else: back to VIC-II mode
        Assert.IsFalse(board.VicSocket.Chip.NewMode);
    }

    [TestMethod]
    public void ControlA_ShouldMapTheRomsOfTheC65Mode()
    {
        var board = NewBoard(withCpu: false);
        EnterVic3Mode(board);

        board.CpuWrite(0xD030, 0x08); // ROM@8000
        Assert.AreEqual(Marker(0x38000), board.CpuRead(0x8000));
        Assert.AreEqual(Marker(0x39000), board.CpuRead(0x9000));

        board.CpuWrite(0xD030, 0x10); // ROM@A000
        Assert.AreEqual(Marker(0x3A000), board.CpuRead(0xA000));

        board.CpuWrite(0xD030, 0x20); // ROM@C000
        Assert.AreEqual(Marker(0x2C000), board.CpuRead(0xC000));

        board.CpuWrite(0xD030, 0x80); // ROM@E000
        Assert.AreEqual(Marker(0x3E000), board.CpuRead(0xE000));
        Assert.AreEqual(Marker(0x3F000), board.CpuRead(0xF000));
    }

    [TestMethod]
    public void RomOverlay_ShouldIgnoreWrites()
    {
        var board = NewBoard(withCpu: false);
        EnterVic3Mode(board);
        board.CpuWrite(0xD030, 0x08);

        board.CpuWrite(0x8000, 0x77);

        Assert.AreEqual(0, board.Ram(0x8000));
    }

    [TestMethod]
    public void ColorRamAtDc00_ShouldReplaceTheCias()
    {
        var board = NewBoard(withCpu: false);
        EnterVic3Mode(board);

        board.CpuWrite(0xD030, 0x01);
        board.CpuWrite(0xDC00, 0x3C);
        Assert.AreEqual(0x3C, board.Ram(0x1FC00));
        Assert.AreEqual(0x3C, board.CpuRead(0xDC00));

        board.CpuWrite(0xD030, 0x00);
        Assert.AreNotEqual(0x3C, board.CpuRead(0xDC00)); // CIA 1 port A again
    }

    [TestMethod]
    public void Palette_ShouldBeWrittenInVic3Mode()
    {
        var board = NewBoard(withCpu: false);
        EnterVic3Mode(board);

        board.CpuWrite(0xD105, 0x0A); // red of color 5
        board.CpuWrite(0xD205, 0x0B); // green
        board.CpuWrite(0xD305, 0x0C); // blue

        var vic = (CSG4567)board.VicSocket.Chip!;
        Assert.AreEqual(0x0A, vic.Palette(0, 5));
        Assert.AreEqual(0x0B, vic.Palette(1, 5));
        Assert.AreEqual(0x0C, vic.Palette(2, 5));
    }

    // A chip that fits the SID socket (same pinout) and records the writes: the sockets accept anything compatible
    sealed class RecordingSid : I6581Pinout
    {
        public readonly Dictionary<int, byte> Written = [];
        public Func<byte> PotXInput { get; set; } = () => 0xFF;
        public Func<byte> PotYInput { get; set; } = () => 0xFF;
        public double AudioOutput => 0;
        public byte Read(int register) => 0xFF;
        public void Write(int register, byte value) => Written[register] = value;
        public void Reset() { }
        public void Tick() { }
    }

    [TestMethod]
    public void TwoSids_ShouldBeAtD400AndD420()
    {
        var board = NewBoard(withCpu: false);
        var right = new RecordingSid();
        var left = new RecordingSid();
        board.SidRightSocket.Remove();
        board.SidRightSocket.Insert(right);
        board.SidLeftSocket.Remove();
        board.SidLeftSocket.Insert(left);
        EnterVic3Mode(board);

        board.CpuWrite(0xD418, 0x0F); // right SID volume
        board.CpuWrite(0xD438, 0x0A); // left SID volume

        Assert.AreEqual(0x0F, right.Written[0x18]);
        Assert.AreEqual(0x0A, left.Written[0x18]);
        Assert.IsFalse(left.Written.ContainsKey(0x00) || right.Written.Count != 1);
    }

    #endregion

    #region 4510 mapper and physical memory

    [TestMethod]
    public void MappedAddresses_ShouldBePhysical()
    {
        var board = NewBoard(withCpu: false);
        var bus = board.Bus;

        Assert.AreEqual(Marker(0x2E000), bus.Read(0x2E000, mapped: true));  // ROM, whatever the CPU port says
        bus.Write(0x2E000, mapped: true, value: 0x99);                      // lost
        Assert.AreEqual(Marker(0x2E000), board.Physical(0x2E000));

        bus.Write(0x1F800, mapped: true, value: 0x44);
        Assert.AreEqual(0x44, board.Ram(0x1F800));
        Assert.AreEqual(0xFF, bus.Read(0x80000, mapped: true));             // expansion: not fitted
    }

    [TestMethod]
    public void CpuPort_ShouldBeAtPhysicalAddress0And1EvenWhenMapped()
    {
        var board = NewBoard(withCpu: false);
        var bus = board.Bus;
        board.CpuWrite(0xE000, 0x33); // under the KERNAL

        bus.Write(0x00000, mapped: true, value: 0x07);
        bus.Write(0x00001, mapped: true, value: 0x00);

        Assert.AreEqual(0x07, bus.Read(0x00000, mapped: true));
        Assert.AreEqual(0x33, board.CpuRead(0xE000)); // LORAM = HIRAM = 0: the KERNAL is gone
    }

    [TestMethod]
    public void CpuWithMap_ShouldSeeTheRomAtADifferentAddress()
    {
        // The C65 way of showing the C64 BASIC at $A000 while ROM@A000 is something else: MAP block 5 (A000-BFFF)
        // to physical $2A000: offset = $2A000 - $A000 = $20000 -> upper offset: Y = $00, Z low nibble = 2
        var board = NewBoard(withCpu: false);
        var cpu = new MOS4510();
        board.CpuSocket.Insert(cpu);
        const ushort program = 0x2000;
        byte[] code = [0xA9, 0x00, 0xA2, 0x00, 0xA0, 0x00, 0xA3, 0x22, 0x5C, 0xEA, 0xAD, 0x00, 0xB0]; // LDA #0 LDX #0 LDY #0 LDZ #$22 MAP EOM LDA $B000
        for (int i = 0; i < code.Length; i++) board.Ram(program + i, code[i]);
        cpu.PC = program;

        for (int i = 0; i < 7; i++)
            do cpu.Tick(); while (!cpu.IsInstructionBoundary);

        Assert.AreEqual(Marker(0x2B000), cpu.A);
    }

    #endregion

    #region DMAgic on the board

    [TestMethod]
    public void Dma_ShouldCopyMemoryThroughTheRegistersAtD700()
    {
        var board = NewBoard(withCpu: false);
        EnterVic3Mode(board);
        for (int i = 0; i < 4; i++) board.Ram(0x0300 + i, (byte)(0x40 + i));
        byte[] list = [0x00, 4, 0, 0x00, 0x03, 0x00, 0x00, 0x04, 0x01, 0x00, 0x00, 0x00]; // copy 4 bytes $0300 -> $10400
        for (int i = 0; i < list.Length; i++) board.Ram(0x0200 + i, list[i]);

        board.CpuWrite(0xD702, 0x00);
        board.CpuWrite(0xD701, 0x02);
        board.CpuWrite(0xD700, 0x00); // trigger
        board.RunCycles(12 + 2 * 4 + 1);

        for (int i = 0; i < 4; i++) Assert.AreEqual(0x40 + i, board.Ram(0x10400 + i));
    }

    [TestMethod]
    public void Dma_ShouldHaltTheCpuWhileItRuns()
    {
        var board = NewBoard();
        EnterVic3Mode(board);
        var cpu = (MOS4510)board.CpuSocket.Chip!;
        byte[] list = [0x03, 0, 0, 0x5A, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00]; // fill 64 KB: long job
        for (int i = 0; i < list.Length; i++) board.Ram(0x0200 + i, list[i]);
        board.CpuWrite(0xD701, 0x02);
        board.CpuWrite(0xD700, 0x00);

        long before = cpu.InstructionCount;
        board.RunCycles(1000);

        Assert.AreEqual(before, cpu.InstructionCount, "the CPU does not run during the job");
    }

    #endregion
}
