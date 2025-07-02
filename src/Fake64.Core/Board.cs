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

    Ram8KbChip[] ram;
    Ram64KbChip ram64;

    byte[] Rom(string name) => File.ReadAllBytes(Path.Combine("roms", name));

    public Board()
    {
        kernal = Rom(nameof(kernal));
        chargen = Rom(nameof(chargen));
        basic = Rom(nameof(basic));
        ram = [new(this), new(this), new(this), new(this), new(this), new(this), new(this), new(this)];
        ram64 = new(this);
        colorRam = new ColorRamChip(this);
        cia2 = new MOS6526(this);
        cia1 = new MOS6526(this);
        sid = new MOS6581(this);
        vicii = new MOS6569(this);
        cpu = new MOS6510(this);
        MemoryMap();

        Reset();

        var f = 0.9852;
        var microseconds = (long)(1_000_000 / f); // 1.02 MHz
        var targetTicks = microseconds * Stopwatch.Frequency / 1_000_000;
        //clock = Task.Factory.StartNew(async () =>
        //{
        //    var sw = Stopwatch.StartNew();
        //    sw.Stop();
        //    while (true)
        //    {
        //        sw.Restart();
        //        Clock();
        //        while (sw.ElapsedTicks < targetTicks) { }
        //        sw.Stop();
        //    }
        //});
    }

    void Clock()
    {
        foreach (var r in ram) r.Clock();
        ram64.Clock();
        colorRam.Clock();
        cia2.Clock();
        cia1.Clock();
        sid.Clock();
        vicii.Clock();
        cpu.Clock();
    }

    public void Reset()
    {
        foreach(var r in ram) r.Reset();
        ram64.Reset();
        colorRam.Reset();
        cia2.Reset();
        cia1.Reset();
        sid.Reset();
        vicii.Reset();
        cpu.Reset();
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
        else if (addr <= 0x0001)
        {
            cpu.Address(addr, value);
            MemoryMap();
            return;
        }
        else
        {
        }

        //ram[addr >> addr_shift].Address((ushort)(addr & addr_mask), value);
        ram64.Address(addr, value);
    }

    public void Raster(Bitmap bitmap, Rectangle cr)
    {
        vicii.Mode0(bitmap, cr);
    }

    const ushort kernal_base = 0xE000;
    byte[] kernal;
    const ushort chargen_base = 0xD000;
    byte[] chargen;
    const ushort basic_base = 0xA000;
    byte[] basic;
}
