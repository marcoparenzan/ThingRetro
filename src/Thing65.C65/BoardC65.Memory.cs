using Thing65.Mos;

namespace Thing65.C65;

/*
    Physical memory (1 MB, 20 address bits):
      $00000-$1FFFF  128 KB of RAM (the last 2 KB, $1F800, are the Color RAM); $0000/$0001 are the 4510's port
      $20000-$3FFFF  128 KB of system ROM
      $40000-$FFFFF  cartridge and RAM expansion: not fitted (reads $FF, writes are lost)

    What the CPU puts on the bus is decoded like this:
    - A block mapped by the 4510 (MAP) is already a physical address: the board uses it as it is.
    - An unmapped address is a 16-bit one and the chips of the board decide what is there:
        $0000-$7FFF  RAM
        $8000  ROM $38000 if ROM@8000 (control register A of the VIC-III, bit 3), otherwise RAM
        $A000  ROM $3A000 if ROM@A000 (bit 4); otherwise the C64 BASIC ($2A000) if LORAM and HIRAM; otherwise RAM
        $C000  ROM $2C000 if ROM@C000 (bit 5), otherwise RAM
        $D000  I/O if CHAREN and (LORAM or HIRAM); otherwise the C64 character ROM ($2D000) if LORAM or HIRAM; otherwise RAM
        $E000  ROM $3E000 if ROM@E000 (bit 7); otherwise the C64 KERNAL ($2E000) if HIRAM; otherwise RAM
      LORAM, HIRAM and CHAREN are the pins P0-P2 of the 4510's port (the memory map of the C64 mode).
    Writes to a ROM area go to the RAM underneath, except in the physical ROM space, where they are lost.
    Reference: "C65 Specifications", chapter 1.5
 */
public partial class BoardC65
{
    const int ROM_BASE = 0x20000;

    // ---- Physical memory --------------------------------------------------------------------------------
    byte ReadPhysical(int address)
    {
        address &= 0xFFFFF;
        if (address < 2) return address == 0 ? cpuPort.Direction : cpuPort.Pins;
        if (address < ROM_BASE) return ram[address];
        if (address < ROM_BASE + 0x20000) return rom[address - ROM_BASE];
        return 0xFF;
    }

    void WritePhysical(int address, byte value)
    {
        address &= 0xFFFFF;
        if (address < 2)
        {
            if (address == 0) cpuPort.Direction = value; else cpuPort.Latch = value;
            ram[address] = value;
        }
        else if (address < ROM_BASE) ram[address] = value;
    }

    // ---- CPU bus ----------------------------------------------------------------------------------------
    sealed class CpuBus(BoardC65 board) : IMappedBus
    {
        public byte Read(int address, bool mapped) => mapped ? board.ReadPhysical(address) : board.ReadUnmapped((ushort)address);
        public void Write(int address, bool mapped, byte value)
        {
            if (mapped) board.WritePhysical(address, value); else board.WriteUnmapped((ushort)address, value);
        }
    }

    // Pins P0-P2 of the 4510's port as the memory decoder sees them
    int PortBits => cpuPort.Pins & 7;
    byte ControlA => vic?.ControlA ?? 0;

    bool IoVisible => (PortBits & 4) != 0 && (PortBits & 3) != 0;

    byte ReadUnmapped(ushort address)
    {
        byte control = ControlA;
        int bits = PortBits;
        switch (address >> 12)
        {
            case 0x8:
            case 0x9:
                if ((control & 0x08) != 0) return rom[0x18000 + address - 0x8000];
                break;
            case 0xA:
            case 0xB:
                if ((control & 0x10) != 0) return rom[0x1A000 + address - 0xA000];
                if ((bits & 3) == 3) return rom[0x0A000 + address - 0xA000];
                break;
            case 0xC:
                if ((control & 0x20) != 0) return rom[0x0C000 + address - 0xC000];
                break;
            case 0xD:
                if (IoVisible) return ReadIo(address);
                if ((bits & 3) != 0) return rom[0x0D000 + address - 0xD000];
                break;
            case 0xE:
            case 0xF:
                if ((control & 0x80) != 0) return rom[0x1E000 + address - 0xE000];
                if ((bits & 3) > 1) return rom[0x0E000 + address - 0xE000];
                break;
        }
        return ReadPhysical(address);
    }

    void WriteUnmapped(ushort address, byte value)
    {
        byte control = ControlA;
        switch (address >> 12)
        {
            case 0x8:
            case 0x9:
                if ((control & 0x08) != 0) return;
                break;
            case 0xA:
            case 0xB:
                if ((control & 0x10) != 0) return;
                break;
            case 0xC:
                if ((control & 0x20) != 0) return;
                break;
            case 0xD:
                if (IoVisible)
                {
                    WriteIo(address, value);
                    return;
                }
                break;
            case 0xE:
            case 0xF:
                if ((control & 0x80) != 0) return;
                break;
        }
        WritePhysical(address, value);
    }

