namespace ThingCommodore.Drives;

/*
    The 1581 board: a 6502 at 2 MHz, 8 KB of RAM, the 32 KB DOS ROM, an 8520 and a WD1770.
      $0000-$1FFF  RAM (8 KB)
      $4000-$5FFF  8520 (16 registers, repeated): port A the drive, port B the IEC bus, FLAG = ATN
      $6000-$7FFF  WD1770 (4 registers, repeated)
      $8000-$FFFF  ROM
    Port A: PA0 side select (0 = side 0, 1 = side 1), PA1 /READY (0 = a disk is in and spinning), PA2 /MOTOR (0 = on), PA3-PA4 device number
    jumpers, PA5 and PA6 the LEDs, PA7 /DISK CHANGE (0 = the disk was changed; a step of the head resets it).
    Port B, as in the 1541 but for the device number: PB0 DATA in, PB1 DATA out, PB2 CLK in, PB3 CLK out, PB4 ATN gate (1 = DATA goes down with ATN),
    PB5 fast serial direction, PB6 /WRITE PROTECT, PB7 ATN in. The IEC pins are inverted by the drivers and receivers: a 1 written
    to PB1 or PB3 pulls DATA or CLK down, and PB0, PB2 and PB7 read 1 when the line is down. ATN pulls DATA down by itself unless PB4
    says it has been acknowledged. The 8520's interrupt goes to the CPU's /IRQ; the WD1770's INTRQ and DRQ are read in its status.
    The fast serial burst (SP and CNT on the bus lines) is not done: the C128 asks for it and the DOS falls back to the slow bus.
    Reference: "1581 Service Manual" (PN 314982-01)
 */
public class Board1581
{
    public const int ROM_SIZE = 0x8000;
    public const long CLOCK_HZ = 2_000_000;

    readonly byte[] ram = new byte[0x2000];
    readonly byte[] rom;
    readonly Bus bus;
    readonly Mechanism1581 mechanism = new();
    IecBus? iec;
    int deviceNumber = 8;

    I6502Pinout? cpu;
    I6526Pinout? cia;
    IWd1770Pinout? fdc;

    // Every access to the 8520 and the WD1770, for tools that want to see what the DOS does: address, true = write, value
    public Action<ushort, bool, byte>? Trace { get; set; }

    public Mechanism1581 Mechanism => mechanism;

    // The disk in the drive (null = no disk)
    public D81Image? Disk
    {
        get => mechanism.Disk;
        set => mechanism.Disk = value;
    }

    public Socket<I6502Pinout> CpuSocket { get; }
    public Socket<I6526Pinout> CiaSocket { get; }      // 8520 at $4000
    public Socket<IWd1770Pinout> FdcSocket { get; }    // WD1770 at $6000

    public Board1581(byte[] rom)
    {
        ArgumentNullException.ThrowIfNull(rom);
        if (rom.Length != ROM_SIZE) throw new ArgumentException($"The 1581 ROM has {ROM_SIZE} bytes, not {rom.Length}", nameof(rom));
        this.rom = rom;
        bus = new Bus(this);

        CpuSocket = new Socket<I6502Pinout>("CPU", ConnectCpu, c => { c.Bus = null; cpu = null; });
        CiaSocket = new Socket<I6526Pinout>("8520", ConnectCia, _ => cia = null);
        FdcSocket = new Socket<IWd1770Pinout>("WD1770", ConnectFdc, c => { c.Drive = null; fdc = null; });

        CpuSocket.Insert(new MOS6502());
        CiaSocket.Insert(new MOS8520());
        FdcSocket.Insert(new Wd1770(CLOCK_HZ));
        Reset();
    }

    // The jumpers of the device number (8-11)
    public int DeviceNumber
    {
        get => deviceNumber;
        set => deviceNumber = value is >= 8 and <= 11 ? value : throw new ArgumentOutOfRangeException(nameof(value), "The device number is 8 to 11");
    }

    // The IEC cable: the drive connects its DATA and CLK pins to the lines of the bus
    public void Plug(IecBus cable)
    {
        if (iec is not null) throw new InvalidOperationException("The drive is already plugged in");
        iec = cable;
        cable.Data.Connect(() => DataLow);
        cable.Clk.Connect(() => ClkLow);
    }

