using Thing65.C65;

namespace ThingCommodore.Tests;

// A Thing65 computer (in C64 mode) and a 1541 (device 9) on the same IEC cable
[TestClass]
public sealed class ComputerWithDriveTests
{
    const long ComputerHz = 985_248;
    const string CR = "\r";

    static byte[] Rom(string name)
    {
        var file = Path.Combine(AppContext.BaseDirectory, "roms", name);
        if (!File.Exists(file)) Assert.Inconclusive($"ROM missing: {file}");
        return File.ReadAllBytes(file);
    }

    static BoardC65 NewComputer()
    {
        var board = new BoardC65(new SystemRom128(Rom("c-65-19910828.rom")), startClock: false);
        board.RunCycles(9 * ComputerHz);
        return board;
    }

    // The drive on the cable: its cycles follow the computer's
    static Board1541 PlugDrive(BoardC65 computer)
    {
        var drive = new Board1541(Rom("c-drive-1541.rom")) { DeviceNumber = 9 };
        var cable = new IecBus();
        cable.Attach(() => computer.Iec.AtnLow(), () => computer.Iec.ClkLow(), () => computer.Iec.DataLow(),
            input => computer.Iec.ClkInputLow = input, input => computer.Iec.DataInputLow = input);
        drive.Plug(cable);
        computer.AfterCycle = new DriveClock(ComputerHz, drive.Tick).OnComputerCycle;
        return drive;
    }

    static void Type(BoardC65 board, string text, long settleCycles = 600_000)
    {
        board.TypeText(text);
        while (board.IsTyping) board.RunCycles(1000);
        board.RunCycles(settleCycles);
    }

    static void EnterC64Mode(BoardC65 board)
    {
        Type(board, "GO64" + CR, 1_000_000);
        Type(board, "Y" + CR, 6_000_000);
    }

    // The 40-column text screen of the C64 mode: characters at $0400
    static string Screen(BoardC65 board)
    {
        var text = new System.Text.StringBuilder();
        for (int row = 0; row < 25; row++)
        {
            for (int column = 0; column < 40; column++)
            {
                byte code = board.Ram(0x0400 + row * 40 + column);
                text.Append(code is >= 1 and <= 26 ? (char)('A' + code - 1) : code is >= 0x20 and < 0x60 ? (char)code : '.');
            }
            text.AppendLine();
        }
        return text.ToString();
    }

    // LISTEN device, SECOND $6F, U\nISTEN with the routines of the C64 KERNAL's serial bus, then the serial status byte ($90):
    // 0 = the device answered, $80 = device not present. They are called directly: through the jump table ($FFB1...) the C65 ROM
    // first sorts out its own drive
    static int ListenStatus(BoardC65 computer, int device)
    {
        byte[] program =
        [
            0xA9, (byte)device,       // LDA #device
            0x20, 0x0C, 0xED,         // JSR $ED0C LISTEN
            0xA9, 0x6F,               // LDA #$6F
            0x20, 0xB9, 0xED,         // JSR $EDB9 SECOND
            0x20, 0xFE, 0xED,         // JSR $EDFE U\nSN
            0x60,                     // RTS
        ];
        for (int i = 0; i < program.Length; i++) computer.Ram(0xC000 + i, program[i]);
        computer.Ram(0x90, 0);

        Type(computer, "SYS49152" + CR, 3_000_000);
        return computer.Ram(0x90);
    }

    [TestMethod]
    [Timeout(180_000)]
    public void WithoutADrive_Listen_ShouldReportDeviceNotPresent()
    {
        var computer = NewComputer();
        EnterC64Mode(computer);

        Assert.AreEqual(0x80, ListenStatus(computer, 9));
    }

    [TestMethod]
    [Timeout(180_000)]
    public void WithADriveOnTheCable_Listen_ShouldBeAnsweredByTheDrive()
    {
        var computer = NewComputer();
        PlugDrive(computer);
        EnterC64Mode(computer);

        Assert.AreEqual(0, ListenStatus(computer, 9));
    }

