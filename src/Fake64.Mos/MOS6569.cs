namespace Fake64.Mos;

public partial class MOS6569 : I6569Pinout
{
    // Video bus (A0-A13, D0-D11) and video output, wired by the board
    public IVicMemory? Memory { get; set; }
    public Action? FrameCompleted { get; set; }

    // The VIC-II generates its own timing: raster line and cycle within the line
    private int rasterLine;
    private int currentCycle;
    private int rasterCompare;        // Raster interrupt line (written through $D012 and bit 7 of $D011)
    private bool badLinesEnabled;     // DEN ($D011 bit 4) set during line $30 of the current frame

    // VIC-II has 47 registers (0x00-0x2E)
    private byte[] registers = new byte[0x2F];

    // Constants for register addresses
    const byte REG_SPRITE0_X = 0x00;        // Sprite 0 X position
    const byte REG_SPRITE0_Y = 0x01;        // Sprite 0 Y position
    const byte REG_SPRITE1_X = 0x02;
    const byte REG_SPRITE1_Y = 0x03;
    const byte REG_SPRITE2_X = 0x04;
    const byte REG_SPRITE2_Y = 0x05;
    const byte REG_SPRITE3_X = 0x06;
    const byte REG_SPRITE3_Y = 0x07;
    const byte REG_SPRITE4_X = 0x08;
    const byte REG_SPRITE4_Y = 0x09;
    const byte REG_SPRITE5_X = 0x0A;
    const byte REG_SPRITE5_Y = 0x0B;
    const byte REG_SPRITE6_X = 0x0C;
    const byte REG_SPRITE6_Y = 0x0D;
    const byte REG_SPRITE7_X = 0x0E;
    const byte REG_SPRITE7_Y = 0x0F;
    const byte REG_SPRITE_X_MSB = 0x10;     // MSB of X positions for all sprites
    const byte REG_CONTROL_1 = 0x11;        // Control register 1
    const byte REG_RASTER = 0x12;           // Raster counter
    const byte REG_LIGHT_PEN_X = 0x13;      // Light pen X position
    const byte REG_LIGHT_PEN_Y = 0x14;      // Light pen Y position
    const byte REG_SPRITE_ENABLE = 0x15;    // Sprite enable register
    const byte REG_CONTROL_2 = 0x16;        // Control register 2
    const byte REG_SPRITE_Y_EXPAND = 0x17;  // Sprite vertical expansion
    const byte REG_MEMORY_POINTERS = 0x18;  // Memory pointers
    const byte REG_INTERRUPT_STATUS = 0x19; // Interrupt status
    const byte REG_INTERRUPT_ENABLE = 0x1A; // Interrupt enable
    const byte REG_SPRITE_PRIORITY = 0x1B;  // Sprite priority
    const byte REG_SPRITE_MULTICOLOR = 0x1C;// Sprite multicolor mode
    const byte REG_SPRITE_X_EXPAND = 0x1D;  // Sprite horizontal expansion
    const byte REG_SPRITE_SPRITE_COLL = 0x1E;// Sprite-sprite collision
    const byte REG_SPRITE_DATA_COLL = 0x1F; // Sprite-background collision
    const byte REG_BORDER_COLOR = 0x20;     // Border color
    const byte REG_BACKGROUND_COLOR = 0x21; // Background color 0
    const byte REG_BACKGROUND_COLOR_1 = 0x22;// Background color 1
    const byte REG_BACKGROUND_COLOR_2 = 0x23;// Background color 2
    const byte REG_BACKGROUND_COLOR_3 = 0x24;// Background color 3
    const byte REG_SPRITE_MULTICOLOR_0 = 0x25;// Sprite multicolor 0
    const byte REG_SPRITE_MULTICOLOR_1 = 0x26;// Sprite multicolor 1
    const byte REG_SPRITE0_COLOR = 0x27;    // Sprite 0 color
    const byte REG_SPRITE1_COLOR = 0x28;
    const byte REG_SPRITE2_COLOR = 0x29;
    const byte REG_SPRITE3_COLOR = 0x2A;
    const byte REG_SPRITE4_COLOR = 0x2B;
    const byte REG_SPRITE5_COLOR = 0x2C;
    const byte REG_SPRITE6_COLOR = 0x2D;
    const byte REG_SPRITE7_COLOR = 0x2E;

    // Screen properties
    const ushort TOTAL_RASTER_LINES = 312;    // PAL has 312 raster lines
    const ushort CYCLES_PER_LINE = 63;        // 63 cycles per raster line

    public MOS6569()
    {
        Reset();
    }

