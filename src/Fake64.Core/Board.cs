using System.Diagnostics;
using System.Drawing;

namespace Fake64;

public partial class Board
{
    Task clock;

    MOS6510 cpu;
    MOS6569 vicii;
    MOS6581 sid;
    MOS6526 cia1;
    MOS6526 cia2;
    ColorRamChip colorRam;

    const byte addr_shift = 13;
    const ushort addr_mask = 0x1fff;

    Ram64KbChip ram64;

    byte[] Rom(string name) => File.ReadAllBytes(Path.Combine("roms", name));

    public Board(string kernalName = null, string chargenName = null, string basicName = null)
    {
        kernal = Rom(kernalName ?? nameof(kernal));
        chargen = Rom(chargenName ?? nameof(chargen));
        basic = Rom(basicName ?? nameof(basic));
        ram64 = new(this);
        colorRam = new ColorRamChip(this);
        cia2 = new MOS6526(this, 2);
        cia1 = new MOS6526(this, 1);
        sid = new MOS6581(this);
        vicii = new MOS6569(this);
        cpu = new MOS6510(this);
        MemoryMap();

        Reset();

        var f = 0.9852;
        //var microseconds = (long)(1_000_000 / f); // 1.02 MHz
        var microseconds = (long)(1_000_000 / f); // 1.02 MHz
        var targetTicks = Stopwatch.Frequency / microseconds;
        clock = Task.Factory.StartNew(async () =>
        {
            var sw = Stopwatch.StartNew();
            sw.Stop();
            while (true)
            {
                sw.Restart();
                Clock();
                while (sw.ElapsedTicks < targetTicks) { }
                sw.Stop();
            }
        });
    }

    int audioFrameCounter = 0;
    const int audioFrameRate = 50; // Example frame rate for audio processing

    public void Clock()
    {
        ram64.Clock();
        colorRam.Clock();
        cia2.Clock();
        cia1.Clock();
        sid.Clock();
        vicii.Clock();
        cpu.Clock();

        // Process SID audio output periodically
        audioFrameCounter++;
        if (audioFrameCounter >= audioFrameRate)
        {
            audioFrameCounter = 0;
            ProcessAudio();
        }
    }

    private void ProcessAudio()
    {
        // Get audio samples from SID and send to audio system
        int[] samples = sid.GetAudioData();
        // Send to audio output system
        // ...
    }

    public void Reset()
    {
        ram64.Reset();
        colorRam.Reset();
        cia2.Reset();
        cia1.Reset();
        sid.Reset();
        vicii.Reset();
        cpu.Reset();
    }

    public void Invalidate(Bitmap bitmap, Rectangle cr)
    {
        vicii.Invalidate(bitmap, cr);
    }

    internal void CpuTriggerIRQ()
    {
        cpu.TriggerIRQ();
    }

    internal void CIATriggerInterrupt(int ciaId)
    {
        if (ciaId == 1)
        {
            cpu.TriggerIRQ();
        }
        else if (ciaId == 2)
        {
            cpu.TriggerNMI();
        }
    }

    public byte Chargen(ushort addr) => chargen[addr];

    /*
        Bits #0-#2: Configuration for memory areas $A000-$BFFF, $D000-$DFFF and $E000-$FFFF. Values:
        %x00: RAM visible in all three areas.
        %x01: RAM visible at $A000-$BFFF and $E000-$FFFF.
        %x10: RAM visible at $A000-$BFFF; KERNAL ROM visible at $E000-$FFFF.
        %x11: BASIC ROM visible at $A000-$BFFF; KERNAL ROM visible at $E000-$FFFF.
        %0xx: Character ROM visible at $D000-$DFFF. (Except for the value %000, see above.)
        %1xx: I/O area visible at $D000-$DFFF. (Except for the value %100, see above.)
     */

    void MemoryMap()
    {
        var state = cpu.Address(0x01);
        ramVisible = ((state & (byte)0b00000011) == 0b00000000);
        kernalVisible = ((state & (byte)0b00000010) == 0b00000010);
        basicRomVisible = ((state & (byte)0b00000011) == 0b00000011);

        chargenVisible = ((state & (byte)0b00000100) == 0b00000000) && !ramVisible;
        ioAreaVisible = ((state & (byte)0b00000100) == 0b00000100) && !ramVisible;
    }

    bool ramVisible;
    bool kernalVisible;
    bool basicRomVisible;

    bool chargenVisible;
    bool ioAreaVisible;