    [TestMethod]
    [Timeout(180_000)]
    public void WithADrive_Listen_ForAnotherDevice_ShouldReportDeviceNotPresent()
    {
        var computer = NewComputer();
        PlugDrive(computer);
        EnterC64Mode(computer);

        Assert.AreEqual(0x80, ListenStatus(computer, 10) & 0x80, "not present (with the time-out bits)");
    }

    // Reads a file from the drive the way the KERNAL's LOAD does it, with the routines of the serial bus: LISTEN, OPEN on channel 0,
    // the name, U\nISTEN, TALK, the data with ACPTR until the status says end of file. The bytes go to $C100 and following, the end is left in $C0FE/$C0FF
    static byte[] LoadFile(BoardC65 computer, int device, string? name, long cycles = 25_000_000, byte secondary = 0x60)
    {
        var code = new List<byte>();
        if (name is not null)
        {
            code.AddRange([0xA9, (byte)device, 0x20, 0x0C, 0xED, 0xA9, 0xF0, 0x20, 0xB9, 0xED]);              // LISTEN, SECOND $F0
            foreach (char c in name) code.AddRange([0xA9, (byte)c, 0x20, 0xDD, 0xED]);                       // CIOUT per character
            code.AddRange([0x20, 0xFE, 0xED]);                                                               // U\nSN
        }
        code.AddRange([0xA9, (byte)device, 0x20, 0x09, 0xED, 0xA9, secondary, 0x20, 0xC7, 0xED]);           // TALK, TKSA
        code.AddRange([0xA9, 0x00, 0x85, 0xFB, 0xA9, 0xC1, 0x85, 0xFC, 0xA0, 0x00]);                         // ($FB) = $C100; LDY #0
        code.AddRange([0x20, 0x13, 0xEE, 0x91, 0xFB, 0xC8, 0xD0, 0x02, 0xE6, 0xFC, 0xA5, 0x90, 0xF0, 0xF2]); // loop: ACPTR; STA ($FB),Y; INY; BNE +2; INC $FC; LDA $90; BEQ loop
        code.AddRange([0x20, 0xEF, 0xED, 0x8C, 0xFE, 0xC0, 0xA5, 0xFC, 0x8D, 0xFF, 0xC0, 0xA9, 0xAA, 0x85, 0xFE, 0x60]); // UNTLK; STY $C0FE; page to $C0FF; marker; RTS
        for (int i = 0; i < code.Count; i++) computer.Ram(0xC000 + i, code[i]);
        computer.Ram(0x90, 0);

        RunProgram(computer, cycles);

        int count = (computer.Ram(0xC0FF) - 0xC1) * 256 + computer.Ram(0xC0FE);
        return Enumerable.Range(0, count).Select(i => computer.Ram(0xC100 + i)).ToArray();
    }

    [TestMethod]
    [Timeout(300_000)]
    public void Drive_ShouldSendTheDirectoryOfTheDisk()
    {
        var computer = NewComputer();
        var drive = PlugDrive(computer);
        drive.Disk = D64Writer.Create("TEST DISK", [("HELLO", new byte[] { 0x01, 0x08, 0x00, 0x00 })]);
        EnterC64Mode(computer);

        var directory = LoadFile(computer, 9, "$");

        string text = new(directory.Select(b => (char)b).ToArray());
        StringAssert.Contains(text, "TEST DISK");
        StringAssert.Contains(text, "HELLO");
        Assert.IsTrue(Math.Abs(drive.Mechanism.HalfTrack - 34) <= 2, $"the head is at track 18 (half track 34), not {drive.Mechanism.HalfTrack}");
    }

    [TestMethod]
    [Timeout(300_000)]
    public void Drive_ShouldSendAProgramOverSeveralSectors()
    {
        var computer = NewComputer();
        var drive = PlugDrive(computer);
        var program = new byte[600];                       // three sectors of the disk
        program[0] = 0x01;
        program[1] = 0x08;
        for (int i = 2; i < program.Length; i++) program[i] = (byte)(i * 7);
        drive.Disk = D64Writer.Create("PROGRAMS", [("BIG", program)]);
        EnterC64Mode(computer);

        var received = LoadFile(computer, 9, "BIG");

        Assert.AreEqual(program.Length, received.Length);
        CollectionAssert.AreEqual(program, received.Take(program.Length).ToArray());
    }

