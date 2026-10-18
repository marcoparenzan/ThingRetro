namespace ThingRetroEmu.Ports;

// Commodore 1351 mouse in proportional mode (the one GEOS uses). The mouse counts the movement and presents
// it on the POT lines: POT X and POT Y are (position mod 64) * 2, bit 0 is a noise bit the drivers ignore.
// The left button closes the fire pin, the right one the "up" pin.
// The driver reads the difference between two successive readings, so only the movement counts.
// Not modelled: joystick mode (right button held at power on) and the synchronization of the mouse
// with the SID's measurement
public sealed class Mouse1351 : IControlPortDevice
{
    const int RightButtonBit = 0x01, LeftButtonBit = 0x10;

    int x, y;
    int buttons;

    // Movement in "mickeys": x to the right, y upwards (like the 1351 counters)
    public void Move(int dx, int dy)
    {
        Interlocked.Add(ref x, dx);
        Interlocked.Add(ref y, dy);
    }

    public bool LeftButton { get => (Volatile.Read(ref buttons) & LeftButtonBit) != 0; set => SetButton(LeftButtonBit, value); }
    public bool RightButton { get => (Volatile.Read(ref buttons) & RightButtonBit) != 0; set => SetButton(RightButtonBit, value); }

    public byte DigitalLines => (byte)~Volatile.Read(ref buttons);
    public byte PotX => (byte)((Volatile.Read(ref x) & 0x3F) << 1);
    public byte PotY => (byte)((Volatile.Read(ref y) & 0x3F) << 1);

    void SetButton(int bit, bool pressed)
    {
        if (pressed) Interlocked.Or(ref buttons, bit);
        else Interlocked.And(ref buttons, ~bit);
    }
}