    public byte Address(ushort addr)
    {
        if (addr == 0xE473)
        { 
        }
        
        if (addr >= kernal_base)
        {
            if (kernalVisible)
            {
                return kernal[addr - kernal_base];
            }
        }
        else if (addr >= chargen_base)
        {
            if (chargenVisible)
            {
                return chargen[addr - chargen_base];
            }
            else if (ioAreaVisible)
            {
                if (addr < 0xd400)
                {
                    return vicii.Address((ushort)(addr & 0x03ff));
                }
                else if (addr >= 0xdf00) // I/O 2
                {
                }
                else if (addr >= 0xde00) // I/O 1
                {
                }
                else if (addr >= 0xdd00) // CIA 2
                {
                    return cia2.Address((ushort)(addr & 0x00ff));
                }
                else if (addr >= 0xdc00) // CIA 1
                {
                    return cia1.Address((ushort)(addr & 0x00ff));
                }
                else if (addr >= 0xd800) // Color RAM
                {
                    return colorRam.Address((ushort)(addr & 0x03ff));
                }
                else if (addr >= 0xd400) // SID
                {
                    return sid.Address((ushort)(addr & 0x03ff));
                }
                else
                {
                    return vicii.Address((ushort)(addr & 0x03ff));
                }
            }
            else
            {
            }
        }
        else if (addr >= 0xc000)
        {
        }
        else if (addr >= basic_base)
        {
            if (basicRomVisible)
            {
                return basic[addr - basic_base];
            }
            else
            {
            }
        }
        else if (addr <= 0x0001)
        {
            return cpu.Address(addr);
        }
        else
        {
        }
        //return ram[addr >> addr_shift].Address((ushort)(addr & addr_mask));
        return ram64.Address(addr);
    }

    public void Address(ushort addr, byte value)
    {
        if (addr >= kernal_base)
        {
        }
        else if (addr >= chargen_base)
        {
            if (chargenVisible)
            {
            }
            else if (ioAreaVisible)
            {
                if (addr < 0xd400)
                {
                    vicii.Address((ushort)(addr & 0x03ff), value);
                }
                else if (addr >= 0xdf00) // I/O 2
                {
                }
                else if (addr >= 0xde00) // I/O 1
                {
                }
                else if (addr >= 0xdd00) // CIA 2
                {
                    cia2.Address((ushort)(addr & 0x00ff), value);
                    return;
                }
                else if (addr >= 0xdc00) // CIA 1
                {
                    cia1.Address((ushort)(addr & 0x00ff), value);
                    return;
                }
                else if (addr >= 0xd800) // Color RAM
                {
                    colorRam.Address((ushort)(addr & 0x03ff), value);
                    return;
                }
                else if (addr >= 0xd400) // SID
                {
                    sid.Address((ushort)(addr & 0x03ff), value);
                    return;
                }
                else
                {
                    vicii.Address((ushort)(addr & 0x03ff), value);
                    return;
                }
            }
            else
            {
            }
        }
        else if (addr >= 0xc000)
        {
        }
        else if (addr >= basic_base)
        {

        }
        else if (addr >= 0x0400 && addr<0x0500)
        {
        }
        else if (addr <= 0x0001)
        {
            cpu.Address(addr, value);
            MemoryMap();
            return;
        }
        else
        {
        }

        ram64.Address(addr, value);
    }

    const ushort kernal_base = 0xE000;
    byte[] kernal;
    const ushort chargen_base = 0xD000;
    byte[] chargen;
    const ushort basic_base = 0xA000;
    byte[] basic;

