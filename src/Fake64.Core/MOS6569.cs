using Fake64;
using System.Diagnostics;
using System.Threading.Channels;

public class MOS6569
{
    private Board board;
    Channel<int> channel = Channel.CreateUnbounded<int>();

    private int rasterLine;
    private int currentCycle;
    private bool badLine;
    private bool rasterIrqPending;
    private bool spriteSpriteCollisionPending;
    private bool spriteBackgroundCollisionPending;
    private bool frameComplete;
    private bool lineComplete;

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
    const ushort VISIBLE_SCREEN_WIDTH = 320;
    const ushort VISIBLE_SCREEN_HEIGHT = 200;

    // Memory pointers
    private ushort videoMatrixBase;
    private ushort charGenBase;
    private ushort bitmapBase;

    public MOS6569(Board board)
    {
        this.board = board;
        this.channel = channel; 
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
        currentCycle = 0;
        badLine = false;
        rasterIrqPending = false;
        spriteSpriteCollisionPending = false;
        spriteBackgroundCollisionPending = false;
        frameComplete = false;
        lineComplete = false;
        
        // Initialize memory pointers
        UpdateMemoryPointers();
    }


    // Process VIC-II signals for the current cycle
    internal void ProcessSignals(int currentCycle)
    {
        this.currentCycle = currentCycle;
        
        // Check if we're about to reach the end of the line
        lineComplete = (currentCycle == CYCLES_PER_LINE - 1);
        
        // At the end of a line, check if we'll reach a raster interrupt in the next line
        if (lineComplete)
        {
            int nextRasterLine = (rasterLine + 1) % TOTAL_RASTER_LINES;
            int targetRaster = ((registers[REG_CONTROL_1] & 0x80) << 1) | registers[REG_RASTER];
            
            // Will we hit the raster interrupt on the next line?
            rasterIrqPending = (nextRasterLine == targetRaster);
            
            // Calculate if the next line will be a bad line
            // Bad lines occur between raster lines 48-247 when the 3 LSBs of the raster line 
            // match the scroll value in Control Register 1
            bool nextLineBadLine = (nextRasterLine >= 0x30 && nextRasterLine <= 0xF7) &&
                                  ((nextRasterLine & 0x07) == (registers[REG_CONTROL_1] & 0x07));
            
            // Will we transition to a new frame in the next cycle?
            frameComplete = (nextRasterLine == 0);
        }
        
        // Check for sprite collisions - simplified for this example
        // In a real implementation, you would check all sprite positions and background data
        if (registers[REG_SPRITE_ENABLE] != 0)
        {
            // Simulate sprite-sprite collision detection
            if ((registers[REG_SPRITE_ENABLE] & (registers[REG_SPRITE_ENABLE] - 1)) != 0)
            {
                // If more than one sprite is enabled, collision is possible
                spriteSpriteCollisionPending = true;
            }
            
            // Simulate sprite-background collision detection
            // This would need actual sprite position and background data checking
            spriteBackgroundCollisionPending = (registers[REG_SPRITE_ENABLE] != 0);
        }
    }

    // Handle end-of-line processing
    internal void EndLine(int currentLine)
    {
        if (lineComplete)
        {
            // Update the raster line counter
            rasterLine = (rasterLine + 1) % TOTAL_RASTER_LINES;
            
            // Update the raster register (with 8-bit overflow)
            registers[REG_RASTER] = (byte)(rasterLine & 0xFF);
            
            // Set/clear the 9th bit of the raster line in control register 1
            if ((rasterLine & 0x100) != 0)
                registers[REG_CONTROL_1] |= 0x80;
            else
                registers[REG_CONTROL_1] &= 0x7F;
            
            // Check for raster interrupt that we identified in ProcessSignals
            if (rasterIrqPending)
            {
                // Set raster interrupt flag
                registers[REG_INTERRUPT_STATUS] |= 0x01;
                
                // If raster interrupts are enabled, signal IRQ
                if ((registers[REG_INTERRUPT_ENABLE] & 0x01) != 0)
                {
                    board.CpuTriggerIRQ();
                }
                
                rasterIrqPending = false;
            }
            
            // Calculate bad line condition for current line
            badLine = (rasterLine >= 0x30 && rasterLine <= 0xF7) &&
                     ((rasterLine & 0x07) == (registers[REG_CONTROL_1] & 0x07));
            
            // If this is a bad line, CPU will be stalled for character data fetches
            // This affects overall system timing and would need to be communicated to the CPU
            
            // Handle frame boundary
            if (frameComplete)
            {
                // Signal frame completion (for synchronization)
                channel.Writer.TryWrite(channel.Reader.Count);
                frameComplete = false;
            }
            
            lineComplete = false;
        }
        
        // Handle collisions detected in ProcessSignals
        if (spriteSpriteCollisionPending)
        {
            registers[REG_SPRITE_SPRITE_COLL] |= registers[REG_SPRITE_ENABLE];
            registers[REG_INTERRUPT_STATUS] |= 0x04; // Set sprite-sprite collision interrupt
            CheckInterrupts();
            spriteSpriteCollisionPending = false;
        }
        
        if (spriteBackgroundCollisionPending)
        {
            registers[REG_SPRITE_DATA_COLL] |= registers[REG_SPRITE_ENABLE];
            registers[REG_INTERRUPT_STATUS] |= 0x02; // Set sprite-background collision interrupt
            CheckInterrupts();
            spriteBackgroundCollisionPending = false;
        }
    }

