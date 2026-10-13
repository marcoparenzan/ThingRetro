using Fake64.Mos;

namespace Fake64.C64;

/*
    The PLA decides what the CPU sees by combining pins P0-P2 of the 6510 (LORAM, HIRAM, CHAREN)
    with the GAME and EXROM lines of the expansion port (active low, high without a cartridge):

    No cartridge (EXROM = 1, GAME = 1):
      $A000 BASIC if LORAM and HIRAM, $E000 KERNAL if HIRAM,
      $D000 I/O if CHAREN, otherwise character ROM (all RAM if LORAM = HIRAM = 0)
    8K cartridge (EXROM = 0, GAME = 1):  plus ROML at $8000 if LORAM and HIRAM
    16K cartridge (EXROM = 0, GAME = 0): plus ROMH at $A000 (instead of BASIC) if HIRAM
    Ultimax (EXROM = 1, GAME = 0): ROML at $8000, ROMH at $E000, I/O always at $D000,
      $1000-$7FFF and $A000-$CFFF unconnected; port $01 is ignored
 */
public partial class Board
{
    bool kernalVisible;
    bool basicRomVisible;
    bool chargenVisible;
    bool ioAreaVisible;
    bool romlVisible;
    bool romhAtA000;
    bool ultimax;

    // PLA inputs: the pins of the CPU port (pull-ups if the socket is empty) and the cartridge lines
    void MemoryMap()
    {
        var state = cpu?.Port.Pins ?? 0xFF;
        bool loram = (state & 0x01) != 0;
        bool hiram = (state & 0x02) != 0;
        bool charen = (state & 0x04) != 0;
        bool exrom = cartridge?.Exrom ?? true;
        bool game = cartridge?.Game ?? true;

        ultimax = exrom && !game;
        if (ultimax)
        {
            romlVisible = true;
            romhAtA000 = false;
            basicRomVisible = false;
            kernalVisible = false;
            chargenVisible = false;
            ioAreaVisible = true;
            return;
        }

        bool allRam = !loram && !hiram;
        romlVisible = !exrom && loram && hiram;
        romhAtA000 = !exrom && !game && hiram;
        basicRomVisible = game && loram && hiram;
        kernalVisible = hiram;
        chargenVisible = !charen && !allRam;
        ioAreaVisible = charen && !allRam;
    }

    // In Ultimax mode these areas are not connected to anything
    bool IsUnmappedInUltimax(ushort addr) =>
        ultimax && ((addr >= 0x1000 && addr < 0x8000) || (addr >= 0xA000 && addr < 0xD000));

    // Memory as the CPU sees it, including the port registers at $0000/$0001
    // (for debuggers, tests and external tools; the CPU itself uses the bus)
    public byte Address(ushort addr)
    {
        if (addr <= 0x0001 && cpu is not null)
            return addr == 0 ? cpu.Port.Direction : cpu.Port.Pins;
        return BusRead(addr);
    }

    public void Address(ushort addr, byte value)
    {
        if (addr <= 0x0001 && cpu is not null)
        {
            if (addr == 0) cpu.Port.Direction = value;
            else cpu.Port.Latch = value;
        }
        BusWrite(addr, value);
    }

    // CPU bus decoded by the PLA
    byte BusRead(ushort addr)
    {
        if (IsUnmappedInUltimax(addr))
            return 0xFF;

        switch (addr >> 12)
        {
            case 0x8:
            case 0x9:
                if (romlVisible) return cartridge!.ReadRoml(addr & 0x1FFF);
                break;
            case 0xA:
            case 0xB:
                if (romhAtA000) return cartridge!.ReadRomh(addr & 0x1FFF);
                if (basicRomVisible) return basic[addr - basic_base];
                break;
            case 0xD:
                if (ioAreaVisible) return ReadIo(addr);
                if (chargenVisible) return chargen[addr - chargen_base];
                break;
            case 0xE:
            case 0xF:
                if (ultimax) return cartridge!.ReadRomh(addr & 0x1FFF);
                if (kernalVisible) return kernal[addr - kernal_base];
                break;
        }
        return ram64.Address(addr);
    }

