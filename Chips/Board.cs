using System.Drawing;

namespace Chips;

public partial class Board
{
    MOS6510 cpu;
    MOS6569 vicii;
    MOS6581 sid;
    MOS6526 cia1;
    MOS6526 cia2;
    ColorRamChip colorRam;

    object bus_lock = new object();

    public byte Chargen(ushort addr)
    {
        lock (bus_lock)
        {
            return chargen[addr];
        }
    }

    /*
        Bits #0-#2: Configuration for memory areas $A000-$BFFF, $D000-$DFFF and $E000-$FFFF. Values:
        %x00: RAM visible in all three areas.
        %x01: RAM visible at $A000-$BFFF and $E000-$FFFF.
        %x10: RAM visible at $A000-$BFFF; KERNAL ROM visible at $E000-$FFFF.
        %x11: BASIC ROM visible at $A000-$BFFF; KERNAL ROM visible at $E000-$FFFF.
        %0xx: Character ROM visible at $D000-$DFFF. (Except for the value %000, see above.)
        %1xx: I/O area visible at $D000-$DFFF. (Except for the value %100, see above.)
     */

    bool RamVisibleAllThreeAreas => ((cpu.Address(0x01) & (byte) 0b00000011) == 0b00000000);
    bool KernalRomVisible => ((cpu.Address(0x01) & (byte)0b00000010) == 0b00000010);
    bool BasicRomVisible => ((cpu.Address(0x01) & (byte)0b00000011) == 0b00000011);

    bool CharacterRomVisible => ((cpu.Address(0x01) & (byte) 0b00000100) == 0b00000000) && !RamVisibleAllThreeAreas;
    bool IOAreaVisible => ((cpu.Address(0x01) & (byte)0b00000100) == 0b00000100) && !RamVisibleAllThreeAreas;

    public byte Address(ushort addr)
    {
        lock (bus_lock)
        { 
            if (addr >= kernal_base)
            {
                if (KernalRomVisible)
                    return kernal[addr - kernal_base];
                else
                    return ram[addr >> addr_shift].Address((ushort)(addr & addr_mask));
            }
            else if (addr >= chargen_base)
            {
                if (CharacterRomVisible)
                {
                    return chargen[addr - chargen_base];
                }
                else if (IOAreaVisible)
                {
                    if (addr >= 0xdf00) // I/O 2
                    {
                        return ram[addr >> addr_shift].Address((ushort)(addr & addr_mask));
                    }
                    else if (addr >= 0xde00) // I/O 1
                    {
                        return ram[addr >> addr_shift].Address((ushort)(addr & addr_mask));
                    }
                    if (addr >= 0xdd00) // CIA 2
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
                    return ram[addr >> addr_shift].Address((ushort)(addr & addr_mask));
            }
            else if (addr >= 0xc000)
            {
                return ram[addr >> addr_shift].Address((ushort)(addr & addr_mask));
            }
            else if (addr >= basic_base)
            {
                if (BasicRomVisible)
                    return basic[addr - basic_base];
                else
                    return ram[addr >> addr_shift].Address((ushort)(addr & addr_mask));
            }
            else
            {
                return ram[addr >> addr_shift].Address((ushort)(addr & addr_mask));
            }
        }
    }

    public void Address(ushort addr, byte value)
    {
        lock (bus_lock)
        {
            if (addr >= kernal_base)
            {
                ram[addr >> addr_shift].Address((ushort)(addr & addr_mask), value);
            }
            else if (addr >= chargen_base)
            {
                if (CharacterRomVisible)
                {
                    ram[addr >> addr_shift].Address((ushort)(addr & addr_mask), value);
                }
                else if (IOAreaVisible)
                {
                    if (addr >= 0xdf00) // I/O 2
                    {
                        ram[addr >> addr_shift].Address((ushort)(addr & addr_mask), value);
                    }
                    else if (addr >= 0xde00) // I/O 1
                    {
                        ram[addr >> addr_shift].Address((ushort)(addr & addr_mask), value);
                    }
                    if (addr >= 0xdd00) // CIA 2
                    {
                        cia2.Address((ushort)(addr & 0x00ff), value);
                    }
                    else if (addr >= 0xdc00) // CIA 1
                    {
                        cia1.Address((ushort)(addr & 0x00ff), value);
                    }
                    else if (addr >= 0xd800) // Color RAM
                    {
                        colorRam.Address((ushort)(addr & 0x03ff), value);
                    }
                    else if (addr >= 0xd400) // SID
                    {
                        sid.Address((ushort)(addr & 0x03ff), value);
                    }
                    else
                    {
                        vicii.Address((ushort)(addr & 0x03ff), value);
                    }
                }
                else
                {
                    ram[addr >> addr_shift].Address((ushort)(addr & addr_mask), value);
                }
            }
            else if (addr >= 0xc000)
            {
                ram[addr >> addr_shift].Address((ushort)(addr & addr_mask), value);
            }
            else if (addr >= basic_base)
            {
                ram[addr >> addr_shift].Address((ushort)(addr & addr_mask), value);
            }
            else
            {
                ram[addr >> addr_shift].Address((ushort)(addr & addr_mask), value);
            }
        }
    }

    public void Raster(Bitmap bitmap, Rectangle cr)
    {
        vicii.Raster(bitmap, cr);
    }

    const ushort kernal_base = 0xE000;
    byte[] kernal;
    const ushort chargen_base = 0xD000;
    byte[] chargen;
    const ushort basic_base = 0xA000;
    byte[] basic;

    public Board()
    {
        kernal = File.ReadAllBytes(nameof(kernal));
        chargen = File.ReadAllBytes(nameof(chargen));
        basic = File.ReadAllBytes(nameof(basic));
        cpu = new MOS6510(this);
        vicii = new MOS6569(this);
        colorRam = new ColorRamChip(this);
        sid = new MOS6581(this);
        cia1 = new MOS6526(this);
        cia2 = new MOS6526(this);
    }

    const byte addr_shift = 13;
    const ushort addr_mask = 0x1fff;

    Ram8KbChip[] ram = [
        new(),
        new(),
        new(),
        new(),
        new(),
        new(),
        new(),
        new()
    ];
}
