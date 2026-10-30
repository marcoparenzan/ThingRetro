using ThingMOS;
using ThingRetroEmu.Chips;
using ThingRetroEmu.Sockets;
using ThingRetroEmu.Video;
using ThingRetroEmu.Audio;
using ThingVCS;

namespace ThingVCS7800.A7800;

/// <summary>Development board scaffold, NOT a complete 7800 hardware decoder.</summary>
public sealed class Board : IBus
{
    readonly byte[] ram = new byte[4096];
    byte openBus = 0xff;
    bool cpuExecuting, slowCpuAccess;
    public int CpuMasterPhase { get; private set; } = -1;
    public int CpuSlotClocks { get; private set; } = 4;
    public bool Phi2 => CpuMasterPhase >= CpuSlotClocks / 2 && CpuMasterPhase < CpuSlotClocks;
    public bool HardwareBoot { get; set; }
    public bool Mode2600 { get; private set; }
    public bool ControlLocked { get; private set; } = true;
    public byte InputControl { get; private set; } = 7;
    public bool BiosEnabled => HardwareBoot && (InputControl & 4) == 0;
    public Socket<IBootRomPinout> BiosSocket { get; }
    BoardAddressSelection Decode(ushort address) => Mode2600 ? BoardAddressDecoder.Decode2600(address) : BoardAddressDecoder.Decode(address);
    bool IsSlowAddress(ushort address) => Mode2600 || Decode(address).Slow;
    public void SelectDevelopmentMode(bool vcs2600)
    {
        HardwareBoot = false;
        development2600 = vcs2600;
        Reset();
    }
    bool development2600;
    void WriteInputControl(byte value)
    {
        if (ControlLocked) return;
        InputControl = value;
        Mode2600 = (value & 2) == 0;
        ControlLocked = (value & 1) != 0;
    }
    public long MasterClocks { get; private set; }
    public long SlowCpuAccesses { get; private set; }
    void ObserveCpuAccess(ushort address)
    {
        // Functional slow-access windows, currently only the decoded TIA/RIOT address ranges.
        if (cpuExecuting && CpuSocket.Chip is ISallyPinout &&
            IsSlowAddress(address))
            slowCpuAccess = true;
    }
    public Socket<I6502Pinout> CpuSocket { get; }
    public Socket<IRiot6532Pinout> RiotSocket { get; }
    public Socket<ITiaInputPinout> TiaInputSocket { get; }
    public Socket<IMariaPinout> MariaSocket { get; }
    public Socket<ThingVCS.ICartridgePinout> CartridgeSocket { get; }
    public VideoPort Video { get; } = new();
    public AudioPort Audio { get; } = new(1789773);
    VideoSystem configuredSystem = VideoSystem.Ntsc;
    public MariaTiming Timing => MariaTiming.For(Video.EffectiveSystem ?? VideoSystem.Ntsc);
    public long Cycles { get; private set; }
    public ConsoleSwitches Console { get; } = new();
    public ThingRetroEmu.Ports.ControlPort ControlPort1 { get; } = new("Control port 1");
    public ThingRetroEmu.Ports.ControlPort ControlPort2 { get; } = new("Control port 2");
    // SWCHA: P1 directions PA4-7, P2 directions PA0-3; active-low contacts.
    byte ReadJoystickDirections() => (byte)(((ControlPort1.DigitalLines & 15) << 4) | (ControlPort2.DigitalLines & 15));
    bool ButtonPressed(int port, bool second)
    {
        var control = port == 1 ? ControlPort1 : ControlPort2;
        if (control.Device is ThingRetroEmu.Ports.ITwoButtonJoystick joystick)
            return second ? joystick.Fire2 : joystick.Fire;
        return !second && (control.DigitalLines & 0x10) == 0;
    }
    bool OneButtonMode(int port)
    {
        var riot = RiotSocket.Chip;
        int mask = port == 1 ? 4 : 16;
        // Undriven pin defaults high. Only an output driven low selects two-button mode.
        return riot is null || (riot.PortBDirection & mask) == 0 || (riot.PortBOutput & mask) != 0;
    }
    bool FirePinHigh(int port) => !OneButtonMode(port) || (!ButtonPressed(port, false) && !ButtonPressed(port, true));
    bool ComparatorHigh(int input) => ButtonPressed(input < 2 ? 1 : 2, (input & 1) == 0);