    private byte[] keyboardMatrix = new byte[8] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };

    // Rest of the existing code...

    // Method to get keyboard matrix state for CIA1
    public byte GetKeyboardState(byte rowSelectionMask)
    {
        // Return keyboard matrix columns based on rows selected by CIA1 Port A
        byte result = 0xFF;
        for (int row = 0; row < 8; row++)
        {
            // When a bit in rowSelectionMask is 0, that row is selected
            if ((rowSelectionMask & (1 << row)) == 0)
            {
                result &= keyboardMatrix[row];
            }
        }
        return result;
    }

    // Methods for keyboard input
    public void KeyDown(byte row, byte col)
    {
        if (row >= 0 && row < 8 && col >= 0 && col < 8)
        {
            // Clear the bit (0 = pressed)
            keyboardMatrix[row] &= (byte)~(1 << col);
        }
        // No need to explicitly update CIA ports here
        // CIA1 will read from the matrix when the CPU accesses Port B
    }

    public void KeyUp(byte row, byte col)
    {
        if (row >= 0 && row < 8 && col >= 0 && col < 8)
        {
            // Set the bit (1 = released)
            keyboardMatrix[row] |= (byte)(1 << col);
        }
        // No need to explicitly update CIA ports here
    }

    // Helper method to simulate pressing a key by its name/symbol
    public void PressKey(char keyName)
    {
        // Map common key names to row/column positions
        // This mapping should match the C64 keyboard matrix
        KeyPosition pos = GetKeyPosition(keyName);
        if (pos.IsValid)
        {
            KeyDown((byte)pos.Row, (byte)pos.Col);
        }
    }

    // Helper method to simulate releasing a key by its name/symbol
    public void ReleaseKey(char keyName)
    {
        KeyPosition pos = GetKeyPosition(keyName);
        if (pos.IsValid)
        {
            KeyUp((byte)pos.Row, (byte)pos.Col);
        }
    }

    private struct KeyPosition
    {
        public int Row { get; set; }
        public int Col { get; set; }
        public bool IsValid { get; set; }

        public static KeyPosition Invalid => new KeyPosition { IsValid = false };
    }

    private KeyPosition GetKeyPosition(char keyName)
    {
        // C64 keyboard matrix mapping
        // This is a simplified example - extend with the full C64 keyboard layout
        switch (keyName)
        {
            // Row 0
            //case 'DELETE': case 'BACK': case 'DEL': return new KeyPosition { Row = 0, Col = 0, IsValid = true };
            //case 'RETURN': case 'ENTER': return new KeyPosition { Row = 0, Col = 1, IsValid = true };
            //case 'CURSOR_RIGHT': case 'RIGHT': return new KeyPosition { Row = 0, Col = 2, IsValid = true };
            //case 'F7': return new KeyPosition { Row = 0, Col = 3, IsValid = true };
            //case 'F1': return new KeyPosition { Row = 0, Col = 4, IsValid = true };
            //case 'F3': return new KeyPosition { Row = 0, Col = 5, IsValid = true };
            //case 'F5': return new KeyPosition { Row = 0, Col = 6, IsValid = true };
            //case 'CURSOR_DOWN': case 'DOWN': return new KeyPosition { Row = 0, Col = 7, IsValid = true };

            // Row 1
            case '3': return new KeyPosition { Row = 1, Col = 0, IsValid = true };
            case 'W': return new KeyPosition { Row = 1, Col = 1, IsValid = true };
            case 'A': return new KeyPosition { Row = 1, Col = 2, IsValid = true };
            case '4': return new KeyPosition { Row = 1, Col = 3, IsValid = true };
            case 'Z': return new KeyPosition { Row = 1, Col = 4, IsValid = true };
            case 'S': return new KeyPosition { Row = 1, Col = 5, IsValid = true };
            case 'E': return new KeyPosition { Row = 1, Col = 6, IsValid = true };
            //case 'CURSOR_LEFT': case 'LEFT': return new KeyPosition { Row = 1, Col = 7, IsValid = true };

            // Row 2
            case '5': return new KeyPosition { Row = 2, Col = 0, IsValid = true };
            case 'R': return new KeyPosition { Row = 2, Col = 1, IsValid = true };
            case 'D': return new KeyPosition { Row = 2, Col = 2, IsValid = true };
            case '6': return new KeyPosition { Row = 2, Col = 3, IsValid = true };
            case 'C': return new KeyPosition { Row = 2, Col = 4, IsValid = true };
            case 'F': return new KeyPosition { Row = 2, Col = 5, IsValid = true };
            case 'T': return new KeyPosition { Row = 2, Col = 6, IsValid = true };
            case 'X': return new KeyPosition { Row = 2, Col = 7, IsValid = true };

            // Row 3
            case '7': return new KeyPosition { Row = 3, Col = 0, IsValid = true };
            case 'Y': return new KeyPosition { Row = 3, Col = 1, IsValid = true };
            case 'G': return new KeyPosition { Row = 3, Col = 2, IsValid = true };
            case '8': return new KeyPosition { Row = 3, Col = 3, IsValid = true };
            case 'B': return new KeyPosition { Row = 3, Col = 4, IsValid = true };
            case 'H': return new KeyPosition { Row = 3, Col = 5, IsValid = true };
            case 'U': return new KeyPosition { Row = 3, Col = 6, IsValid = true };
            case 'V': return new KeyPosition { Row = 3, Col = 7, IsValid = true };

            // Row 4
            case '9': return new KeyPosition { Row = 4, Col = 0, IsValid = true };
            case 'I': return new KeyPosition { Row = 4, Col = 1, IsValid = true };
            case 'J': return new KeyPosition { Row = 4, Col = 2, IsValid = true };
            case '0': return new KeyPosition { Row = 4, Col = 3, IsValid = true };
            case 'M': return new KeyPosition { Row = 4, Col = 4, IsValid = true };
            case 'K': return new KeyPosition { Row = 4, Col = 5, IsValid = true };
            case 'O': return new KeyPosition { Row = 4, Col = 6, IsValid = true };
            case 'N': return new KeyPosition { Row = 4, Col = 7, IsValid = true };

            // Row 5
            case '+': return new KeyPosition { Row = 5, Col = 0, IsValid = true };
            case 'P': return new KeyPosition { Row = 5, Col = 1, IsValid = true };
            case 'L': return new KeyPosition { Row = 5, Col = 2, IsValid = true };
            case '-': return new KeyPosition { Row = 5, Col = 3, IsValid = true };
            case '.': return new KeyPosition { Row = 5, Col = 4, IsValid = true };
            case ':': return new KeyPosition { Row = 5, Col = 5, IsValid = true };
            case '@': return new KeyPosition { Row = 5, Col = 6, IsValid = true };
            case ',': return new KeyPosition { Row = 5, Col = 7, IsValid = true };

            // Row 6
            //case 'POUND': case '£': return new KeyPosition { Row = 6, Col = 0, IsValid = true };
            case '*': return new KeyPosition { Row = 6, Col = 1, IsValid = true };
            case ';': return new KeyPosition { Row = 6, Col = 2, IsValid = true };
            //case 'HOME': case 'CLR': return new KeyPosition { Row = 6, Col = 3, IsValid = true };
            //case 'UP': return new KeyPosition { Row = 6, Col = 4, IsValid = true };
            case '=': return new KeyPosition { Row = 6, Col = 5, IsValid = true };
            case '^': return new KeyPosition { Row = 6, Col = 6, IsValid = true };
            //case 'ARROW_UP': return new KeyPosition { Row = 6, Col = 6, IsValid = true };
            case '/': return new KeyPosition { Row = 6, Col = 7, IsValid = true };

            // Row 7
            case '1': return new KeyPosition { Row = 7, Col = 0, IsValid = true };
            //case 'ARROW_LEFT': case '←': return new KeyPosition { Row = 7, Col = 1, IsValid = true };
            //case 'CTRL': case 'CONTROL': return new KeyPosition { Row = 7, Col = 2, IsValid = true };
            case '2': return new KeyPosition { Row = 7, Col = 3, IsValid = true };
            case ' ': return new KeyPosition { Row = 7, Col = 4, IsValid = true };
            //case 'COMMODORE': case 'C=': return new KeyPosition { Row = 7, Col = 5, IsValid = true };
            case 'Q': return new KeyPosition { Row = 7, Col = 6, IsValid = true };
            //case 'RUN': case 'STOP': case 'RUN/STOP': return new KeyPosition { Row = 7, Col = 7, IsValid = true };

            // Function key combinations (these would need special handling)
            //case 'F2': return new KeyPosition { Row = 0, Col = 4, IsValid = true }; // F1 + SHIFT
            //case 'F4': return new KeyPosition { Row = 0, Col = 5, IsValid = true }; // F3 + SHIFT
            //case 'F6': return new KeyPosition { Row = 0, Col = 6, IsValid = true }; // F5 + SHIFT
            //case 'F8': return new KeyPosition { Row = 0, Col = 3, IsValid = true }; // F7 + SHIFT

            default: return KeyPosition.Invalid;
        }
    }

    byte port1A;
    byte port1B;
    byte port2A;
    byte port2B;

    public byte GetCIAPort(byte id, char port)
    {
        if (id == 1)
        {
            if (port == 'A')
            {
                return port1A;
            }
            else if (port == 'B')
            {
                //return port1B;
                return GetKeyboardState(port1A);
            }
        }
        else if (id == 2)
        {
            if (port == 'A')
            {
                return port2A;
            }
            else if (port == 'B')
            {
                return port2B;
            }
        }
        return 0xff;
    }

    public void SetCIAPort(byte id, char port, byte value)
    {
        if (id == 1)
        {
            if (port == 'A')
            {
                port1A = value;
            }
            else if (port == 'B')
            {
                port1B = value;
            }
        }
        else if (id == 2)
        {
            if (port == 'A')
            {
                port2A = value;
            }
            else if (port == 'B')
            {
                port2B = value;
            }
        }
    }
}
