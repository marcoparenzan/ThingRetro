using ThingRetroEmu.Chips;
using ThingRetroEmu.Buses;
using ThingRetroEmu.Media;
using ThingRetroEmu.Sockets;

namespace ThingCommodore.Drives;

/*
    The 1541 board: a 6502, 2 KB of RAM, the 16 KB DOS ROM and two VIAs.
      $0000-$17FF  RAM (2 KB, mirrored)
      $1800-$1BFF  VIA 1: the IEC bus (PB0 DATA in, PB1 DATA out, PB2 CLK in, PB3 CLK out, PB4 ATN acknowledge, PB5-PB6 device
                   number, PB7 ATN in; CA1 = ATN in)
      $1C00-$1FFF  VIA 2: the mechanics (PB0-PB1 stepper, PB2 motor, PB3 LED, PB4 write protect sense, PB7 SYNC; PA data,
                   CA1 byte ready)
      $8000-$FFFF  ROM (the 16 KB seen twice)
    The IEC pins are inverted by the drivers and receivers (7406): a 1 written to PB1/PB3 pulls DATA/CLK down, and PB0/PB2/PB7
    read 1 when the line is down. ATN pulls DATA down by itself unless PB4 says it has been acknowledged (a XOR gate).
    The mechanics (Mechanism1541) hang on VIA 2: PB0-PB1 stepper, PB2 motor, PB3 LED, PB5-PB6 speed zone, PB7 SYNC (low on a SYNC
    mark), PB4 write protect sense, PA the byte under the head (or to write: DDRA output), CA1 byte ready, CA2 enables SO (the CPU's V
    flag on byte ready), CB2 the read/write switch (low = write).
    Reference: "1541 Service Manual" (schematic 1540008-01), "Inside Commodore DOS"
 */
public class Board1541
{
    public const int ROM_SIZE = 0x4000;

    readonly byte[] ram = new byte[0x800];
    readonly byte[] rom;
    readonly Bus bus;
    readonly Mechanism1541 mechanism = new();
    IecBus? iec;
    int deviceNumber = 8;

    I6502Pinout? cpu;
    IVia6522Pinout? via1;
    IVia6522Pinout? via2;

    // Every access to the VIA 1 (IEC bus), for tools that want to see what the DOS does: address, true = write, value
    public Action<ushort, bool, byte>? Via1Trace { get; set; }

    public Mechanism1541 Mechanism => mechanism;

    // The disk in the drive (null = no disk)
    public D64Image? Disk
    {
        get => mechanism.Disk;
        set => mechanism.Disk = value;
    }

    public Socket<I6502Pinout> CpuSocket { get; }
    public Socket<IVia6522Pinout> Via1Socket { get; }  // 6522 at $1800: IEC bus
    public Socket<IVia6522Pinout> Via2Socket { get; }  // 6522 at $1C00: mechanics

    public Board1541(byte[] rom)
    {
        ArgumentNullException.ThrowIfNull(rom);
        if (rom.Length != ROM_SIZE) throw new ArgumentException($"The 1541 ROM has {ROM_SIZE} bytes, not {rom.Length}", nameof(rom));
        this.rom = rom;
        bus = new Bus(this);

        CpuSocket = new Socket<I6502Pinout>("CPU", ConnectCpu, c => { c.Bus = null; cpu = null; });
        Via1Socket = new Socket<IVia6522Pinout>("VIA 1", ConnectVia1, _ => via1 = null);
        Via2Socket = new Socket<IVia6522Pinout>("VIA 2", ConnectVia2, _ => via2 = null);

        CpuSocket.Insert(new MOS6502());
        Via1Socket.Insert(new Via6522());
        Via2Socket.Insert(new Via6522());
        Reset();
    }

    // The jumpers of the device number (8-11)
    public int DeviceNumber
    {
        get => deviceNumber;
        set => deviceNumber = value is >= 8 and <= 11 ? value : throw new ArgumentOutOfRangeException(nameof(value), "The device number is 8 to 11");
    }