    // Check and trigger interrupts if enabled
    private void CheckInterrupts()
    {
        if ((registers[REG_INTERRUPT_STATUS] & registers[REG_INTERRUPT_ENABLE] & 0x0F) != 0)
        {
            registers[REG_INTERRUPT_STATUS] |= 0x80; // Set IRQ flag
            board.CpuTriggerIRQ();
        }
    }

    // Address space is 0x00-0x3F (64 bytes), but we only use 0x00-0x2E
    public byte Address(ushort addr)
    {
        if (addr <= 0x2E)
            return registers[addr];
        else
            return 0xFF; // Unused registers return 0xFF
    }

    public void Address(ushort addr, byte value)
    {
        // Special handling for certain registers
        switch (addr)
        {
            case REG_RASTER:
                registers[addr] = value;
                break;
            case REG_CONTROL_1:
                registers[addr] = value;
                break;
            case REG_INTERRUPT_STATUS:
                // Writing 1s clears the corresponding bits
                registers[addr] &= (byte)~value;
                break;
            default:
                registers[addr] = value;
                break;
        }
    }

    // Update memory pointers based on register settings
    private void UpdateMemoryPointers()
    {
        // Extract memory pointers from register 0x18 (Memory Pointers)
        videoMatrixBase = (ushort)((registers[REG_MEMORY_POINTERS] & 0xF0) << 6); // $0400, $0800, $0C00, etc.
        charGenBase = (ushort)((registers[REG_MEMORY_POINTERS] & 0x0E) << 10);    // $1000, $1800, etc.
        bitmapBase = (ushort)((registers[REG_MEMORY_POINTERS] & 0x08) << 10);     // $0000 or $2000
    }

    public void Clock(long ticks)
    {
        // This is now a single-cycle implementation
        // Each call to Clock processes exactly one cycle's worth of operations
        
        // Most of the cycle-specific operations are now handled in ProcessSignals and EndLine
        
        // Fetch data on cycle 1-5 of a bad line (simplification)
        if (badLine && currentCycle >= 1 && currentCycle <= 5)
        {
            // Perform character data fetch - this would stall the CPU
            // This is a simplification; real VIC-II has more complex BA signal timing
        }
        
        // Sprite data fetch cycles
        if (currentCycle >= 15 && currentCycle <= 54)
        {
            // Fetch sprite data if relevant sprites are enabled
            // Again, this is a simplification
        }
    }

    public ValueTask<bool> WaitForRetraceAsync()=> channel.Reader.WaitToReadAsync();

    public bool TryRead(out int value) => channel.Reader.TryRead(out value);

    unsafe public void Render(byte* scan0)
    {
        // Determine which display mode to use based on control registers
        byte displayMode = (byte)((registers[REG_CONTROL_1] & 0x60) >> 5);

        switch (displayMode)
        {
            case 0: // Standard character mode
                TextMode(scan0);
                break;
            case 1: // Multicolor character mode
                break;
            case 2: // Standard bitmap mode
                break;
            case 3: // Multicolor bitmap mode
                break;
        }
    }

