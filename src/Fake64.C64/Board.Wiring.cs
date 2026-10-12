using Fake64.Mos;

namespace Fake64.C64;

// Il cablaggio della scheda: cosa è collegato a quale pin di ciascuno zoccolo
public partial class Board
{
    public Socket<I6510Pinout> CpuSocket { get; private set; } = null!;   // MOS 6510, DIP-40
    public Socket<I6569Pinout> VicSocket { get; private set; } = null!;   // MOS 6569 VIC-II, DIP-40
    public Socket<I6581Pinout> SidSocket { get; private set; } = null!;   // MOS 6581 SID, DIP-28
    public Socket<I6526Pinout> Cia1Socket { get; private set; } = null!;  // MOS 6526 CIA 1 ($DC00), DIP-40
    public Socket<I6526Pinout> Cia2Socket { get; private set; } = null!;  // MOS 6526 CIA 2 ($DD00), DIP-40

    // Chip attualmente negli zoccoli (null = zoccolo vuoto), letti a ogni ciclo
    I6510Pinout? cpu;
    I6569Pinout? vic;
    I6581Pinout? sid;
    I6526Pinout? cia1;
    I6526Pinout? cia2;

    // /IRQ della CPU: VIC-II e CIA 1. /NMI: CIA 2 (il tasto RESTORE non è ancora modellato)
    OpenCollectorLine irqLine = null!;
    OpenCollectorLine nmiLine = null!;

    internal bool IrqLine => irqLine.IsLow;
    internal bool NmiLine => nmiLine.IsLow;

    readonly CpuBus cpuBus;
    readonly VicBus vicBus;

    void CreateSockets()
    {
        irqLine = new OpenCollectorLine("/IRQ", () => vic?.Irq == true, () => cia1?.Irq == true);
        nmiLine = new OpenCollectorLine("/NMI", () => cia2?.Irq == true);

        CpuSocket = new Socket<I6510Pinout>("CPU", ConnectCpu, DisconnectCpu);
        VicSocket = new Socket<I6569Pinout>("VIC-II", ConnectVic, DisconnectVic);
        SidSocket = new Socket<I6581Pinout>("SID", chip => sid = chip, _ => sid = null);
        Cia1Socket = new Socket<I6526Pinout>("CIA 1", ConnectCia1, DisconnectCia1);
        Cia2Socket = new Socket<I6526Pinout>("CIA 2", ConnectCia2, DisconnectCia2);
    }

    // CPU: bus indirizzi/dati attraverso il PLA, /IRQ, /NMI, RDY da BA e AEC da AEC del VIC-II,
    // P0-P2 (LORAM, HIRAM, CHAREN) al PLA; P3-P5 al registratore (non collegato: pull-up)
    void ConnectCpu(I6510Pinout chip)
    {
        chip.Bus = cpuBus;
        chip.IrqInput = () => irqLine.IsLow;
        chip.NmiInput = () => nmiLine.IsLow;
        chip.RdyInput = () => vic?.BusAvailable ?? true;
        chip.AecInput = () => vic?.AddressEnable ?? true;
        chip.Port.External = () => 0xFF;
        chip.Port.Changed += MemoryMap;
        cpu = chip;
        MemoryMap();
    }

    void DisconnectCpu(I6510Pinout chip)
    {
        chip.Port.Changed -= MemoryMap;
        chip.Bus = null;
        cpu = null;
        MemoryMap();
    }

    // VIC-II: bus video (con il banco da CIA 2 e la ROM caratteri), /IRQ, BA, uscita video
    void ConnectVic(I6569Pinout chip)
    {
        chip.Memory = vicBus;
        chip.FrameCompleted = OnFrameCompleted;
        vic = chip;
    }

    void DisconnectVic(I6569Pinout chip)
    {
        chip.Memory = null;
        chip.FrameCompleted = null;
        vic = null;
    }

    // CIA 1: porta A alle righe della tastiera, porta B alle colonne (il joystick non è ancora collegato)
    void ConnectCia1(I6526Pinout chip)
    {
        chip.PortA.External = () => 0xFF;
        chip.PortB.External = () => keyboard.GetKeyboardState(chip.PortA.Pins);
        cia1 = chip;
    }

    void DisconnectCia1(I6526Pinout chip)
    {
        chip.PortB.External = () => 0xFF;
        cia1 = null;
    }

    // CIA 2: PA0-PA1 = banco del VIC-II (invertiti), PA2 = user port, PA3-PA5 = ATN/CLK/DATA in uscita
    // sul bus seriale, PA6-PA7 = CLK/DATA in ingresso. Senza drive collegati le linee seriali sono
    // pilotate solo dal C64: le uscite passano per un invertitore, quindi un'uscita a 1 tiene la linea bassa
    void ConnectCia2(I6526Pinout chip)
    {
        chip.PortA.External = () =>
        {
            byte output = chip.PortA.Output;
            byte pins = 0x3F;                              // PA0-PA5: pull-up
            if ((output & 0x10) == 0) pins |= 0x40;        // CLK IN: alta se CLK OUT non la tira giù
            if ((output & 0x20) == 0) pins |= 0x80;        // DATA IN: alta se DATA OUT non la tira giù
            return pins;
        };
        chip.PortB.External = () => 0xFF;
        chip.PortA.Changed += UpdateVicBank;
        cia2 = chip;
        UpdateVicBank();
    }

    void DisconnectCia2(I6526Pinout chip)
    {
        chip.PortA.Changed -= UpdateVicBank;
        chip.PortA.External = () => 0xFF;
        cia2 = null;
        UpdateVicBank();
    }
}
