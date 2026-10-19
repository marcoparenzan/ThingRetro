namespace ThingCommodore.Graphics;

// The memory of a running C64, as Board.CaptureMemory() hands it over: the 64 KB of RAM (the RAM under the ROMs
// and the I/O, not what the CPU would read), the 1024 nibbles of Color RAM, the VIC-II registers $D000-$D03F, the
// 4 KB of the character ROM, and the VIC-II bank and the CPU port. A viewer can then show any part of it as bytes,
// characters, a screen, a charset, sprites or a bitmap
public sealed class MemoryCapture
{
    public const int RamSize = 65536, ColorSize = 1024, RegistersSize = 64, CharsetRomSize = 4096, RomSize = 8192;
    // RAM, Color RAM, VIC-II registers, character ROM, VIC-II bank, CPU port, then BASIC and KERNAL ROMs
    public const int Size = RamSize + ColorSize + RegistersSize + CharsetRomSize + 2 + 2 * RomSize;

    const int ColorAt = RamSize, RegistersAt = ColorAt + ColorSize, CharsetAt = RegistersAt + RegistersSize, BankAt = CharsetAt + CharsetRomSize;
    const int BasicAt = BankAt + 2, KernalAt = BasicAt + RomSize;

    readonly byte[] data;

    public MemoryCapture(byte[] data)
    {
        if (data.Length != Size) throw new FormatException($"A memory capture is {Size} bytes, not {data.Length}");
        this.data = data;
        // Slices are made once: a viewer reads them byte by byte
        Ram = data[..RamSize];
        ColorRam = data[ColorAt..RegistersAt];
        CharsetRom = data[CharsetAt..BankAt];
        BasicRom = data[BasicAt..KernalAt];
        KernalRom = data[KernalAt..];
    }

    // RAM with the addresses wrapping at 64 KB, so that a view can run past the end
    public byte[] Ram { get; }
    public byte[] ColorRam { get; }
    public byte[] CharsetRom { get; }
    public byte[] BasicRom { get; }
    public byte[] KernalRom { get; }

    public byte Register(int register) => data[RegistersAt + register];
    public int VicBank => data[BankAt] & 3;
    public int CpuPort => data[BankAt + 1];

    // What the VIC-II is set to show now ($D018: the screen in steps of 1 KB, the characters in steps of 2 KB, both
    // inside its 16 KB bank)
    public int ScreenAddress => VicBank * 0x4000 + (Register(0x18) >> 4) * 0x400;
    public int CharsetAddress => VicBank * 0x4000 + (Register(0x18) >> 1 & 7) * 0x800;
    public bool CharsetIsRom => (VicBank & 1) == 0 && (CharsetAddress & 0x3FFF) is >= 0x1000 and < 0x2000;
    public bool BitmapMode => (Register(0x11) & 0x20) != 0;
    public int BitmapAddress => VicBank * 0x4000 + (Register(0x18) & 8) * 0x400;
    public bool MulticolorMode => (Register(0x16) & 0x10) != 0;
    public int Border => Register(0x20) & 15;
    public int Background => Register(0x21) & 15;
    public int Multicolor1 => Register(0x22) & 15;
    public int Multicolor2 => Register(0x23) & 15;
    public int SpriteMulticolor1 => Register(0x25) & 15;
    public int SpriteMulticolor2 => Register(0x26) & 15;
    public int SpriteColor(int sprite) => Register(0x27 + sprite) & 15;

    // The bytes of a character as the VIC-II reads them: from the character ROM where it sees it, otherwise from RAM
    public byte CharacterRow(int code, int row)
    {
        int address = CharsetAddress + code * 8 + row;
        return CharsetIsRom ? CharsetRom[(address & 0x0FFF)] : Ram[address & 0xFFFF];
    }

    // The memory as the CPU sees it, with the RAM, BASIC, KERNAL, the character ROM and the I/O area chosen by the CPU port (LORAM,
    // HIRAM and CHAREN, bits 0-2). The I/O chips are not in a capture (reading them has side effects): the VIC-II registers and
    // Color RAM are there, the rest of $D000-$DFFF is -1. Cartridges are not seen
    public int CpuRead(int address)
    {
        address &= 0xFFFF;
        int port = CpuPort;
        bool loram = (port & 1) != 0, hiram = (port & 2) != 0, charen = (port & 4) != 0;
        if (address is >= 0xA000 and < 0xC000 && loram && hiram) return BasicRom[address - 0xA000];
        if (address >= 0xE000 && hiram) return KernalRom[address - 0xE000];
        if (address is >= 0xD000 and < 0xE000 && (loram || hiram))
        {
            if (!charen) return CharsetRom[address - 0xD000];
            if (address < 0xD400) return Register(address & 0x3F);   // the 64 registers repeat through the 1 KB
            if (address is >= 0xD800 and < 0xDC00) return ColorRam[address - 0xD800] & 15;
            return -1;
        }
        return Ram[address];
    }
}
