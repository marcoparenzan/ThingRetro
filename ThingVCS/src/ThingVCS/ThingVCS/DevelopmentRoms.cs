namespace ThingVCS;
public static class DevelopmentRoms
{
    /// <summary>Small 6502 test: set MARIA BACKGRND, then loop. Not a game.</summary>
    public static byte[] DirectObjects()
    {
        var rom = new byte[16384];
        byte[] program = [
            0xa9,0x06,0x8d,0x20,0x00,
            0xa9,0x3a,0x8d,0x25,0x00,
            0xa9,0x7a,0x8d,0x26,0x00,
            0xa9,0xba,0x8d,0x27,0x00,
            0xa9,0xc1,0x8d,0x2c,0x00,
            0xa9,0x00,0x8d,0x30,0x00,
            0xa9,0x40,0x8d,0x3c,0x00,
            0x4c,0x23,0xc0];
        program.CopyTo(rom, 0);
        for (int line = 0; line < 242; line++)
        { rom[0x100 + line * 3] = 0; rom[0x101 + line * 3] = 0xc4; rom[0x102 + line * 3] = 0; }
        byte[] list = [0x00,0x3c,0xc5,40,0,0]; list.CopyTo(rom, 0x400);
        byte[] graphics = [0x55,0xaa,0xff,0x1b]; graphics.CopyTo(rom, 0x500);
        rom[0x3ffc] = 0; rom[0x3ffd] = 0xc0; return rom;
    }
    public static byte[] IndirectObjects()
    {
        var rom = DirectObjects();
        // Insert CHARBASE setup before CTRL; loop at the end of the revised program.
        byte[] tail = [0xa9,0xc6,0x8d,0x34,0x00,0xa9,0x50,0x8d,0x3c,0x00,0x4c,0x28,0xc0];
        tail.CopyTo(rom, 30);
        byte[] list = [0,0x60,0xc5,0x3e,40,0,0]; list.CopyTo(rom, 0x400);
        rom[0x500] = 0; rom[0x501] = 2;
        byte[] characters = [0x55,0xaa,0xff,0x1b]; characters.CopyTo(rom, 0x600);
        return rom;
    }
    public static byte[] WriteModeObjects(byte readMode = 0)
    {
        if (readMode > 3) throw new ArgumentOutOfRangeException(nameof(readMode));
        var rom = DirectObjects();
        var program = new List<byte>();
        void Set(byte address, byte value) => program.AddRange([0xa9,value,0x8d,address,0]);
        Set(0x20, 6);
        for (int i = 1; i < 32; i++) if ((i & 3) != 0) Set((byte)(0x20 + i), (byte)((i << 4) | 10));
        Set(0x2c, 0xc1); Set(0x30, 0); Set(0x3c, (byte)(0x40 | readMode));
        int loop = 0xc000 + program.Count;
        program.AddRange([0x4c,(byte)loop,(byte)(loop >> 8)]);
        program.ToArray().CopyTo(rom, 0);
        byte[] list = [0,0xc0,0xc5,0x3c,40,0,0]; list.CopyTo(rom, 0x400);
        return rom;
    }
    public static byte[] DisplayListInterrupts(bool startupDli = true, int activeLines = 242)
    {
        if (activeLines is not (242 or 292)) throw new ArgumentOutOfRangeException(nameof(activeLines));
        var rom = DirectObjects();
        // Relocate graphics/list beyond the longest DLL; PAL entries would overlap $C400.
        byte[] list = [0, 0x3c, 0xc9, 40, 0, 0]; list.CopyTo(rom, 0x800);
        byte[] graphics = [0x55, 0xaa, 0xff, 0x1b]; graphics.CopyTo(rom, 0x900);
        for (int line = 0; line < activeLines; line++)
        {
            rom[0x100 + line * 3] = 0x80;
            rom[0x101 + line * 3] = 0xc8;
            rom[0x102 + line * 3] = 0;
        }
        if (!startupDli) rom[0x100] = 0; // Isolate post-render transitions from the provisional startup edge.
        // NMI handler: INC $1800; RTI. Main code uses no stack writes outside the existing CPU model.
        byte[] handler = [0xee,0x00,0x18,0x40]; handler.CopyTo(rom, 0x600);
        rom[0x3ffa] = 0; rom[0x3ffb] = 0xc6;
        return rom;
    }
    /// <summary>CPU-generated pure tone on channel 0 and poly9 noise on channel 1.</summary>
    public static byte[] TiaSound()
    {
        var rom = new byte[16384];
        byte[] program = [
            0xa9, 0x3a, 0x8d, 0x20, 0x00,
            0xa9, 4, 0x8d, 0x15, 0x00,
            0xa9, 7, 0x8d, 0x17, 0x00,
            0xa9, 15, 0x8d, 0x19, 0x00,
            0xa9, 8, 0x8d, 0x16, 0x00,
            0xa9, 3, 0x8d, 0x18, 0x00,
            0xa9, 6, 0x8d, 0x1a, 0x00,
            0x4c, 0x23, 0xc0];
        program.CopyTo(rom, 0); rom[0x3ffd] = 0xc0;
        return rom;
    }
    public static byte[] Backdrop(byte color = 0x3a)
    {
        var rom = new byte[16384];
        byte[] program = [0xa9, color, 0x8d, 0x20, 0x00, 0x4c, 0x05, 0xc0];
        program.CopyTo(rom, 0);
        rom[0x3ffc] = 0; rom[0x3ffd] = 0xc0;
        return rom;
    }
}