    // https://lospec.com/palette-list/commodore64
    unsafe void TextMode(byte* scan0)
    {
        var borderColorAddress = (ushort)(registers[REG_BORDER_COLOR] << 2);
        var backgroundColorAddress = (ushort)(registers[REG_BACKGROUND_COLOR] << 2);

        for (var y = 0; y < 42; y++)
        {
            for (var x = 0; x < 403; x++)
            {
                var colorAddress = borderColorAddress;
                scan0[0] = palette[colorAddress++]; // blueComponent;
                scan0++;
                scan0[0] = palette[colorAddress++]; // greenComponent;
                scan0++;
                scan0[0] = palette[colorAddress++]; // redComponent;
                scan0++;
                scan0[0] = palette[colorAddress++]; // transparency;
                scan0++;
            }
        }
        for (var y = 0; y < 200; y++)
        {
            for (var x = 0; x < 42; x++)
            {
                var colorAddress = borderColorAddress;
                scan0[0] = palette[colorAddress++]; // blueComponent;
                scan0++;
                scan0[0] = palette[colorAddress++]; // greenComponent;
                scan0++;
                scan0[0] = palette[colorAddress++]; // redComponent;
                scan0++;
                scan0[0] = palette[colorAddress++]; // transparency;
                scan0++;
            }
            for (var x = 0; x < 320; x += 8)
            {
                var offset = (y >> 3) * 40 + (x >> 3);
                var ch = board.Address((ushort)(0x400 + offset));
                var chBits = board.Chargen((ushort)((ch << 3) + (y & 0b00000111)));
                var foregroundColorAddress = (ushort)(board.Address((ushort)(0xD800 + offset)) << 2);
                var mask = 0b10000000;
                while (mask > 0)
                {
                    var colorAddress = foregroundColorAddress;
                    if ((chBits & mask) == 0)
                        colorAddress = backgroundColorAddress;
                    scan0[0] = palette[colorAddress++]; // blueComponent;
                    scan0++;
                    scan0[0] = palette[colorAddress++]; // greenComponent;
                    scan0++;
                    scan0[0] = palette[colorAddress++]; // redComponent;
                    scan0++;
                    scan0[0] = palette[colorAddress++]; // transparency;
                    scan0++;

                    mask >>= 1;
                }
            }
            for (var x = 0; x < 41; x++)
            {
                var colorAddress = borderColorAddress;
                scan0[0] = palette[colorAddress++]; // blueComponent;
                scan0++;
                scan0[0] = palette[colorAddress++]; // greenComponent;
                scan0++;
                scan0[0] = palette[colorAddress++]; // redComponent;
                scan0++;
                scan0[0] = palette[colorAddress++]; // transparency;
                scan0++;
            }
        }
        for (var y = 0; y < 42; y++)
        {
            for (var x = 0; x < 403; x++)
            {
                var colorAddress = borderColorAddress;
                scan0[0] = palette[colorAddress++]; // blueComponent;
                scan0++;
                scan0[0] = palette[colorAddress++]; // greenComponent;
                scan0++;
                scan0[0] = palette[colorAddress++]; // redComponent;
                scan0++;
                scan0[0] = palette[colorAddress++]; // transparency;
                scan0++;
            }
        }
    }

    // http://unusedino.de/ec64/technical/misc/vic656x/colors/
    static byte[] palette = new byte[] {
        (byte) 0x00, (byte) 0x00, (byte) 0x00, (byte) 0xFF,  // 0 - Black
        (byte) 0xFF, (byte) 0xFF, (byte) 0xFF, (byte) 0xFF,  // 1 - White
        (byte) 0x2B, (byte) 0x37, (byte) 0x68, (byte) 0xFF,  // 2 - Red
        (byte) 0xB2, (byte) 0xA4, (byte) 0x70, (byte) 0xFF,  // 3 - Cyan
        (byte) 0x86, (byte) 0x3D, (byte) 0x6F, (byte) 0xFF,  // 4 - Purple
        (byte) 0x43, (byte) 0x8D, (byte) 0x58, (byte) 0xFF,  // 5 - Green
        (byte) 0x79, (byte) 0x28, (byte) 0x35, (byte) 0xFF,  // 6 - Blue
        (byte) 0x6F, (byte) 0xC7, (byte) 0xB8, (byte) 0xFF,  // 7 - Yellow
        (byte) 0x25, (byte) 0x4F, (byte) 0x6F, (byte) 0xFF,  // 8 - Orange
        (byte) 0x00, (byte) 0x39, (byte) 0x43, (byte) 0xFF,  // 9 - Brown
        (byte) 0x59, (byte) 0x67, (byte) 0x9A, (byte) 0xFF,  // A - Light Red
        (byte) 0x44, (byte) 0x44, (byte) 0x44, (byte) 0xFF,  // B - Dark Grey
        (byte) 0x6C, (byte) 0x6C, (byte) 0x6C, (byte) 0xFF,  // C - Grey
        (byte) 0x84, (byte) 0xD2, (byte) 0x9A, (byte) 0xFF,  // D - Light Green
        (byte) 0xB5, (byte) 0x5E, (byte) 0x6C, (byte) 0xFF,  // E - Light Blue
        (byte) 0x95, (byte) 0x95, (byte) 0x95, (byte) 0xFF   // F - Light Grey
    };
}