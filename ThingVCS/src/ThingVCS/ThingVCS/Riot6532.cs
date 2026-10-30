namespace ThingVCS;

// Logical pins: PA/PB inputs, driven outputs + DDR, IRQ, register select, clock and reset.
// True IRQ means asserted; no physical DIP numbering or analogue simulation.
public interface IRiot6532Pinout
{
    Func<byte> PortAInput { get; set; }
    Func<byte> PortBInput { get; set; }
    byte PortAOutput { get; }
    byte PortBOutput { get; }
    byte PortADirection { get; }
    byte PortBDirection { get; }
    bool Irq { get; }
    byte ReadRegister(int address);
    void WriteRegister(int address, byte value);
    byte ReadRam(int address);
    void WriteRam(int address, byte value);
    void Tick();
    void Reset();
}

/// <summary>Functional 6532 ports, interval timer and clock-sampled PA7 edge detector.</summary>
public sealed class Riot6532 : IRiot6532Pinout
{
    readonly byte[] ram = new byte[128];
    byte timer;
    int divider = 1, remaining = 1;
    bool running, timerFlag, irqEnabled, underflow;
    bool edgeFlag, edgeIrqEnabled, positiveEdge;
    bool previousPa7 = true;
    byte PortALevel => (byte)((PortAOutput & PortADirection) | (PortAInput() & ~PortADirection));
    public Func<byte> PortAInput { get; set; } = () => 0xff;
    public Func<byte> PortBInput { get; set; } = () => 0xff;
    public byte PortAOutput { get; private set; }
    public byte PortBOutput { get; private set; }
    public byte PortADirection { get; private set; }
    public byte PortBDirection { get; private set; }
    public bool Irq => (irqEnabled && timerFlag) || (edgeIrqEnabled && edgeFlag);
    public byte ReadRam(int address) => ram[address & 127];
    public void WriteRam(int address, byte value) => ram[address & 127] = value;
    public byte ReadRegister(int address)
    {
        if ((address & 4) == 0)
            return (address & 3) switch
            {
                0 => PortALevel,
                1 => PortADirection,
                2 => (byte)((PortBOutput & PortBDirection) | (PortBInput() & ~PortBDirection)),
                _ => PortBDirection
            };
        if ((address & 1) != 0)
        {
            byte flags = (byte)((timerFlag ? 0x80 : 0) | (edgeFlag ? 0x40 : 0));
            edgeFlag = false; // Flag reads acknowledge PA7 only, not the interval timer.
            return flags;
        }
        byte value = timer;
        timerFlag = false;
        irqEnabled = (address & 8) != 0;
        return value;
    }
    public void WriteRegister(int address, byte value)
    {
        if ((address & 0x10) != 0 && (address & 4) != 0)
        {
            divider = (address & 3) switch { 0 => 1, 1 => 8, 2 => 64, _ => 1024 };
            remaining = divider; timer = value; running = true;
            underflow = false; timerFlag = false; irqEnabled = (address & 8) != 0;
        }
        else if ((address & 4) == 0)
        {
            switch (address & 3)
            {
                case 0: PortAOutput = value; break;
                case 1: PortADirection = value; break;
                case 2: PortBOutput = value; break;
                case 3: PortBDirection = value; break;
            }
        }
        else
        {
            // A1 enables edge IRQ; A0 selects positive (1) or negative (0) edge. Data is ignored.
            edgeIrqEnabled = (address & 2) != 0;
            positiveEdge = (address & 1) != 0;
        }
    }
    public void Tick()
    {
        bool pa7 = (PortALevel & 0x80) != 0;
        if (pa7 != previousPa7 && pa7 == positiveEdge) edgeFlag = true;
        previousPa7 = pa7;
        if (!running || --remaining > 0) return;
        if (timer == 0)
        {
            if (!underflow) timerFlag = true;
            underflow = true; divider = 1;
        }
        timer = unchecked((byte)(timer - 1)); remaining = divider;
    }
    public void Reset()
    {
        PortAOutput = PortBOutput = PortADirection = PortBDirection = 0;
        timer = 0; divider = remaining = 1;
        running = timerFlag = irqEnabled = underflow = false;
        edgeFlag = edgeIrqEnabled = positiveEdge = false;
        previousPa7 = (PortALevel & 0x80) != 0;
        // Reset does not erase RAM or disconnect external input pins.
    }
}
