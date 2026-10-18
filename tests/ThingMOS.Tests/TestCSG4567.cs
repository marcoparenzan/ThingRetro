namespace ThingMOS.Tests;

// The VIC-III on its own, with a memory made for the test. A frame is 312 lines of 63 cycles; the pixels are
// "dots" of half a VIC-II pixel and every raster line gives two lines of the frame
[TestClass]
public sealed class CSG4567Tests
{
    const int CyclesPerFrame = 312 * 63;
    const int FirstTextLine = 0x33;          // YSCROLL = 3 below
    const int DisplayLeft = 84;              // dots
    static int FrameRow(int rasterLine) => 2 * (rasterLine - 9);

    sealed class Memory : IVic3Memory
    {
        public readonly byte[] Bank0 = new byte[0x4000]; // what the VIC-II sees at the bank's address
        public readonly byte[] Ram = new byte[0x20000];
        public readonly byte[] Color = new byte[0x800];

        public int Bank { get; set; }
        public byte Read(int address) => Bank0[address & 0x3FFF];
        public byte ReadColor(int address) => Color[address & 0x7FF];
        public byte ReadRam(int address) => Ram[address & 0x1FFFF];
        public void WriteRam(int address, byte value) => Ram[address & 0x1FFFF] = value;
    }

    static (CSG4567 Vic, Memory Memory) NewVic()
    {
        var memory = new Memory();
        var vic = new CSG4567 { Memory = memory };
        return (vic, memory);
    }

    static void Enter(CSG4567 vic)
    {
        vic.Write(0x2F, 0xA5);
        vic.Write(0x2F, 0x96);
    }

    // Text display on: DEN, 25 rows, YSCROLL 3, 40 columns; screen at $0400 (or the 2K matrix of H640), characters at $1000
    static void TextOn(CSG4567 vic)
    {
        vic.Write(0x11, 0x1B);
        vic.Write(0x16, 0x08);
        vic.Write(0x18, 0x14);
    }

    static void RunFrames(CSG4567 vic, int frames = 2)
    {
        for (int i = 0; i < frames * CyclesPerFrame; i++) vic.Tick();
    }

    #region VIC-II mode

    [TestMethod]
    public void BorderAndBackground_ShouldUseTheirColors()
    {
        var (vic, memory) = NewVic();
        TextOn(vic);

        RunFrames(vic);

        Assert.AreEqual(6, vic.PixelAt(DisplayLeft + 100, FrameRow(FirstTextLine) + 10)); // blue background
        Assert.AreEqual(14, vic.PixelAt(10, FrameRow(FirstTextLine) + 10));               // light blue border
        Assert.AreEqual(14, vic.PixelAt(400, FrameRow(20)));                              // above the display
    }

    [TestMethod]
    public void Text40Columns_ShouldDrawTwoDotsPerPixel()
    {
        var (vic, memory) = NewVic();
        TextOn(vic);
        memory.Bank0[0x0400] = 1;                         // screen code 1 in the first cell
        for (int i = 0; i < 8; i++) memory.Bank0[0x1008 + i] = 0b10101010; // character 1: vertical bars
        memory.Color[0] = 1;                              // white

        RunFrames(vic);

        int row = FrameRow(FirstTextLine);
        Assert.AreEqual(1, vic.PixelAt(DisplayLeft + 0, row));
        Assert.AreEqual(1, vic.PixelAt(DisplayLeft + 1, row), "a pixel is 2 dots");
        Assert.AreEqual(6, vic.PixelAt(DisplayLeft + 2, row));
        Assert.AreEqual(1, vic.PixelAt(DisplayLeft + 4, row));
        Assert.AreEqual(vic.PixelAt(DisplayLeft, row), vic.PixelAt(DisplayLeft, row + 1), "the two lines of the frame are the same");
    }

    [TestMethod]
    public void RegistersBeyondTheVic2_ShouldNotExistInVic2Mode()
    {
        var (vic, _) = NewVic();

        vic.Write(0x30, 0x80);
        Assert.AreEqual(0xFF, vic.Read(0x30));
        Assert.AreEqual(0, vic.ControlA);

        // The 64 registers repeat in $D000-$D3FF
        vic.Write(0x20 + 0x40, 0x03);
        Assert.AreEqual(0x03, vic.Read(0x20));
    }

