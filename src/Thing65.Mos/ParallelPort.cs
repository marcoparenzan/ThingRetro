namespace Thing65.Mos;

// 8-bit parallel port of a chip (PA/PB of the 6526, P0-P5 of the 6510).
// Each bit is an output if the matching bit of Direction is 1, an input otherwise.
// NMOS outputs have a weak pull-up: an external device can always pull a pin to ground.
// So the level of a pin is the output (or the pull-up, for an input) ANDed with what the outside
// imposes (External, 0 = pulled to ground): this is how a joystick can be read even on an output port
public sealed class ParallelPort
{
    byte latch;
    byte direction;

    public byte Latch
    {
        get => latch;
        set { latch = value; Changed?.Invoke(); }
    }

    public byte Direction
    {
        get => direction;
        set { direction = value; Changed?.Invoke(); }
    }

    // Lines pulled to ground from outside (bits at 0); set by the board the chip is wired to
    public Func<byte> External { get; set; } = () => 0xFF;

    // Bits driven by the chip (the outputs), without reading the external pins
    public byte Output => (byte)(latch & direction);

    // Actual pin levels: outputs from the latch, inputs at 1 (pull-up), all ANDed with the outside
    public byte Pins => (byte)(((latch & direction) | ~direction) & External());

    // Latch or Direction changed: whatever is connected to the outputs can react (e.g. the VIC-II bank)
    public event Action? Changed;

    public void Reset()
    {
        latch = 0;
        direction = 0;
        Changed?.Invoke();
    }
}
