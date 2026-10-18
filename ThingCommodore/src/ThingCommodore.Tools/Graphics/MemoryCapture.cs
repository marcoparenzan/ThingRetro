namespace ThingCommodore.Graphics;

// The memory of a running C64, as Board.CaptureMemory() hands it over: the 64 KB of RAM (the RAM under the ROMs
// and the I/O, not what the CPU would read), the 1024 nibbles of Color RAM, the VIC-II registers $D000-$D03F, the
// 4 KB of the character ROM, and the VIC-II bank and the CPU port. A viewer can then show any part of it as bytes,
// characters, a screen, a charset, sprites or a bitmap
public sealed class MemoryCapture
{
    public const int RamSize = 65536, ColorSize = 1024, RegistersSize = 64, CharsetRomSize = 4096;
    public const int Size = RamSize + ColorSize + RegistersSize + CharsetRomSize + 2;

    readonly byte[] data;

    public MemoryCapture(byte[] data)
    {
        if (data.Length != Size) throw new FormatException($"A memory capture is {Size} bytes, not {data.Length}");
        this.data = data;
    }

    // RAM with the addresses wrapping at 64 KB, so that a view can run past the end
    public byte[] Ram => data[..RamSize];
    public byte[] ColorRam => data[RamSize..(RamSize + ColorSize)];
    public byte[] CharsetRom => data[(RamSize + ColorSize + RegistersSize)..(RamSize + ColorSize + RegistersSize + CharsetRomSize)];

    public byte Register(int register) => data[RamSize + ColorSize + register];
    public int VicBank => data[Size - 2] & 3;
    public int CpuPort => data[Size - 1];

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
}