    #endregion

    #region VIC-III mode

    [TestMethod]
    public void KeyRegister_ShouldNeedA5Then96()
    {
        var (vic, _) = NewVic();

        vic.Write(0x2F, 0x96);
        vic.Write(0x2F, 0xA5);
        Assert.IsFalse(vic.NewMode);

        Enter(vic);
        Assert.IsTrue(vic.NewMode);
        vic.Write(0x30, 0x80);
        Assert.AreEqual(0x80, vic.ControlA);

        vic.Write(0x2F, 0x00);
        Assert.IsFalse(vic.NewMode);
        Assert.AreEqual(0x80, vic.ControlA, "the registers keep their values");
    }

    [TestMethod]
    public void Text80Columns_ShouldDrawOneDotPerPixelAndUseThe2KMatrix()
    {
        var (vic, memory) = NewVic();
        Enter(vic);
        TextOn(vic);
        vic.Write(0x31, 0x80);                            // H640
        vic.Write(0x18, 0x24);                            // 2K matrix at $0800 (bit 4 ignored), characters at $1000
        memory.Bank0[0x0800] = 1;                         // cell 0
        memory.Bank0[0x0801] = 1;                         // cell 1: the second column, 8 dots to the right
        for (int i = 0; i < 8; i++) memory.Bank0[0x1008 + i] = 0b10100000;
        memory.Color[0] = 1;
        memory.Color[1] = 2;

        RunFrames(vic);

        int row = FrameRow(FirstTextLine);
        Assert.AreEqual(1, vic.PixelAt(DisplayLeft + 0, row));
        Assert.AreEqual(6, vic.PixelAt(DisplayLeft + 1, row), "a pixel is 1 dot");
        Assert.AreEqual(1, vic.PixelAt(DisplayLeft + 2, row));
        Assert.AreEqual(2, vic.PixelAt(DisplayLeft + 8, row), "the color of the second column comes from the 2K Color RAM");
    }

    [TestMethod]
    public void Attributes_ShouldReverseHighlightAndUnderline()
    {
        var (vic, memory) = NewVic();
        Enter(vic);
        TextOn(vic);
        vic.Write(0x31, 0x20);                            // ATTR
        memory.Bank0[0x0400] = 1;                         // plain
        memory.Bank0[0x0401] = 1;                         // reverse
        memory.Bank0[0x0402] = 1;                         // highlight
        memory.Bank0[0x0403] = 1;                         // underline
        for (int i = 0; i < 8; i++) memory.Bank0[0x1008 + i] = 0b11110000;
        memory.Color[0] = 0x01;
        memory.Color[1] = 0x21;                           // reverse
        memory.Color[2] = 0x41;                           // highlight: color + 16
        memory.Color[3] = 0x81;                           // underline

        RunFrames(vic);

        int row = FrameRow(FirstTextLine);
        Assert.AreEqual(1, vic.PixelAt(DisplayLeft + 0, row));
        Assert.AreEqual(6, vic.PixelAt(DisplayLeft + 14, row));
        Assert.AreEqual(6, vic.PixelAt(DisplayLeft + 16 + 0, row), "reverse: the background where the character was");
        Assert.AreEqual(1, vic.PixelAt(DisplayLeft + 16 + 14, row));
        Assert.AreEqual(17, vic.PixelAt(DisplayLeft + 32 + 0, row), "highlight adds 16 to the color");
        Assert.AreEqual(22, vic.PixelAt(DisplayLeft + 32 + 14, row), "and to the background");
        Assert.AreEqual(6, vic.PixelAt(DisplayLeft + 48 + 14, row));
        Assert.AreEqual(1, vic.PixelAt(DisplayLeft + 48 + 14, FrameRow(FirstTextLine + 7)), "underline on the eighth line");
    }

