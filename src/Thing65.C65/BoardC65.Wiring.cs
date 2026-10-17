using Thing65.Mos;

namespace Thing65.C65;

// The board's wiring: what is connected to which pin of each socket
public partial class BoardC65
{
    public Socket<I4510Pinout> CpuSocket { get; private set; } = null!;      // CSG 4510
    public Socket<I4567Pinout> VicSocket { get; private set; } = null!;      // CSG 4567 VIC-III
    public Socket<I6581Pinout> SidRightSocket { get; private set; } = null!; // 6581, right channel ($D400)
    public Socket<I6581Pinout> SidLeftSocket { get; private set; } = null!;  // 6581, left channel ($D420)
    public Socket<I6526Pinout> Cia1Socket { get; private set; } = null!;     // 6526 CIA 1 ($DC00)
    public Socket<I6526Pinout> Cia2Socket { get; private set; } = null!;     // 6526 CIA 2 ($DD00)
    public Socket<IF018Pinout> DmaSocket { get; private set; } = null!;      // CSG F018 DMAgic ($D700)
    public Socket<IF011Pinout> FdcSocket { get; private set; } = null!;      // CSG F011 disk controller ($D080)

    // Chips currently in the sockets (null = empty socket), read at every cycle
    I4510Pinout? cpu;
    I4567Pinout? vic;
    I6581Pinout? sidRight;
    I6581Pinout? sidLeft;
    I6526Pinout? cia1;
    I6526Pinout? cia2;
    IF018Pinout? dma;
    IF011Pinout? fdc;

    // CPU /IRQ: VIC-III, CIA 1 and the disk controller. /NMI: CIA 2 (the RESTORE key is not modelled yet)
    OpenCollectorLine irqLine = null!;
    OpenCollectorLine nmiLine = null!;

    internal bool IrqLine => irqLine.IsLow;
    internal bool NmiLine => nmiLine.IsLow;

    readonly CpuBus cpuBus;
    readonly VicBus vicBus;
    readonly DmaBus dmaBus;

    void CreateSockets()
    {
        irqLine = new OpenCollectorLine("/IRQ", () => vic?.Irq == true, () => cia1?.Irq == true, () => fdc?.Irq == true);
        nmiLine = new OpenCollectorLine("/NMI", () => cia2?.Irq == true);

        CpuSocket = new Socket<I4510Pinout>("CPU", ConnectCpu, DisconnectCpu);
        VicSocket = new Socket<I4567Pinout>("VIC-III", ConnectVic, DisconnectVic);
        SidRightSocket = new Socket<I6581Pinout>("SID right", chip => ConnectSid(chip, ref sidRight), _ => DisconnectSid(ref sidRight));
        SidLeftSocket = new Socket<I6581Pinout>("SID left", chip => ConnectSid(chip, ref sidLeft), _ => DisconnectSid(ref sidLeft));
        Cia1Socket = new Socket<I6526Pinout>("CIA 1", ConnectCia1, DisconnectCia1);
        Cia2Socket = new Socket<I6526Pinout>("CIA 2", ConnectCia2, DisconnectCia2);
        DmaSocket = new Socket<IF018Pinout>("DMAgic", ConnectDma, DisconnectDma);
        FdcSocket = new Socket<IF011Pinout>("F011", chip => fdc = chip, _ => fdc = null);

        // The CPU port has pull-ups on P0-P2: the memory map of the C64 mode
        cpuPort.External = () => 0xFF;
    }

    // CPU: address/data bus (20 bits and MAP/), /IRQ, /NMI, AEC from the VIC-III, /DMA from the DMAgic and from
    // the VIC-III's BA (simplification: the CPU stops on a low BA even if it could still write for 3 cycles)
    void ConnectCpu(I4510Pinout chip)
    {
        chip.Bus = cpuBus;
        chip.IrqInput = () => irqLine.IsLow;
        chip.NmiInput = () => nmiLine.IsLow;
        chip.AecInput = () => vic?.AddressEnable ?? true;
        chip.DmaInput = () => (vic?.BusAvailable ?? true) && !(dma?.Busy ?? false);
        cpu = chip;
    }

    void DisconnectCpu(I4510Pinout chip)
    {
        chip.Bus = null;
        cpu = null;
    }

    // VIC-III: video bus (RAM bank 0, Color RAM, character ROM), /IRQ, BA, video output
    void ConnectVic(I4567Pinout chip)
    {
        chip.Memory = vicBus;
        chip.FrameCompleted = OnFrameCompleted;
        vic = chip;
    }

    void DisconnectVic(I4567Pinout chip)
    {
        chip.Memory = null;
        chip.FrameCompleted = null;
        vic = null;
    }

    // DMAgic: the system bus, and the VIC-III has priority over it
    void ConnectDma(IF018Pinout chip)
    {
        chip.Memory = dmaBus;
        chip.BusGrant = () => vic?.BusAvailable ?? true;
        dma = chip;
    }

    void DisconnectDma(IF018Pinout chip)
    {
        chip.Memory = null;
        dma = null;
    }

    // CIA 1: port A to the keyboard rows and to the contacts of control port 2, port B to the keyboard columns and
    // to the contacts of control port 1 (the C65 keyboard has the same 8x8 matrix as the C64, plus 9 more keys read elsewhere)
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

    // SIDs: POT X and POT Y come from the control ports through an analog switch driven by PA6/PA7 of CIA 1
    void ConnectSid(I6581Pinout chip, ref I6581Pinout? field)
    {
        chip.PotXInput = () => SelectedPotPort()?.PotX ?? 0xFF;
        chip.PotYInput = () => SelectedPotPort()?.PotY ?? 0xFF;
        field = chip;
    }

    void DisconnectSid(ref I6581Pinout? field)
    {
        if (field is null) return;
        field.PotXInput = () => 0xFF;
        field.PotYInput = () => 0xFF;
        field = null;
    }

    ControlPort? SelectedPotPort()
    {
        byte select = cia1?.PortA.Pins ?? 0xFF;
        if ((select & 0x40) != 0) return ControlPort1;
        if ((select & 0x80) != 0) return ControlPort2;
        return null;
    }

    // CIA 2: PA0-PA1 = VIC bank (inverted), PA2 = user port, PA3-PA5 = ATN/CLK/DATA out on the serial bus,
    // PA6-PA7 = CLK/DATA in (no drive connected: the outputs drive the lines)
    void ConnectCia2(I6526Pinout chip)
    {
        chip.PortA.External = () =>
        {
            byte output = chip.PortA.Output;
            byte pins = 0x3F;
            if ((output & 0x10) == 0) pins |= 0x40;
            if ((output & 0x20) == 0) pins |= 0x80;
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