    void BusWrite(ushort addr, byte value)
    {
        if (IsUnmappedInUltimax(addr))
            return;

        if ((addr >> 12) == 0xD && ioAreaVisible)
        {
            WriteIo(addr, value);
            return;
        }

        // Writes to the ROM areas (BASIC, KERNAL, characters, cartridge) end up in the RAM underneath
        ram64.Address(addr, value);
    }

    // I/O area: the PLA activates the /CS of a chip; an empty socket leaves the bus high ($FF)
    byte ReadIo(ushort addr)
    {
        if (addr < 0xD400) return vic?.Read(addr & 0x3F) ?? 0xFF;
        if (addr < 0xD800) return sid?.Read(addr & 0x1F) ?? 0xFF;
        if (addr < 0xDC00) return colorRam.Address((ushort)(addr & 0x03ff));
        if (addr < 0xDD00) return cia1?.Read(addr & 0x0F) ?? 0xFF;
        if (addr < 0xDE00) return cia2?.Read(addr & 0x0F) ?? 0xFF;
        if (addr < 0xDF00) return cartridge?.ReadIo1(addr & 0xFF) ?? ram64.Address(addr); // I/O 1
        return cartridge?.ReadIo2(addr & 0xFF) ?? ram64.Address(addr);                    // I/O 2
    }

    void WriteIo(ushort addr, byte value)
    {
        if (addr < 0xD400) vic?.Write(addr & 0x3F, value);
        else if (addr < 0xD800) sid?.Write(addr & 0x1F, value);
        else if (addr < 0xDC00) colorRam.Address((ushort)(addr & 0x03ff), value);
        else if (addr < 0xDD00) cia1?.Write(addr & 0x0F, value);
        else if (addr < 0xDE00) cia2?.Write(addr & 0x0F, value);
        else if (addr < 0xDF00) { if (cartridge is null) ram64.Address(addr, value); else cartridge.WriteIo1(addr & 0xFF, value); }
        else { if (cartridge is null) ram64.Address(addr, value); else cartridge.WriteIo2(addr & 0xFF, value); }
    }

    // CPU bus: A0-A15 and D0-D7 go through the PLA
    sealed class CpuBus(Board board) : IBus
    {
        public byte Read(ushort address) => board.BusRead(address);
        public void Write(ushort address, byte value) => board.BusWrite(address, value);
    }

    // 16 KB bank seen by the VIC-II: A14/A15 come from PA0/PA1 of CIA 2, inverted.
    // Recomputed only when the port changes (or CIA 2 is removed: the pull-ups give bank 0)
    int vicBank;

    void UpdateVicBank() => vicBank = 3 - ((cia2?.PortA.Pins ?? 0xFF) & 0x03);

    internal int VicBank => vicBank;

    // Video bus: the VIC-II always sees RAM (never BASIC, KERNAL or I/O), except the character ROM
    // at $1000-$1FFF in banks 0 and 2; in Ultimax it sees the cartridge's ROMH at $3000-$3FFF
    internal byte VicRead(int addr)
    {
        addr &= 0x3FFF;
        if (ultimax)
        {
            if ((addr & 0x3000) == 0x3000) return cartridge!.ReadRomh(0x1000 | addr & 0x0FFF);
        }
        else if ((vicBank & 1) == 0 && (addr & 0x3000) == 0x1000)
            return chargen[addr & 0x0FFF];
        return ram64.Address((ushort)(vicBank << 14 | addr));
    }

    sealed class VicBus(Board board) : IVicMemory
    {
        public byte Read(int address) => board.VicRead(address);
        public byte ReadColor(int address) => board.colorRam.Address((ushort)address);
    }
}