    [TestMethod]
    public void Bitplanes_ShouldFormThePaletteIndex()
    {
        var (vic, memory) = NewVic();
        Enter(vic);
        TextOn(vic);
        vic.Write(0x31, 0x10);                            // BPM
        vic.Write(0x32, 0x07);                            // planes 0, 1, 2
        vic.Write(0x33, 0x02);                            // plane 0 at $2000 (bank 0)
        vic.Write(0x34, 0x04);                            // plane 1 at $4000 (bank 1: $14000)
        vic.Write(0x35, 0x06);                            // plane 2 at $6000
        memory.Ram[0x2000] = 0b10000000;                  // first pixel: plane 0
        memory.Ram[0x14000] = 0b11000000;                 // first two pixels: plane 1
        memory.Ram[0x6000] = 0b01000000;                  // second pixel: plane 2

        RunFrames(vic);

        int row = FrameRow(FirstTextLine);
        Assert.AreEqual(1 | 2, vic.PixelAt(DisplayLeft + 0, row));          // planes 0 + 1
        Assert.AreEqual(1 | 2, vic.PixelAt(DisplayLeft + 1, row), "320 mode: 2 dots per pixel");
        Assert.AreEqual(2 | 4, vic.PixelAt(DisplayLeft + 2, row));          // planes 1 + 2
        Assert.AreEqual(0, vic.PixelAt(DisplayLeft + 4, row));
    }

    [TestMethod]
    public void BitplaneComplement_ShouldContributeAOneEvenIfNotEnabled()
    {
        var (vic, _) = NewVic();
        Enter(vic);
        TextOn(vic);
        vic.Write(0x31, 0x10);
        vic.Write(0x32, 0x01);
        vic.Write(0x3B, 0x80);                            // plane 7 complemented, not enabled

        RunFrames(vic);

        Assert.AreEqual(0x80, vic.PixelAt(DisplayLeft + 4, FrameRow(FirstTextLine)));
    }

    [TestMethod]
    public void Bitplanes640_ShouldUseOneDotPerPixelAnd80Columns()
    {
        var (vic, memory) = NewVic();
        Enter(vic);
        TextOn(vic);
        vic.Write(0x31, 0x90);                            // BPM + H640
        vic.Write(0x32, 0x01);
        vic.Write(0x33, 0x02);                            // $2000 -> the 16 KB block: $0000
        memory.Ram[0x0000 + 8 * 1] = 0b10100000;          // second column (cells of 8 bytes)

        RunFrames(vic);

        int row = FrameRow(FirstTextLine);
        Assert.AreEqual(1, vic.PixelAt(DisplayLeft + 8, row));
        Assert.AreEqual(0, vic.PixelAt(DisplayLeft + 9, row));
        Assert.AreEqual(1, vic.PixelAt(DisplayLeft + 10, row));
    }

    [TestMethod]
    public void Bitplanes400Lines_ShouldShowTheOddScanInTheSecondLine()
    {
        var (vic, memory) = NewVic();
        Enter(vic);
        TextOn(vic);
        vic.Write(0x31, 0x19);                            // BPM + V400 + INT
        vic.Write(0x32, 0x01);
        vic.Write(0x33, 0x42);                            // even scan: block $2000, odd scan: block $4000
        memory.Ram[0x2000] = 0b10000000;
        memory.Ram[0x4000] = 0b01000000;

        RunFrames(vic);

        int row = FrameRow(FirstTextLine);
        Assert.AreEqual(1, vic.PixelAt(DisplayLeft + 0, row));
        Assert.AreEqual(0, vic.PixelAt(DisplayLeft + 0, row + 1));
        Assert.AreEqual(0, vic.PixelAt(DisplayLeft + 2, row));
        Assert.AreEqual(1, vic.PixelAt(DisplayLeft + 2, row + 1), "the second line comes from the odd scan");
    }