    // Writes a file to the drive: OPEN channel 1 with the name, the bytes, CLOSE. LISTEN, SECOND and U\nISTEN are the KERNAL's; the
    // bytes go out through our own routine, with the clock high for longer than the C65's C64 mode KERNAL leaves it (20 cycles,
    // against 26 on a C64: the 1541 polls the clock about every 20 cycles, so it would sometimes miss a bit)
    // LISTEN, SECOND, the bytes, U\nISTEN, in the order given (secondary address and bytes); then wait
    static void SendSteps(BoardC65 computer, int device, long cycles, params (int Secondary, byte[] Bytes)[] steps)
    {
        var source = new System.Text.StringBuilder("* = $C000\n");
        foreach (var (secondary, bytes) in steps)
        {
            source.Append($"lda #{device}\njsr $ED0C\nlda #${secondary:X2}\njsr $EDB9\n");
            for (int i = 0; i < bytes.Length; i++)
                source.Append($"lda #${bytes[i]:X2}{(i == bytes.Length - 1 ? "\njsr sendeoi\n" : "\njsr send\n")}");
            foreach (byte b in Array.Empty<byte>()) source.Append($"lda #${b:X2}\njsr send\n");
            source.Append("jsr $EDFE\n");
        }
        source.Append("lda #$AA\nsta $FE\nrts\n");   // the marker RunProgram waits for
        source.Append(SendRoutine);
        var program = Thing65.Asm.Assembler.Assemble(source.ToString());
        for (int i = 0; i < program.Bytes.Length; i++) computer.Ram(program.Origin + i, program.Bytes[i]);

        RunProgram(computer, cycles);
    }

    // SYS 49152, then run until the program says it is finished ($FE = $AA) or the time is up (the drive may keep it waiting
    // for a long time: a CLOSE writes the directory and the BAM)
    static void RunProgram(BoardC65 computer, long cycles)
    {
        computer.Ram(0xFE, 0);
        computer.TypeText("SYS49152" + CR);
        while (computer.IsTyping) computer.RunCycles(1000);
        for (long done = 0; done < cycles && computer.Ram(0xFE) != 0xAA; done += 100_000) computer.RunCycles(100_000);
        Assert.AreEqual(0xAA, computer.Ram(0xFE), $"the program did not finish: computer at {((Thing65.Mos.MOS4510)computer.CpuSocket.Chip!).DebugInfo.Item1:X4}, ST={computer.Ram(0x90):X2}, lines atn={computer.Iec.AtnLow()} clk={computer.Iec.ClkInputLow()} data={computer.Iec.DataInputLow()}");
        computer.RunCycles(300_000);   // back to BASIC
    }

    static byte[] Ascii(string text) => System.Text.Encoding.ASCII.GetBytes(text);

    // Writes a file to the drive: OPEN channel 1 with the name, the bytes, CLOSE
    static void SaveFile(BoardC65 computer, int device, string nameWithType, byte[] content, long cycles = 40_000_000) =>
        SendSteps(computer, device, cycles, (0xF1, Ascii(nameWithType)), (0x61, content), (0xE1, []));

    // The byte in A on the serial bus, after the KERNAL's routine at $ED40 but slower; $90 gets the timeout bit
    const string SendRoutine = """
        sendeoi: ldx #1                    ; the last byte of a command or name is sent with EOI
                stx $FA
                jsr send
                ldx #0
                stx $FA
                rts
        send:   sta $95
                sei
                jsr $EE97          ; data out high
                jsr $EEA9
                bcs fail           ; data high: nobody there
                jsr $EE85          ; clock out high: ready to send
        ready:  jsr $EEA9
                bcc ready          ; wait for the listener (it may be busy for a long time)
                lda $FA
                beq go
        eoi:    jsr $EEA9          ; EOI: we wait, and the listener answers by pulling data low
                bcs eoi
        again:  jsr $EEA9
                bcc again          ; and releasing it
        go:
                jsr $EE8E          ; clock low
                lda #8
                sta $A5
        bit1:   lda $DD00
                cmp $DD00
                bne bit1
                asl
                bcc fail
                ror $95
                bcs one
                jsr $EEA0          ; data low
                bne clk
        one:    jsr $EE97          ; data high
        clk:    jsr $EE85          ; clock high: the bit is valid
                ldx #20
        hold:   dex
                bne hold
                lda $DD00
                and #$DF           ; data high
                ora #$10           ; clock low
                sta $DD00
                ldx #20
        low:    dex
                bne low
                dec $A5
                bne bit1
        ack:    jsr $EEA9          ; the listener pulls data low
                bcs ack
                cli
                rts
        fail:   lda $90
                ora #$03
                sta $90
                cli
                rts
        """;