    public void Unplug() => iec = null;

    public bool Plugged => iec is not null;

    // PB1 pulls DATA down, and so does the attention gate (a NAND of ATN in and PB4) while ATN is down and PB4 is 1: the DOS keeps
    // PB4 at 1, so that DATA goes down by itself when ATN does, and sets it to 0 when it has taken over
    bool DataLow => cia is not null && ((cia.PortB.Output & 0x02) != 0 || iec is not null && iec.Atn.IsLow && (cia.PortB.Output & 0x10) != 0);
    bool ClkLow => cia is not null && (cia.PortB.Output & 0x08) != 0;

    void ConnectCpu(I6502Pinout chip)
    {
        chip.Bus = bus;
        chip.IrqInput = () => cia?.Irq == true;
        chip.NmiInput = () => false;
        chip.RdyInput = () => true;
        chip.SoInput = () => true;
        cpu = chip;
    }

    void ConnectCia(I6526Pinout chip)
    {
        chip.PortA.External = () =>
        {
            int pins = 0xFF;
            if (mechanism.Ready) pins &= ~0x02;                      // /READY
            if (mechanism.DiskChanged) pins &= ~0x80;                // /DISK CHANGE
            pins = pins & ~0x18 | (deviceNumber - 8) << 3 & 0x18;    // device jumpers
            return (byte)pins;
        };
        chip.PortB.External = () =>
        {
            // the receivers invert: 1 = the line is down
            int pins = (iec?.Data.IsLow == true ? 0x01 : 0) | (iec?.Clk.IsLow == true ? 0x04 : 0) | (iec?.Atn.IsLow == true ? 0x80 : 0) | 0x7A;
            if (mechanism.WriteProtected) pins &= ~0x40;
            return (byte)pins;
        };
        chip.FlagInput = () => iec?.Atn.IsLow != true;               // ATN, as it is on the bus: FLAG sees it go down
        cia = chip;
    }

    void ConnectFdc(IWd1770Pinout chip)
    {
        chip.Drive = mechanism;
        fdc = chip;
    }

    public void Reset()
    {
        mechanism.Reset();
        cia?.Reset();
        fdc?.Reset();
        cpu?.Reset();
    }

    // One cycle at 2 MHz
    public void Tick()
    {
        if (cia is not null)
        {
            int pa = cia.PortA.Pins;
            mechanism.MotorOn = (pa & 0x04) == 0;
            mechanism.Side = (pa & 0x01) == 0 ? 0 : 1;
            mechanism.Led = (pa & 0x60) != 0x60;
        }
        mechanism.Tick();
        cia?.Tick();
        fdc?.Tick();
        cpu?.Tick();
    }

    // For tools and tests
    public byte Ram(int address) => ram[address & 0x1FFF];
    public byte Read(ushort address) => bus.Read(address);

    sealed class Bus(Board1581 board) : IBus
    {
        public byte Read(ushort address)
        {
            if (address >= 0x8000) return board.rom[address & 0x7FFF];
            if (address < 0x2000) return board.ram[address];
            if (address is >= 0x4000 and < 0x6000)
            {
                byte value = board.cia?.Read(address & 0x0F) ?? 0xFF;
                board.Trace?.Invoke(address, false, value);
                return value;
            }
            if (address is >= 0x6000 and < 0x8000)
            {
                byte value = board.fdc?.Read(address & 3) ?? 0xFF;
                board.Trace?.Invoke(address, false, value);
                return value;
            }
            return 0xFF;
        }

        public void Write(ushort address, byte value)
        {
            if (address >= 0x8000) return;
            if (address < 0x2000) board.ram[address] = value;
            else if (address is >= 0x4000 and < 0x6000)
            {
                board.Trace?.Invoke(address, true, value);
                board.cia?.Write(address & 0x0F, value);
            }
            else if (address is >= 0x6000 and < 0x8000)
            {
                board.Trace?.Invoke(address, true, value);
                board.fdc?.Write(address & 3, value);
            }
        }
    }
}
