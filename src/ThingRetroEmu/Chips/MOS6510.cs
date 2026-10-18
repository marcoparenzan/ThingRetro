namespace ThingRetroEmu.Chips;

// MOS 6510: a 6502 with an on-chip I/O port (P0-P5) at registers $0000 (direction) and $0001 (data).
// On the C64 P0-P2 select the memory map (LORAM, HIRAM, CHAREN) and P3-P5 go to the datasette
public class MOS6510 : MOS6502, I6510Pinout
{
    public ParallelPort Port { get; } = new();

    // At reset the port is all input: the pins are what the outside imposes (pull-ups).
    // The port's Changed event lets the board update the memory map before the vector is read
    protected override void OnReset() => Port.Reset();

    // The port registers as the CPU sees them: $00 = direction, $01 = pin levels
    public byte ReadPort(ushort addr) => addr == 0 ? Port.Direction : Port.Pins;

    public void WritePort(ushort addr, byte value)
    {
        if (addr == 0) Port.Direction = value;
        else Port.Latch = value;
    }

    // The address goes on the bus anyway: on a read the bus data is ignored, on a write it also reaches
    // the bus (on the C64, the RAM underneath)
    protected override byte ReadByte(ushort addr)
    {
        byte value = base.ReadByte(addr);
        return addr <= 0x0001 ? ReadPort(addr) : value;
    }

    protected override void WriteByte(ushort addr, byte val)
    {
        if (addr <= 0x0001) WritePort(addr, val);
        base.WriteByte(addr, val);
    }
}