    public void Reset()
    {
        // Initialize all registers to their default values
        for (int i = 0; i < registers.Length; i++)
        {
            registers[i] = 0;
        }

        // Set some default values
        registers[REG_BORDER_COLOR] = 14;     // Light blue border
        registers[REG_BACKGROUND_COLOR] = 6;  // Blue background

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

    // Bad line: lines $30-$F7 whose 3 low raster bits match YSCROLL ($D011 bits 0-2), if the display was
    // enabled (DEN) during line $30. Evaluated at every cycle, so writes to $D011 during the line take effect
    // at once
    internal bool IsBadLine =>
        badLinesEnabled &&
        rasterLine >= 0x30 && rasterLine <= 0xF7 &&
        (rasterLine & 0x07) == (registers[REG_CONTROL_1] & 0x07);

    // On bad lines the VIC-II reads 40 character codes in cycles 15-54 (counting from 1): in those cycles it
    // keeps AEC low and the bus is its own. BA goes low 3 cycles earlier (from cycle 12), so that the CPU, wired
    // to RDY, can finish the writes in progress and stops at the first read cycle.
    // Sprites with DMA on take more cycles, at the end and at the start of the line (see MOS6569.Sprites.cs)
    public bool BusAvailable => !(IsBadLine && currentCycle >= 11 && currentCycle <= 53) && SpriteDmaCycles(SpriteBaWindow) == 0;
    public bool AddressEnable => !(IsBadLine && currentCycle >= 14 && currentCycle <= 53) && SpriteDmaCycles(SpriteAecWindow) == 0;

    // Handle end-of-line processing
    private void EndLine()
    {
        // Draws the line just finished with the current register state
        RenderLine(rasterLine);
        LatchSprites();

        // Update the raster line counter
        rasterLine = (rasterLine + 1) % TOTAL_RASTER_LINES;

        // Update the raster register (with 8-bit overflow)
        registers[REG_RASTER] = (byte)(rasterLine & 0xFF);

        // Set/clear the 9th bit of the raster line in control register 1
        if ((rasterLine & 0x100) != 0)
            registers[REG_CONTROL_1] |= 0x80;
        else
            registers[REG_CONTROL_1] &= 0x7F;

        // The frame's bad lines depend on DEN during line $30
        if (rasterLine == 0)
            badLinesEnabled = false;
        if (rasterLine == 0x30 && (registers[REG_CONTROL_1] & 0x10) != 0)
            badLinesEnabled = true;

        CheckRasterCompare();

        // End of frame: the video output presents the picture
        if (rasterLine == 0)
            FrameCompleted?.Invoke();
    }

    // The raster interrupt triggers when the current line reaches the compare value
    // (also when the compare value is written while that line is already in progress)
    private void CheckRasterCompare()
    {
        if (rasterLine == rasterCompare)
        {
            registers[REG_INTERRUPT_STATUS] |= 0x01;
            CheckInterrupts();
        }
    }

    // Recomputes bit 7 of $D019, which is the state of the VIC-II's /IRQ line
    private void CheckInterrupts()
    {
        if ((registers[REG_INTERRUPT_STATUS] & registers[REG_INTERRUPT_ENABLE] & 0x0F) != 0)
            registers[REG_INTERRUPT_STATUS] |= 0x80; // Set IRQ flag
        else
            registers[REG_INTERRUPT_STATUS] &= 0x7F;
    }

    // /IRQ
    public bool Irq => (registers[REG_INTERRUPT_STATUS] & 0x80) != 0;

    // Address space is 0x00-0x3F (64 bytes), but we only use 0x00-0x2E
    // A0-A5: the 64 registers repeat across the whole area selected by /CS
    public byte Read(int register)
    {
        ushort addr = (ushort)register;
        addr &= 0x3F; // The registers repeat every 64 bytes in $D000-$D3FF
        if (addr == REG_INTERRUPT_STATUS)
            return (byte)(registers[addr] | 0x70); // Bits 4-6 unused, read as 1
        if (addr == REG_SPRITE_SPRITE_COLL || addr == REG_SPRITE_DATA_COLL)
        {
            // The collision registers are cleared when read
            byte collisions = registers[addr];
            registers[addr] = 0;
            return collisions;
        }
        if (addr <= 0x2E)
            return registers[addr];
        else
            return 0xFF; // Unused registers return 0xFF
    }

    public void Write(int register, byte value)
    {
        ushort addr = (ushort)register;
        addr &= 0x3F; // The registers repeat every 64 bytes in $D000-$D3FF
        if (addr > 0x2E) return; // Unused registers

        // The pixels already output on this line keep the old values
        if (addr != REG_INTERRUPT_STATUS && addr != REG_INTERRUPT_ENABLE)
            CatchUp();

        // Special handling for certain registers
        switch (addr)
        {
            case REG_RASTER:
                // Writing sets the compare value, reading returns the current line
                rasterCompare = (rasterCompare & 0x100) | value;
                CheckRasterCompare();
                break;
            case REG_CONTROL_1:
                // Bit 7 is bit 8 of the compare value when written, bit 8 of the raster when read
                rasterCompare = (rasterCompare & 0xFF) | ((value & 0x80) << 1);
                registers[addr] = (byte)((value & 0x7F) | (registers[addr] & 0x80));
                if (rasterLine == 0x30 && (value & 0x10) != 0)
                    badLinesEnabled = true;
                CheckRasterCompare();
                break;
            case REG_INTERRUPT_STATUS:
                // Writing 1s clears the corresponding bits
                registers[addr] &= (byte)~(value & 0x0F);
                CheckInterrupts();
                break;
            case REG_INTERRUPT_ENABLE:
                registers[addr] = (byte)(value & 0x0F);
                CheckInterrupts();
                break;
            case REG_SPRITE_SPRITE_COLL:
            case REG_SPRITE_DATA_COLL:
                break; // Read-only registers
            default:
                registers[addr] = value;
                break;
        }
    }
}
