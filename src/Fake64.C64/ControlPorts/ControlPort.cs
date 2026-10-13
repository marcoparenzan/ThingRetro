namespace Fake64.C64;

// What a device presents on the pins of the control port (DE-9 socket, "Atari" standard):
//   1 JOY0 (up)   2 JOY1 (down)   3 JOY2 (left)   4 JOY3 (right)   6 fire (and light pen on port 1)
//   5 POT Y       9 POT X         7 +5V           8 ground
// Closed digital contacts pull the line to ground; the POT lines carry a resistance that the SID measures
public interface IControlPortDevice
{
    // Pins 1, 2, 3, 4, 6 in bits 0-4 (0 = closed to ground); bits 5-7 are not connected and read 1
    byte DigitalLines { get; }

    // Pins 9 and 5 as the SID measures them (0-255); $FF = no resistance connected
    byte PotX { get; }
    byte PotY { get; }
}

// Control port socket on the board: one device at a time (joystick, paddles, mouse...).
// With nothing connected the contacts are open and the POT lines floating
public sealed class ControlPort
{
    volatile IControlPortDevice? device;

    internal ControlPort(string name)
    {
        Name = name;
    }

    public string Name { get; }
    public IControlPortDevice? Device => device;
    public bool IsEmpty => device is null;

    public void Plug(IControlPortDevice newDevice)
    {
        ArgumentNullException.ThrowIfNull(newDevice);
        if (device is not null)
            throw new InvalidOperationException($"A device is already connected to {Name}: unplug it first");
        device = newDevice;
    }

    public IControlPortDevice? Unplug()
    {
        var unplugged = device;
        device = null;
        return unplugged;
    }

    // Pin levels as seen by the board
    internal byte DigitalLines => device?.DigitalLines ?? 0xFF;
    internal byte PotX => device?.PotX ?? 0xFF;
    internal byte PotY => device?.PotY ?? 0xFF;

    public override string ToString() => $"{Name}: {(device is null ? "empty" : device.GetType().Name)}";
}
