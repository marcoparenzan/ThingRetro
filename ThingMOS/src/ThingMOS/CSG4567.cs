namespace ThingMOS;

// CSG 4567 VIC-III: the C65's video chip. After reset it is a VIC-II (same registers, same timing: the C64 mode of
// the C65); writing $A5 then $96 to the KEY register ($D02F) puts it in "NewVic" mode, where the registers
// $D02F-$D07F, the 256-color palette and the new video modes appear:
//   - 80 columns / 640 pixels per line (H640), 400 lines (V400 with INT), bitplane mode (BPM) with up to 8 planes,
//   - character attributes (blink, reverse, highlight, underline), the ROM-decoding bits of control register A,
//   - the DAT (display address translator) and the FAST bit.
// The picture is made of "dots" of half a VIC-II pixel horizontally, and every raster line gives two lines of the
// frame (a top and a bottom one: they differ only in 400-line bitplane mode): see CSG4567.Graphics.cs.
// The fetch of 80 columns (or of many bitplanes) takes 40 more cycles from the CPU on every display line, as the manual's
// table of cycles per line says. HPOS and VPOS ($D03E/$D03F, signed) move the display relative to the borders, in dots
// and in raster lines. In 1280-pixel mode (H1280 with H640) the second half of every pixel is not drawn: only the "early"
// pixel is (bit plane 7 forced low). Not implemented: external sync.
// Reference: "C65 Specifications", chapter 2.4
public partial class CSG4567 : I4567Pinout
{
    // Video bus and video output, wired by the board
    public IVic3Memory? Memory { get; set; }
    IVicMemory? I6569Pinout.Memory { get => Memory; set => Memory = value as IVic3Memory; }
    public Action? FrameCompleted { get; set; }

    // The VIC-II generates its own timing: raster line and cycle within the line
    int rasterLine;
    int currentCycle;
    int rasterCompare;            // Raster interrupt line (written through $D012 and bit 7 of $D011)
    bool badLinesEnabled;         // DEN ($D011 bit 4) set during line $30 of the current frame
    long frames;                  // for the blink of the attributes

    // $D000-$D07F; the VIC-II uses $00-$2E
    readonly byte[] registers = new byte[0x80];
    readonly byte[][] paletteRam = [new byte[256], new byte[256], new byte[256]]; // red (bit 4 = FG/BG), green, blue

    // Register numbers
    const int REG_SPRITE0_X = 0x00, REG_SPRITE0_Y = 0x01, REG_SPRITE_X_MSB = 0x10, REG_CONTROL_1 = 0x11, REG_RASTER = 0x12,
        REG_SPRITE_ENABLE = 0x15, REG_CONTROL_2 = 0x16, REG_SPRITE_Y_EXPAND = 0x17, REG_MEMORY_POINTERS = 0x18,
        REG_INTERRUPT_STATUS = 0x19, REG_INTERRUPT_ENABLE = 0x1A, REG_SPRITE_PRIORITY = 0x1B, REG_SPRITE_MULTICOLOR = 0x1C,
        REG_SPRITE_X_EXPAND = 0x1D, REG_SPRITE_SPRITE_COLL = 0x1E, REG_SPRITE_DATA_COLL = 0x1F, REG_BORDER_COLOR = 0x20,
        REG_BACKGROUND_COLOR = 0x21, REG_BACKGROUND_COLOR_1 = 0x22, REG_BACKGROUND_COLOR_2 = 0x23,
        REG_BACKGROUND_COLOR_3 = 0x24, REG_SPRITE_MULTICOLOR_0 = 0x25, REG_SPRITE_MULTICOLOR_1 = 0x26,
        REG_SPRITE0_COLOR = 0x27, REG_LAST_VIC2 = 0x2E;

    // VIC-III registers
    const int REG_KEY = 0x2F, REG_CONTROL_A = 0x30, REG_CONTROL_B = 0x31, REG_BITPLANE_ENABLE = 0x32, REG_BITPLANE_ADDRESS = 0x33,
        REG_BITPLANE_COMPLEMENT = 0x3B, REG_BITPLANE_X = 0x3C, REG_BITPLANE_Y = 0x3D, REG_HPOS = 0x3E, REG_VPOS = 0x3F, REG_DAT = 0x40;

    // Control register B
    const int B_H640 = 0x80, B_FAST = 0x40, B_ATTR = 0x20, B_BPM = 0x10, B_V400 = 0x08, B_H1280 = 0x04, B_MONO = 0x02, B_INT = 0x01;
    // Control register A
    const int A_PAL = 0x04;

    const int TOTAL_RASTER_LINES = 312;    // PAL
    const int CYCLES_PER_LINE = 63;

    bool keyAfterFirst;                    // $A5 written: waiting for $96

