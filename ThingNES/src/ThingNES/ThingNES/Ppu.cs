namespace ThingNES;

// Dot-stepped background/sprite pipelines; overflow quirks and register races remain simplified.
public sealed partial class Ppu(Cartridge cartridge)
{
    readonly byte[] nametables = new byte[2048], palette = new byte[32];
    public byte[] Oam { get; } = new byte[256];
    byte control, mask, status, oamAddress, latch, buffer;
    ushort address, temporary;
    bool secondWrite;
    public int Scanline { get; private set; }
    public int Dot { get; private set; }
    public long Frames { get; private set; }
    public bool Nmi => (status & 0x80) != 0 && (control & 0x80) != 0;

    public void Tick()
    {
        if (Scanline == 241 && Dot == 1) status |= 0x80;
        if (Scanline == 261 && Dot == 1) status &= 0x1f;
        if (Scanline < 240 && Dot == 0) BeginSpriteLine();
        if (RenderingEnabled && RenderingLine) ClockSprites();
        if (RenderingEnabled && RenderingLine && (Dot is >= 2 and <= 257 or >= 322 and <= 337)) ShiftBackground();
        if (Scanline < 240 && Dot is >= 1 and <= 256) RenderPixel(Dot - 1, Scanline);
        if (RenderingEnabled && RenderingLine)
        {
            if (Dot is >= 1 and <= 256 or >= 321 and <= 336) FetchBackground();
            if (Dot == 256) IncrementY();
            if (Dot == 257) { LoadBackground(); TransferX(); }
            if (Scanline == 261 && Dot is >= 280 and <= 304) TransferY();
            if (Dot is 337 or 339) { if (Dot == 337) LoadBackground(); nextTile = ReadMemory(0x2000 | (address & 0x0fff)); }
        }
        if (Scanline == 261 && Dot == 339 && (Frames & 1) != 0 && (mask & 0x18) != 0)
        { Dot = 0; Scanline = 0; Frames++; return; }
        if (++Dot == 341) { Dot = 0; if (++Scanline == 262) { Scanline = 0; Frames++; } }
    }
    public byte ReadRegister(int register)
    {
        byte result = latch;
        switch (register & 7)
        {
            case 2: result = (byte)((status & 0xe0) | (latch & 31)); status &= 0x7f; secondWrite = false; break;
            case 4: result = Oam[oamAddress]; break;
            case 7:
                int a = address & 0x3fff;
                result = a >= 0x3f00 ? (byte)((ReadMemory(a) & 63) | (latch & 0xc0)) : buffer;
                buffer = ReadMemory(a >= 0x3f00 ? a - 0x1000 : a);
                Increment(); break;
        }
        latch = result;
        return result;
    }
    public void WriteRegister(int register, byte value)
    {
        latch = value;
        switch (register & 7)
        {
            case 0: control = value; temporary = (ushort)((temporary & ~0x0c00) | ((value & 3) << 10)); break;
            case 1: mask = value; break;
            case 3: oamAddress = value; break;
            case 4: Oam[oamAddress++] = value; break;
            case 5:
                if (!secondWrite)
                { fineX = (byte)(value & 7); temporary = (ushort)((temporary & ~31) | (value >> 3)); }
                else
                { temporary = (ushort)((temporary & ~0x73e0) | ((value & 7) << 12) | ((value & 0xf8) << 2)); }
                secondWrite = !secondWrite; break;
            case 6:
                temporary = !secondWrite ? (ushort)((temporary & 255) | ((value & 63) << 8)) : (ushort)((temporary & 0x7f00) | value);
                if (secondWrite) address = temporary;
                secondWrite = !secondWrite; break;
            case 7: WriteMemory(address & 0x3fff, value); Increment(); break;
        }
    }
    void Increment()
    {
        if (RenderingEnabled && RenderingLine) { IncrementX(); IncrementY(); }
        else address = (ushort)((address + ((control & 4) != 0 ? 32 : 1)) & 0x7fff);
    }
    int NametableIndex(int a)
    {
        int offset = (a - 0x2000) & 0xfff, table = offset >> 10;
        int physical = cartridge.Mirroring == Mirroring.Vertical ? table & 1 : table >> 1;
        return physical * 1024 + (offset & 1023);
    }
    static int PaletteIndex(int a) { int i = a & 31; return (i & 0x13) == 0x10 ? i - 16 : i; }
    public byte ReadMemory(int a)
    {
        a &= 0x3fff;
        return a < 0x2000 ? cartridge.ReadChr(a) : a < 0x3f00 ? nametables[NametableIndex(a)] : palette[PaletteIndex(a)];
    }
    public void WriteMemory(int a, byte value)
    {
        a &= 0x3fff;
        if (a < 0x2000) cartridge.WriteChr(a, value);
        else if (a < 0x3f00) nametables[NametableIndex(a)] = value;
        else palette[PaletteIndex(a)] = (byte)(value & 63);
    }
}