    // ---- I/O area ($D000-$DFFF) -------------------------------------------------------------------------
    // In VIC-II mode (after reset, C64 mode) it is the C64's: VIC-II registers every 64 bytes up to $D3FF, SID
    // mirrors up to $D7FF, Color RAM, CIAs. In VIC-III mode: VIC-III $D000-$D07F, disk controller $D080,
    // RAM expansion $D0A0, palette $D100-$D3FF, SIDs $D400/$D420, UART $D600, DMAgic $D700.
    // With CRAM@DC00 (control register A, bit 0) the 2 KB of Color RAM replace the CIAs at $DC00-$DFFF
    bool NewMode => vic?.NewMode ?? false;
    bool ColorRamAtDc00 => (ControlA & 0x01) != 0;

    byte ReadIo(ushort address)
    {
        int offset = address & 0xFFF;
        if (offset >= 0x800) return ReadIoUpper(offset);

        if (offset < 0x400)
        {
            if (!NewMode || offset < 0x80) return vic?.Read(NewMode ? offset : offset & 0x3F) ?? 0xFF;
            if (offset < 0xA0) return fdc?.Read(offset & 0x0F) ?? 0xFF;
            return 0xFF; // RAM expansion controller and palettes are not readable
        }

        // $D400-$D7FF
        if (NewMode && offset >= 0x600)
        {
            if (offset >= 0x700 && offset < 0x710) return dma?.Read(offset & 3) ?? 0xFF;
            return 0xFF; // UART and the rest of the C65 registers
        }
        return (offset & 0x20) == 0 ? sidRight?.Read(offset & 0x1F) ?? 0xFF : sidLeft?.Read(offset & 0x1F) ?? 0xFF;
    }

    byte ReadIoUpper(int offset)
    {
        if (offset < 0xC00 || ColorRamAtDc00) return ram[0x1F800 + (offset - 0x800)];
        return offset switch
        {
            < 0xD00 => cia1?.Read(offset & 0x0F) ?? 0xFF,
            < 0xE00 => cia2?.Read(offset & 0x0F) ?? 0xFF,
            _ => 0xFF, // I/O 1 and I/O 2: external
        };
    }

    void WriteIo(ushort address, byte value)
    {
        int offset = address & 0xFFF;
        if (offset >= 0x800)
        {
            WriteIoUpper(offset, value);
            return;
        }

        if (offset < 0x400)
        {
            if (!NewMode || offset < 0x80) vic?.Write(NewMode ? offset : offset & 0x3F, value);
            else if (offset < 0xA0) fdc?.Write(offset & 0x0F, value);
            else if (offset >= 0x100) vic?.WritePalette((offset >> 8) - 1, offset & 0xFF, value);
            return;
        }

        if (NewMode && offset >= 0x600)
        {
            if (offset >= 0x700 && offset < 0x710) dma?.Write(offset & 3, value);
            return;
        }
        if ((offset & 0x20) == 0) sidRight?.Write(offset & 0x1F, value); else sidLeft?.Write(offset & 0x1F, value);
    }

    void WriteIoUpper(int offset, byte value)
    {
        if (offset < 0xC00 || ColorRamAtDc00) ram[0x1F800 + (offset - 0x800)] = value;
        else if (offset < 0xD00) cia1?.Write(offset & 0x0F, value);
        else if (offset < 0xE00) cia2?.Write(offset & 0x0F, value);
    }

    // ---- DMAgic -----------------------------------------------------------------------------------------
    // Absolute addresses; the I/O bit selects the I/O space ($D000-$DFFF) instead of the memory
    sealed class DmaBus(BoardC65 board) : IDmaMemory
    {
        public byte Read(int address, bool io) => io ? board.ReadIo((ushort)(0xD000 | address & 0xFFF)) : board.ReadPhysical(address);

        public void Write(int address, bool io, byte value)
        {
            if (io) board.WriteIo((ushort)(0xD000 | address & 0xFFF), value); else board.WritePhysical(address, value);
        }
    }

    // ---- VIC-III ----------------------------------------------------------------------------------------
    // 16 KB bank from PA0/PA1 of CIA 2, inverted. The VIC sees RAM of bank 0 (the first 64 KB) and the character ROM
    // at $1000-$1FFF of banks 0 and 2: the C64 set ($2D000), or the C65 one ($29000) with CROM@9000
    int vicBank;

    void UpdateVicBank() => vicBank = 3 - ((cia2?.PortA.Pins ?? 0xFF) & 0x03);

    internal int VicBank => vicBank;

    sealed class VicBus(BoardC65 board) : IVicMemory
    {
        public byte Read(int address)
        {
            address &= 0x3FFF;
            if ((board.vicBank & 1) == 0 && (address & 0x3000) == 0x1000)
            {
                int charset = (board.ControlA & 0x40) != 0 ? 0x09000 : 0x0D000;
                return board.rom[charset + (address & 0x0FFF)];
            }
            return board.ram[board.vicBank << 14 | address];
        }

        public byte ReadColor(int address) => board.ram[0x1F800 + (address & 0x7FF)];
    }

    // ---- For tools and tests ----------------------------------------------------------------------------
    // What the CPU reads at a 16-bit address when the 4510 has not mapped it
    public byte CpuRead(ushort address) => ReadUnmapped(address);
    public void CpuWrite(ushort address, byte value) => WriteUnmapped(address, value);

    // Physical memory ($00000-$FFFFF), bypassing the CPU: also the system ROM
    public byte Physical(int address) => ReadPhysical(address);

    // The bus the CPU is wired to (20-bit addresses and MAP/)
    internal IMappedBus Bus => cpuBus;
}