    public Board()
    {
        BiosSocket = new("BIOS ROM", _ => { }, _ => { });
        CpuSocket = new("CPU (6502-compatible development socket)", cpu =>
        {
            cpu.Bus = this;
            if (cpu is ISallyPinout sally) sally.HaltInput = () => !Mode2600 && ((MariaSocket?.Chip?.DmaBusy ?? false) || (MariaSocket?.Chip?.WaitSync ?? false));
            cpu.IrqInput = () => RiotSocket?.Chip?.Irq ?? false;
            cpu.NmiInput = () => !Mode2600 && (MariaSocket?.Chip?.Nmi ?? false);
            cpu.RdyInput = () => Mode2600 || !(MariaSocket?.Chip?.WaitSync ?? false);
            cpu.SoInput = () => false;
        }, cpu =>
        {
            cpu.Bus = null;
            if (cpu is ISallyPinout sally) sally.HaltInput = () => false;
            cpu.IrqInput = () => false;
            cpu.NmiInput = () => false;
            cpu.RdyInput = () => true;
            cpu.SoInput = () => false;
        });
        RiotSocket = new("RIOT 6532", chip =>
        {
            chip.PortAInput = ReadJoystickDirections;
            chip.PortBInput = Console.Read7800Contacts;
        }, chip =>
        {
            chip.PortAInput = () => 0xff;
            chip.PortBInput = () => 0xff;
        });
        TiaInputSocket = new("TIA digital inputs (partial chip)", chip =>
        {
            chip.Fire1High = () => FirePinHigh(1);
            chip.Fire2High = () => FirePinHigh(2);
            if (chip is ITiaButtonInputPinout buttons) buttons.ButtonInputHigh = ComparatorHigh;
        }, chip =>
        {
            chip.Fire1High = () => true;
            chip.Fire2High = () => true;
            if (chip is ITiaButtonInputPinout buttons) buttons.ButtonInputHigh = _ => false;
        });
        MariaSocket = new("MARIA", chip =>
        {
            chip.Bus = this;
            chip.BusBoundaryInput = () => CpuMasterPhase == 0;
            if (chip is Maria maria) maria.Configure(Timing.System);
        }, chip => { chip.Bus = null; chip.BusBoundaryInput = null; });
        CartridgeSocket = new("Cartridge connector", _ => { }, _ => { });
        Video.SystemChanged += () =>
        {
            if (configuredSystem != Timing.System)
            {
                configuredSystem = Timing.System;
                if (MariaSocket.Chip is Maria maria) maria.Configure(Timing.System);
                Audio.SetClockRate(Timing.MasterClockHz / 4);
                Reset();
            }
        };
    }
    public byte Read(ushort address)
    {
        ObserveCpuAccess(address);
        if (BiosEnabled && BiosSocket.Chip is { } bios && bios.Selects(address)) return openBus = bios.Read(address);
        var selected = Decode(address);
        return openBus = selected.Chip switch
        {
            BoardChipSelect.Maria => MariaSocket.Chip?.ReadRegister(selected.Offset, openBus) ?? openBus,
            BoardChipSelect.Tia => TiaInputSocket.Chip?.ReadRegister(selected.Offset & 15, openBus) ?? openBus,
            BoardChipSelect.RiotRegisters => RiotSocket.Chip?.ReadRegister(selected.Offset) ?? openBus,
            BoardChipSelect.RiotRam => RiotSocket.Chip?.ReadRam(selected.Offset) ?? openBus,
            BoardChipSelect.MainRam => ram[selected.Offset],
            BoardChipSelect.Cartridge => CartridgeSocket.Chip?.Read((ushort)selected.Offset) ?? openBus,
            _ => openBus
        };
    }
    public void Write(ushort address, byte value)
    {
        ObserveCpuAccess(address);
        openBus = value;
        var selected = Decode(address);
        switch (selected.Chip)
        {
            case BoardChipSelect.Tia:
                if (HardwareBoot && !ControlLocked) WriteInputControl(value);
                TiaInputSocket.Chip?.WriteRegister(selected.Offset, value); break;
            case BoardChipSelect.Maria: MariaSocket.Chip?.WriteRegister(selected.Offset, value); break;
            case BoardChipSelect.RiotRegisters: RiotSocket.Chip?.WriteRegister(selected.Offset, value); break;
            case BoardChipSelect.RiotRam: RiotSocket.Chip?.WriteRam(selected.Offset, value); break;
            case BoardChipSelect.MainRam: ram[selected.Offset] = value; break;
            case BoardChipSelect.Cartridge: CartridgeSocket.Chip?.Write((ushort)selected.Offset, value); break;
        }
    }
    public void Reset()
    {
        InputControl = HardwareBoot ? (byte)0 : (byte)(development2600 ? 5 : 7);
        ControlLocked = !HardwareBoot; Mode2600 = !HardwareBoot && development2600;
        Cycles = MasterClocks = SlowCpuAccesses = 0; cpuExecuting = slowCpuAccess = false;
        CartridgeSocket.Chip?.Reset(); MariaSocket.Chip?.Reset(); TiaInputSocket.Chip?.Reset(); RiotSocket.Chip?.Reset(); CpuSocket.Chip?.Reset();
    }
    public void Tick()
    {
        var cpu = CpuSocket.Chip;
        var sally = cpu as ISallyPinout;
        bool haltedAtStart = sally?.HaltInput() ?? false;
        var next = sally?.NextBusCycle;
        CpuSlotClocks = !haltedAtStart && next is { } access && IsSlowAddress(access.Address) ? 6 : 4;
        slowCpuAccess = false;
        var maria = Mode2600 ? null : MariaSocket.Chip;
        long frame = maria?.Frames ?? 0;
        try
        {
            for (int phase = 0; phase < CpuSlotClocks; phase++)
            {
                CpuMasterPhase = phase;
                sally?.SamplePins();
                // MARIA may take/release the bus only at a CPU slot boundary (PHI1).
                maria?.Tick();
                if (phase == 0 && sally is not null)
                {
                    // A handoff at PHI1 may release a pending slow access or cancel it for DMA.
                    // Fix the remaining slot duration before PHI2, not after the bus access.
                    CpuSlotClocks = !sally.HaltInput() && next is { } pending && IsSlowAddress(pending.Address) ? 6 : 4;
                }
                if (phase == CpuSlotClocks - 1)
                {
                    // Commit the CPU's single bus operation at the falling PHI2 edge.
                    cpuExecuting = true;
                    try { cpu?.Tick(); }
                    finally { cpuExecuting = false; }
                }
                (TiaInputSocket.Chip as ITiaAudioPinout)?.TickAudio();
                (CartridgeSocket.Chip as IMasterClockedAudioCartridge)?.TickMasterClock(Timing.MasterClockHz);
                MasterClocks++;
                if ((MasterClocks & 3) == 0)
                {
                    var audioCart = CartridgeSocket.Chip as IAudioCartridgePinout;
                    if (audioCart is not IMasterClockedAudioCartridge) audioCart?.TickAudio();
                    // Fixed headroom, not a model of the analogue summing network.
                    double tiaLevel = (TiaInputSocket.Chip as ITiaAudioPinout)?.AudioLevel ?? 0;
                    Audio.Sample(Math.Clamp((tiaLevel + (audioCart?.AudioLevel ?? 0)) * .5, 0, 1));
                }
            }
        }
        finally { CpuMasterPhase = -1; cpuExecuting = false; }
        TiaInputSocket.Chip?.Tick(); RiotSocket.Chip?.Tick();
        if (slowCpuAccess) SlowCpuAccesses++;
        if (maria is not null && maria.Frames != frame) Video.Present(maria);
        Cycles++;
    }
    // A video chip will drive this at frame completion. No synthetic black-frame generator.
    public void Present(IVideoSource source) => Video.Present(source);
}
