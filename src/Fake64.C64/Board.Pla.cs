using Fake64.Mos;

namespace Fake64.C64;

/*
    Il PLA decide cosa vede la CPU combinando i pin P0-P2 della 6510 (LORAM, HIRAM, CHAREN)
    con le linee GAME ed EXROM della porta di espansione (attive basse, alte senza cartuccia):

    Senza cartuccia (EXROM = 1, GAME = 1):
      $A000 BASIC se LORAM e HIRAM, $E000 KERNAL se HIRAM,
      $D000 I/O se CHAREN altrimenti ROM caratteri (tutto RAM se LORAM = HIRAM = 0)
    Cartuccia 8K (EXROM = 0, GAME = 1):  in più ROML a $8000 se LORAM e HIRAM
    Cartuccia 16K (EXROM = 0, GAME = 0): in più ROMH a $A000 (al posto del BASIC) se HIRAM
    Ultimax (EXROM = 1, GAME = 0): ROML a $8000, ROMH a $E000, I/O sempre a $D000,
      $1000-$7FFF e $A000-$CFFF scollegati; la porta $01 viene ignorata
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

    // Ingressi del PLA: i pin della porta della CPU (pull-up se lo zoccolo è vuoto) e le linee della cartuccia
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

    // In modalità Ultimax queste aree non sono collegate a niente
    bool IsUnmappedInUltimax(ushort addr) =>
        ultimax && ((addr >= 0x1000 && addr < 0x8000) || (addr >= 0xA000 && addr < 0xD000));

    // La memoria come la vede la CPU, compresi i registri della porta a $0000/$0001
    // (per debugger, test e strumenti esterni; la CPU stessa usa il bus)
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

    // Bus della CPU decodificato dal PLA
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

        // Le scritture nelle aree ROM (BASIC, KERNAL, caratteri, cartuccia) finiscono nella RAM sottostante
        ram64.Address(addr, value);
    }

    // Area I/O: il PLA attiva il /CS di un chip; uno zoccolo vuoto lascia il bus alto ($FF)
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

    // Bus della CPU: A0-A15 e D0-D7 passano per il PLA
    sealed class CpuBus(Board board) : IBus
    {
        public byte Read(ushort address) => board.BusRead(address);
        public void Write(ushort address, byte value) => board.BusWrite(address, value);
    }

    // Banco di 16 KB visto dal VIC-II: A14/A15 arrivano da PA0/PA1 della CIA 2, invertiti.
    // Si ricalcola solo quando la porta cambia (o la CIA 2 viene tolta: i pull-up danno il banco 0)
    int vicBank;

    void UpdateVicBank() => vicBank = 3 - ((cia2?.PortA.Pins ?? 0xFF) & 0x03);

    internal int VicBank => vicBank;

    // Bus video: il VIC-II vede sempre la RAM (mai BASIC, KERNAL o I/O), tranne la ROM caratteri
    // a $1000-$1FFF nei banchi 0 e 2; in Ultimax vede ROMH della cartuccia a $3000-$3FFF
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