    public bool NewMode { get; private set; }
    public byte ControlA => registers[REG_CONTROL_A];
    public byte ControlB => registers[REG_CONTROL_B];
    public bool Fast => NewMode && (registers[REG_CONTROL_B] & B_FAST) != 0;

    public CSG4567()
    {
        Reset();
    }

    public void Reset()
    {
        Array.Clear(registers);
        registers[REG_BORDER_COLOR] = 14;     // light blue border
        registers[REG_BACKGROUND_COLOR] = 6;  // blue background
        NewMode = false;
        keyAfterFirst = false;
        for (int component = 0; component < 3; component++)
            for (int i = 0; i < 256; i++)
                paletteRam[component][i] = (byte)RomPalette[i & 15][component];

        rasterLine = 0;
        renderedTo = 0;
        currentCycle = 0;
        rasterCompare = 0;
        badLinesEnabled = false;
        mainBorder = verticalBorder = true;
        ResetSprites();
    }

    // End of a cycle: at the end of the last cycle of the line the next line starts.
    // BusAvailable must be read before, because it concerns the current cycle
    public void Tick()
    {
        if (currentCycle == CYCLES_PER_LINE - 1)
        {
            currentCycle = 0;
            EndLine();
        }
        else
        {
            currentCycle++;
        }
        SpriteCycle();
    }

    // Bad line: lines $30-$F7 whose 3 low raster bits match YSCROLL, if the display was enabled (DEN) during line $30
    internal bool IsBadLine =>
        badLinesEnabled &&
        rasterLine >= 0x30 && rasterLine <= 0xF7 &&
        (rasterLine & 0x07) == (registers[REG_CONTROL_1] & 0x07);

    // A line where the chip fetches more than the VIC-II does: 80 columns, or more bitplanes than fit in the
    // VIC-II's fetch (4 in 320 mode, 2 in 640 mode). It costs the CPU one more block of 40 cycles on all the 200 display lines
    internal bool ExtraFetch
    {
        get
        {
            if (!NewMode) return false;
            byte b = registers[REG_CONTROL_B];
            bool wide = (b & B_H640) != 0;
            if ((b & B_BPM) == 0) return wide;
            int planes = int.PopCount(registers[REG_BITPLANE_ENABLE] & (wide ? 0x0F : 0xFF));
            return planes > (wide ? 2 : 4) || (registers[REG_BITPLANE_ENABLE] & (wide ? 0x0C : 0xF0)) != 0;
        }
    }

    // A raster line of the 200 of the display (the display is on and the line is inside it)
    bool IsDisplayLine
    {
        get
        {
            if (!badLinesEnabled) return false;
            int line = rasterLine - (FIRST_TEXT_LINE + (registers[REG_CONTROL_1] & 0x07) + (NewMode ? (sbyte)registers[REG_VPOS] : 0));
            return line is >= 0 and < 200;
        }
    }

    // On bad lines the chip reads the character codes and keeps the bus (BA low 3 cycles before AEC), and so it does on every
    // display line when it fetches more
    bool StealsBus => IsBadLine || (ExtraFetch && IsDisplayLine);
    public bool BusAvailable => !(StealsBus && currentCycle >= 11 && currentCycle <= 53) && SpriteDmaCycles(SpriteBaWindow) == 0;
    public bool AddressEnable => !(StealsBus && currentCycle >= 14 && currentCycle <= 53) && SpriteDmaCycles(SpriteAecWindow) == 0;

    void EndLine()
    {
        RenderLine(rasterLine);
        LatchSprites();

        rasterLine = (rasterLine + 1) % TOTAL_RASTER_LINES;
        registers[REG_RASTER] = (byte)(rasterLine & 0xFF);
        if ((rasterLine & 0x100) != 0)
            registers[REG_CONTROL_1] |= 0x80;
        else
            registers[REG_CONTROL_1] &= 0x7F;

        if (rasterLine == 0)
            badLinesEnabled = false;
        if (rasterLine == 0x30 && (registers[REG_CONTROL_1] & 0x10) != 0)
            badLinesEnabled = true;

        CheckRasterCompare();

        if (rasterLine == 0)
        {
            frames++;
            FrameCompleted?.Invoke();
        }
    }

    void CheckRasterCompare()
    {
        if (rasterLine == rasterCompare)
        {
            registers[REG_INTERRUPT_STATUS] |= 0x01;
            CheckInterrupts();
        }
    }

    void CheckInterrupts()
    {
        if ((registers[REG_INTERRUPT_STATUS] & registers[REG_INTERRUPT_ENABLE] & 0x0F) != 0)
            registers[REG_INTERRUPT_STATUS] |= 0x80;
        else
            registers[REG_INTERRUPT_STATUS] &= 0x7F;
    }

    public bool Irq => (registers[REG_INTERRUPT_STATUS] & 0x80) != 0;