    [TestMethod]
    public void Dat_ShouldReadAndWriteBitplaneBytesByCoordinates()
    {
        var (vic, memory) = NewVic();
        Enter(vic);
        vic.Write(0x33, 0x02);                            // plane 0 at $2000
        vic.Write(0x34, 0x04);                            // plane 1 at $14000

        vic.Write(0x3C, 5);                               // X = 5 bytes
        vic.Write(0x3D, 10);                              // Y = 10 lines (cell row 1, line 2)
        vic.Write(0x40, 0xAA);                            // plane 0
        vic.Write(0x41, 0x55);                            // plane 1

        int offset = (10 >> 3) * 40 * 8 + 5 * 8 + (10 & 7);
        Assert.AreEqual(0xAA, memory.Ram[0x2000 + offset]);
        Assert.AreEqual(0x55, memory.Ram[0x14000 + offset]);
        Assert.AreEqual(0xAA, vic.Read(0x40));
    }

    [TestMethod]
    public void Sprites_ShouldBe2DotsWideAndUseThePalette()
    {
        var (vic, memory) = NewVic();
        Enter(vic);
        TextOn(vic);
        memory.Bank0[0x07F8] = 0x20;                      // sprite 0 data at $0800
        for (int i = 0; i < 63; i++) memory.Bank0[0x0800 + i] = 0xFF;
        vic.Write(0x00, 24 + 8);                          // X
        vic.Write(0x01, 0x40);                            // Y
        vic.Write(0x15, 0x01);                            // enabled
        vic.Write(0x27, 0x85);                            // color: palette index 133 (VIC-III mode: 8 bits)

        RunFrames(vic);

        int row = FrameRow(0x42);
        Assert.AreEqual(133, vic.PixelAt(DisplayLeft + 16, row));
        Assert.AreEqual(133, vic.PixelAt(DisplayLeft + 16 + 47, row), "24 pixels = 48 dots");
        Assert.AreNotEqual(133, vic.PixelAt(DisplayLeft + 16 + 48, row));
    }

    #endregion

    #region Position, bus cycles and 1280 pixels

    [TestMethod]
    public void Hpos_ShouldMoveTheDisplayInDots()
    {
        var (vic, memory) = NewVic();
        Enter(vic);
        TextOn(vic);
        memory.Bank0[0x0400] = 1;
        for (int i = 0; i < 8; i++) memory.Bank0[0x1008 + i] = 0xFF;
        memory.Color[0] = 1;
        vic.Write(0x3E, 12);                              // 12 dots to the right

        RunFrames(vic);

        int row = FrameRow(FirstTextLine);
        Assert.AreEqual(6, vic.PixelAt(DisplayLeft + 10, row), "still the background");
        Assert.AreEqual(1, vic.PixelAt(DisplayLeft + 12, row));
    }

    [TestMethod]
    public void Vpos_ShouldMoveTheDisplayInRasterLines()
    {
        var (vic, memory) = NewVic();
        Enter(vic);
        TextOn(vic);
        memory.Bank0[0x0400] = 1;
        for (int i = 0; i < 8; i++) memory.Bank0[0x1008 + i] = 0xFF;
        memory.Color[0] = 1;
        vic.Write(0x3F, 2);                               // 2 lines down

        RunFrames(vic);

        Assert.AreEqual(6, vic.PixelAt(DisplayLeft, FrameRow(FirstTextLine)));
        Assert.AreEqual(6, vic.PixelAt(DisplayLeft, FrameRow(FirstTextLine + 1)));
        Assert.AreEqual(1, vic.PixelAt(DisplayLeft, FrameRow(FirstTextLine + 2)));
    }

    // Cycles of a frame in which the CPU cannot use the bus (BA low)
    static int StolenCycles(CSG4567 vic)
    {
        int stolen = 0;
        for (int i = 0; i < CyclesPerFrame; i++)
        {
            if (!vic.BusAvailable) stolen++;
            vic.Tick();
        }
        return stolen;
    }

    [TestMethod]
    public void Text80Columns_ShouldTakeMoreCyclesFromTheCpuThanText40()
    {
        var (narrow, _) = NewVic();
        Enter(narrow);
        TextOn(narrow);
        StolenCycles(narrow); // the first frame sets DEN
        int cycles40 = StolenCycles(narrow);

        var (wide, _) = NewVic();
        Enter(wide);
        TextOn(wide);
        wide.Write(0x31, 0x80);
        StolenCycles(wide);
        int cycles80 = StolenCycles(wide);

        Assert.IsTrue(wide.ExtraFetch);
        Assert.IsFalse(narrow.ExtraFetch);
        Assert.IsTrue(cycles40 > 500 && cycles40 < 1500, $"40 columns: {cycles40} (25 bad lines)");
        Assert.IsTrue(cycles80 > 6000, $"80 columns: {cycles80} (the 200 display lines)");
    }