    [TestMethod]
    [Timeout(600_000)]
    public void Drive_ShouldSaveAFileThatItCanLoadBack()
    {
        var computer = NewComputer();
        var drive = PlugDrive(computer);
        drive.Disk = D64Writer.Create("SAVE TEST", [("OLD", new byte[] { 1, 8, 9, 9 })]);
        EnterC64Mode(computer);
        var program = new byte[] { 0x01, 0x08, 0x0B, 0x08, 0x0A, 0x00, 0x99, 0x22, 0x48, 0x49, 0x22, 0x00, 0x00, 0x00 };

        SaveFile(computer, 9, "WROTE,P,W", program);

        Assert.IsTrue(drive.Disk!.Modified, $"the disk was written ({drive.Mechanism.BytesWritten} bytes)");
        CollectionAssert.AreEqual(program, LoadFile(computer, 9, "WROTE"));
        var directory = new string(LoadFile(computer, 9, "$").Select(b => (char)b).ToArray());
        StringAssert.Contains(directory, "WROTE");
        StringAssert.Contains(directory, "OLD");
    }

    [TestMethod]
    [Timeout(600_000)]
    public void Drive_ShouldScratchAFile()
    {
        var computer = NewComputer();
        var drive = PlugDrive(computer);
        drive.Disk = D64Writer.Create("SCRATCH", [("KEEP", new byte[] { 1, 8, 1 }), ("GONE", new byte[] { 1, 8, 2 })]);
        EnterC64Mode(computer);

        SendSteps(computer, 9, 40_000_000, (0x6F, Ascii("S:GONE")));

        string status = new(LoadFile(computer, 9, null, 250_000_000, 0x6F).Select(b => (char)b).ToArray());
        StringAssert.StartsWith(status, "01, FILES SCRATCHED");
        string directory = new(LoadFile(computer, 9, "$").Select(b => (char)b).ToArray());
        StringAssert.Contains(directory, "KEEP");
        Assert.IsFalse(directory.Contains("GONE"), directory);
    }

    // The NEW command: every track is written with its sectors, the BAM and an empty directory. A slow test (35 tracks)
    [TestMethod]
    [Timeout(900_000)]
    public void Drive_ShouldFormatADisk()
    {
        var computer = NewComputer();
        var drive = PlugDrive(computer);
        drive.Disk = D64Image.Blank();
        EnterC64Mode(computer);

        SendSteps(computer, 9, 5_000_000, (0x6F, Ascii("N:FORMATTED,AB")));

        // The drive keeps formatting after the command: asking for the status waits until it has finished
        string status = new(LoadFile(computer, 9, null, 250_000_000, 0x6F).Select(b => (char)b).ToArray());
        string directory = new(LoadFile(computer, 9, "$", 60_000_000).Select(b => b is >= 0x20 and < 0x7F ? (char)b : '.').ToArray());
        Assert.IsTrue(drive.Disk.Modified, $"nothing was written; status '{status}'");
        StringAssert.StartsWith(status, "00, OK", status);
        StringAssert.Contains(directory, "FORMATTED");
        Assert.AreEqual((byte)'A', drive.Disk.Id.Id1);
        Assert.AreEqual((byte)'B', drive.Disk.Id.Id2);
        Assert.AreEqual(664, FreeBlocks(drive.Disk), "35 tracks without the directory track's two sectors");
    }

    // Free blocks in the BAM, without the directory track (as the directory listing counts them)
    static int FreeBlocks(D64Image disk)
    {
        var bam = new byte[256];
        disk.ReadSector(18, 0, bam);
        return Enumerable.Range(1, 35).Where(track => track != 18).Sum(track => bam[4 * track]);
    }
}