    // ---- Registers --------------------------------------------------------------------------------------
    // In VIC-II mode the 64 registers repeat in $D000-$D3FF and only $00-$2E exist (and the KEY register); in
    // VIC-III mode the registers go up to $7F
    public byte Read(int register)
    {
        if (!NewMode) register &= 0x3F;
        register &= 0x7F;
        if (register == REG_INTERRUPT_STATUS)
            return (byte)(registers[register] | 0x70);
        if (register == REG_SPRITE_SPRITE_COLL || register == REG_SPRITE_DATA_COLL)
        {
            byte collisions = registers[register];
            registers[register] = 0;
            return collisions;
        }
        if (register <= REG_LAST_VIC2) return registers[register];
        if (!NewMode) return 0xFF;
        if (register >= REG_DAT && register < REG_DAT + 8) return ReadDat(register - REG_DAT);
        return registers[register];
    }

    public void Write(int register, byte value)
    {
        if (!NewMode) register &= 0x3F;
        register &= 0x7F;

        if (register == REG_KEY)
        {
            // $A5 then $96 switches to VIC-III mode, anything else back
            bool enter = keyAfterFirst && value == 0x96;
            keyAfterFirst = value == 0xA5;
            NewMode = enter;
            registers[REG_KEY] = value;
            return;
        }
        if (register > REG_LAST_VIC2 && !NewMode) return;

        // The pixels already output on this line keep the old values
        if (register != REG_INTERRUPT_STATUS && register != REG_INTERRUPT_ENABLE)
            CatchUp();

        switch (register)
        {
            case REG_RASTER:
                rasterCompare = (rasterCompare & 0x100) | value;
                CheckRasterCompare();
                break;
            case REG_CONTROL_1:
                rasterCompare = (rasterCompare & 0xFF) | ((value & 0x80) << 1);
                registers[register] = (byte)((value & 0x7F) | (registers[register] & 0x80));
                if (rasterLine == 0x30 && (value & 0x10) != 0)
                    badLinesEnabled = true;
                CheckRasterCompare();
                break;
            case REG_INTERRUPT_STATUS:
                registers[register] &= (byte)~(value & 0x0F);
                CheckInterrupts();
                break;
            case REG_INTERRUPT_ENABLE:
                registers[register] = (byte)(value & 0x0F);
                CheckInterrupts();
                break;
            case REG_SPRITE_SPRITE_COLL:
            case REG_SPRITE_DATA_COLL:
                break; // read-only
            default:
                if (register >= REG_DAT && register < REG_DAT + 8) WriteDat(register - REG_DAT, value);
                else registers[register] = value;
                break;
        }
    }

    // $D100-$D3FF: the palette RAM; red, green or blue of an entry (4 bits; bit 4 of red is FG/BG, unused here)
    public void WritePalette(int component, int index, byte value) =>
        paletteRam[component][index & 0xFF] = (byte)(value & 0x1F);

    public byte Palette(int component, int index) => paletteRam[component][index & 0xFF];

    // ---- DAT: Display Address Translator ----------------------------------------------------------------
    // The CPU gives the (X, Y) of a byte of bitplane memory in BPX/BPY and reads or writes it through one of the
    // eight DAT ports, without computing the address of the 8x8 cell layout. X is in bytes (0-79), Y in lines
    // (0-399 in 400-line mode), the plane's address comes from its bitplane address register
    int DatAddress(int plane)
    {
        bool wide = (registers[REG_CONTROL_B] & B_H640) != 0;
        int columns = wide ? 80 : 40;
        int x = registers[REG_BITPLANE_X] & 0x7F;
        int y = registers[REG_BITPLANE_Y] | (registers[REG_BITPLANE_X] & 0x80) << 1;
        bool interlaced400 = (registers[REG_CONTROL_B] & (B_V400 | B_INT)) == (B_V400 | B_INT);
        int field = interlaced400 ? y & 1 : 0;
        if (interlaced400) y >>= 1;
        return PlaneBase(plane, field, wide) + (y >> 3) * columns * 8 + x * 8 + (y & 7);
    }

    byte ReadDat(int plane) => Memory?.ReadRam(DatAddress(plane)) ?? 0xFF;
    void WriteDat(int plane, byte value) => Memory?.WriteRam(DatAddress(plane), value);

    // Start of the bitplane in memory: even planes in the first 64 KB, odd planes in the second; the 8 KB block
    // (16 KB in 640 mode) comes from the plane's address register, even or odd scan bits
    int PlaneBase(int plane, int field, bool wide)
    {
        byte register = registers[REG_BITPLANE_ADDRESS + plane];
        int block = field == 0 ? (register & 0x0E) << 12 : (register & 0xE0) << 8;
        if (wide) block &= ~0x2000;
        return ((plane & 1) << 16) | block;
    }
}