    [TestMethod]
    public void Bitplanes_ShouldCostMoreOnlyBeyondTheViciiFetch()
    {
        var (vic, _) = NewVic();
        Enter(vic);
        vic.Write(0x31, 0x10);                            // BPM, 320 pixels
        vic.Write(0x32, 0x0F);                            // 4 planes: as many as the VIC-II fetches
        Assert.IsFalse(vic.ExtraFetch);
        vic.Write(0x32, 0xFF);                            // 8 planes
        Assert.IsTrue(vic.ExtraFetch);
        vic.Write(0x31, 0x90);                            // 640 pixels: only 4 planes exist, 2 are free
        vic.Write(0x32, 0x03);
        Assert.IsFalse(vic.ExtraFetch);
        vic.Write(0x32, 0x07);
        Assert.IsTrue(vic.ExtraFetch);
    }

    [TestMethod]
    public void Mode1280_ShouldForceBitplane7Low()
    {
        var (vic, _) = NewVic();
        Enter(vic);
        TextOn(vic);
        vic.Write(0x31, 0x90);                            // BPM + H640
        vic.Write(0x3B, 0x80);                            // plane 7 complemented: it would give $80
        RunFrames(vic);
        Assert.AreEqual(0x80, vic.PixelAt(DisplayLeft + 4, FrameRow(FirstTextLine)));

        vic.Write(0x31, 0x94);                            // + H1280
        RunFrames(vic);
        Assert.AreEqual(0x00, vic.PixelAt(DisplayLeft + 4, FrameRow(FirstTextLine)));
    }

    #endregion

    #region Palette

    static unsafe uint[] RenderFrame(CSG4567 vic)
    {
        var pixels = new uint[CSG4567.SCREEN_WIDTH * CSG4567.SCREEN_HEIGHT];
        fixed (uint* p = pixels) vic.Render((byte*)p);
        return pixels;
    }

    [TestMethod]
    public void Render_ShouldUseThePaletteRomForTheFirst16Colors()
    {
        var (vic, _) = NewVic();
        Enter(vic);
        TextOn(vic);
        vic.Write(0x21, 1);                               // white background
        RunFrames(vic);

        var pixels = RenderFrame(vic);

        Assert.AreEqual(0xFFFFFFFFu, pixels[(FrameRow(FirstTextLine) + 10) * CSG4567.SCREEN_WIDTH + DisplayLeft + 100]);
    }

    [TestMethod]
    public void Render_ShouldUseThePaletteRamWhenPalIsSet()
    {
        var (vic, _) = NewVic();
        Enter(vic);
        TextOn(vic);
        vic.Write(0x21, 1);
        vic.WritePalette(0, 1, 0x0A);                     // color 1 = (10, 5, 0)
        vic.WritePalette(1, 1, 0x05);
        vic.WritePalette(2, 1, 0x00);
        RunFrames(vic);

        Assert.AreEqual(0xFFFFFFFFu, RenderFrame(vic)[(FrameRow(FirstTextLine) + 10) * CSG4567.SCREEN_WIDTH + DisplayLeft + 100], "PAL clear: the ROM");

        vic.Write(0x30, 0x04);                            // PAL
        Assert.AreEqual(0xFFAA5500u, RenderFrame(vic)[(FrameRow(FirstTextLine) + 10) * CSG4567.SCREEN_WIDTH + DisplayLeft + 100]);
    }

    [TestMethod]
    public void Fast_ShouldFollowControlRegisterB()
    {
        var (vic, _) = NewVic();
        Enter(vic);

        vic.Write(0x31, 0x40);
        Assert.IsTrue(vic.Fast);
        vic.Write(0x31, 0x00);
        Assert.IsFalse(vic.Fast);
    }

    #endregion
}