    // The IEC cable: the drive connects its DATA, CLK and ATN pins to the lines of the bus
    public void Plug(IecBus cable)
    {
        if (iec is not null) throw new InvalidOperationException("The drive is already plugged in");
        iec = cable;
        cable.Data.Connect(() => DataLow);
        cable.Clk.Connect(() => ClkLow);
    }

    // Takes the drive off the cable (the cable keeps its connections but they do not see the drive any more)
    public void Unplug() => iec = null;

    public bool Plugged => iec is not null;

    // PB1 pulls DATA down, and so does the attention gate while ATN and the acknowledge (PB4) disagree
    bool DataLow => via1 is not null && ((via1.PortB.Output & 0x02) != 0 || iec is not null && (iec.Atn.IsLow != ((via1.PortB.Output & 0x10) != 0)));
    bool ClkLow => via1 is not null && (via1.PortB.Output & 0x08) != 0;

    void ConnectCpu(I6502Pinout chip)
    {
        chip.Bus = bus;
        chip.IrqInput = () => via1?.Irq == true || via2?.Irq == true;
        chip.NmiInput = () => false;
        chip.SoInput = () => !(mechanism.ByteReady && via2?.Ca2Output == true);
        cpu = chip;
    }

    void ConnectVia1(IVia6522Pinout chip)
    {
        chip.PortA.External = () => 0xFF;
        chip.PortB.External = () =>
        {
            // the receivers invert: 1 = the line is down
            int pins = (iec?.Data.IsLow == true ? 0x01 : 0) | (iec?.Clk.IsLow == true ? 0x04 : 0) | (iec?.Atn.IsLow == true ? 0x80 : 0) | 0x7A;
            pins = pins & ~0x60 | ((deviceNumber - 8) << 5) & 0x60;
            return (byte)pins;
        };
        chip.Ca1Input = () => iec?.Atn.IsLow == true;
        via1 = chip;
    }

    void ConnectVia2(IVia6522Pinout chip)
    {
        chip.PortA.External = () => mechanism.Data;
        chip.PortB.External = () => (byte)(0x6F | (mechanism.WriteProtectSense ? 0x10 : 0) | (mechanism.Sync ? 0 : 0x80));
        chip.Ca1Input = () => !mechanism.ByteReady;
        via2 = chip;
    }

    public void Reset()
    {
        mechanism.Reset();
        via1?.Reset();
        via2?.Reset();
        cpu?.Reset();
    }

    // One cycle at 1 MHz
    public void Tick()
    {
        if (via2 is not null)
        {
            int pb = via2.PortB.Output;
            // While the VIA does not drive the stepper (after a reset) the rotor rests where it is: the first phase the DOS
            // sets is not a step
            mechanism.Control(pb & 3, (via2.PortB.Direction & 3) == 3, (pb & 0x04) != 0, (pb & 0x08) != 0, pb >> 5 & 3, !via2.Cb2Output, via2.PortA.Output);
        }
        mechanism.Tick();
        via1?.Tick();
        via2?.Tick();
        cpu?.Tick();
    }

    // For tools and tests
    public byte Ram(int address) => ram[address & 0x7FF];
    public byte Read(ushort address) => bus.Read(address);

    sealed class Bus(Board1541 board) : IBus
    {
        public byte Read(ushort address)
        {
            if (address >= 0x8000) return board.rom[address & 0x3FFF];
            if (address < 0x1800) return board.ram[address & 0x7FF];
            if (address < 0x1C00)
            {
                byte value = board.via1?.Read(address & 0x0F) ?? 0xFF;
                board.Via1Trace?.Invoke(address, false, value);
                return value;
            }
            if (address < 0x2000) return board.via2?.Read(address & 0x0F) ?? 0xFF;
            return 0xFF;
        }

        public void Write(ushort address, byte value)
        {
            if (address >= 0x8000) return;
            if (address < 0x1800) board.ram[address & 0x7FF] = value;
            else if (address < 0x1C00)
            {
                board.Via1Trace?.Invoke(address, true, value);
                board.via1?.Write(address & 0x0F, value);
            }
            else if (address < 0x2000) board.via2?.Write(address & 0x0F, value);
        }
    }
}
