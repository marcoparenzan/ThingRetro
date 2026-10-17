using Thing64.Mos;

namespace Thing64.C64;

// The board's wiring: what is connected to which pin of each socket
public partial class Board
{
    public Socket<I6510Pinout> CpuSocket { get; private set; } = null!;   // MOS 6510, DIP-40
    public Socket<I6569Pinout> VicSocket { get; private set; } = null!;   // MOS 6569 VIC-II, DIP-40
    public Socket<I6581Pinout> SidSocket { get; private set; } = null!;   // MOS 6581 SID, DIP-28
    public Socket<I6526Pinout> Cia1Socket { get; private set; } = null!;  // MOS 6526 CIA 1 ($DC00), DIP-40
    public Socket<I6526Pinout> Cia2Socket { get; private set; } = null!;  // MOS 6526 CIA 2 ($DD00), DIP-40

    // Chips currently in the sockets (null = empty socket), read at every cycle
    I6510Pinout? cpu;
    I6569Pinout? vic;
    I6581Pinout? sid;
    I6526Pinout? cia1;
    I6526Pinout? cia2;

    // CPU /IRQ: VIC-II and CIA 1. /NMI: CIA 2 (the RESTORE key is not modelled yet)
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
        SidSocket = new Socket<I6581Pinout>("SID", ConnectSid, DisconnectSid);
        Cia1Socket = new Socket<I6526Pinout>("CIA 1", ConnectCia1, DisconnectCia1);
        Cia2Socket = new Socket<I6526Pinout>("CIA 2", ConnectCia2, DisconnectCia2);
    }

    // CPU: address/data bus through the PLA, /IRQ, /NMI, RDY from BA and AEC from AEC of the VIC-II,
    // P0-P2 (LORAM, HIRAM, CHAREN) to the PLA; P3-P5 to the datasette (not connected: pull-ups)
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

    // VIC-II: video bus (with the bank from CIA 2 and the character ROM), /IRQ, BA, video output
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

    // CIA 1: port A to the keyboard rows and to the contacts of control port 2, port B to the keyboard
    // columns and to the contacts of control port 1. Closed contacts pull the lines to ground even when they
    // are outputs: that is why a joystick in port 1 "presses" phantom keys during the keyboard scan, and
    // almost every game uses port 2
    void ConnectCia1(I6526Pinout chip)
    {
        chip.PortA.External = () => ControlPort2.DigitalLines;
        chip.PortB.External = () => (byte)(keyboard.GetKeyboardState(chip.PortA.Pins) & ControlPort1.DigitalLines);
        cia1 = chip;
    }

    void DisconnectCia1(I6526Pinout chip)
    {
        chip.PortA.External = () => 0xFF;
        chip.PortB.External = () => 0xFF;
        cia1 = null;
    }

    // SID: POT X and POT Y come from the control ports through an analog switch (4066) driven by
    // PA6 (port 1) and PA7 (port 2) of CIA 1. With no port selected the lines are floating
    void ConnectSid(I6581Pinout chip)
    {
        chip.PotXInput = () => SelectedPotPort()?.PotX ?? 0xFF;
        chip.PotYInput = () => SelectedPotPort()?.PotY ?? 0xFF;
        sid = chip;
    }

    void DisconnectSid(I6581Pinout chip)
    {
        chip.PotXInput = () => 0xFF;
        chip.PotYInput = () => 0xFF;
        sid = null;
    }

    ControlPort? SelectedPotPort()
    {
        byte select = cia1?.PortA.Pins ?? 0xFF;
        if ((select & 0x40) != 0) return ControlPort1;
        if ((select & 0x80) != 0) return ControlPort2;
        return null;
    }

    // CIA 2: PA0-PA1 = VIC-II bank (inverted), PA2 = user port, PA3-PA5 = ATN/CLK/DATA out on the serial bus,
    // PA6-PA7 = CLK/DATA in. With no drive connected the serial lines are driven by the C64 only: the outputs
    // go through an inverter, so an output at 1 holds the line low
    void ConnectCia2(I6526Pinout chip)
    {
        // PA3-PA5 go through inverting drivers to the IEC connector (Iec): a 1 pulls the line down. A cable takes the
        // lines to the drives; without one only the outputs drive the lines
        Iec.AtnLow = () => (chip.PortA.Output & 0x08) != 0;
        Iec.ClkLow = () => (chip.PortA.Output & 0x10) != 0;
        Iec.DataLow = () => (chip.PortA.Output & 0x20) != 0;
        chip.PortA.External = () =>
        {
            byte pins = 0x3F;                                                // PA0-PA5: pull-up
            if (!Iec.ClkLow() && !Iec.ClkInputLow()) pins |= 0x40;           // CLK IN: high if nobody pulls the line down
            if (!Iec.DataLow() && !Iec.DataInputLow()) pins |= 0x80;         // DATA IN
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
        Iec.AtnLow = Iec.ClkLow = Iec.DataLow = () => false;
        chip.PortA.External = () => 0xFF;
        cia2 = null;
        UpdateVicBank();
    }
}
