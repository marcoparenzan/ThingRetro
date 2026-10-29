namespace ThingNES;

public sealed partial class Ppu
{
    ushort patternLow, patternHigh, attributeLow, attributeHigh;
    byte nextTile, nextAttribute, nextLow, nextHigh, fineX;
    public ushort VramAddress => address;
    public ushort TemporaryAddress => temporary;
    public byte FineX => fineX;
    public bool WriteToggle => secondWrite;
    bool RenderingEnabled => (mask & 0x18) != 0;
    bool RenderingLine => Scanline < 240 || Scanline == 261;

    void ShiftBackground()
    {
        patternLow <<= 1; patternHigh <<= 1; attributeLow <<= 1; attributeHigh <<= 1;
    }
    void LoadBackground()
    {
        patternLow = (ushort)((patternLow & 0xff00) | nextLow);
        patternHigh = (ushort)((patternHigh & 0xff00) | nextHigh);
        attributeLow = (ushort)((attributeLow & 0xff00) | ((nextAttribute & 1) != 0 ? 255 : 0));
        attributeHigh = (ushort)((attributeHigh & 0xff00) | ((nextAttribute & 2) != 0 ? 255 : 0));
    }
    void FetchBackground()
    {
        switch (Dot & 7)
        {
            case 1: LoadBackground(); nextTile = ReadMemory(0x2000 | (address & 0x0fff)); break;
            case 3:
                int attr = ReadMemory(0x23c0 | (address & 0x0c00) | ((address >> 4) & 0x38) | ((address >> 2) & 7));
                nextAttribute = (byte)((attr >> (((address >> 4) & 4) | (address & 2))) & 3); break;
            case 5: nextLow = ReadMemory(((control & 0x10) != 0 ? 0x1000 : 0) + nextTile * 16 + ((address >> 12) & 7)); break;
            case 7: nextHigh = ReadMemory(((control & 0x10) != 0 ? 0x1000 : 0) + nextTile * 16 + ((address >> 12) & 7) + 8); break;
            case 0: IncrementX(); break;
        }
    }
    void IncrementX()
    {
        if ((address & 31) == 31) address = (ushort)((address & ~31) ^ 0x0400);
        else address++;
    }
    void IncrementY()
    {
        if ((address & 0x7000) != 0x7000) { address += 0x1000; return; }
        address &= 0x0fff;
        int y = (address >> 5) & 31;
        if (y == 29) { y = 0; address ^= 0x0800; }
        else if (y == 31) y = 0;
        else y++;
        address = (ushort)((address & ~0x03e0) | (y << 5));
    }
    void TransferX() => address = (ushort)((address & ~0x041f) | (temporary & 0x041f));
    void TransferY() => address = (ushort)((address & ~0x7be0) | (temporary & 0x7be0));
}
