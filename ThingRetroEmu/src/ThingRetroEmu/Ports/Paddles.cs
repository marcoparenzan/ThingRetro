namespace ThingRetroEmu.Ports;

// Pair of paddles (Le Mans, REVS...): two 470 kΩ potentiometers on the POT X (paddle A) and POT Y (paddle B)
// lines, and one button each: A closes pin 3 (left), B pin 4 (right).
// The position is the value the SID measures: 0 with the knob all the way one side, 255 the other
public sealed class Paddles : IControlPortDevice
{
    const int FireABit = 0x04, FireBBit = 0x08;

    int positionA = 0x80, positionB = 0x80;
    int buttons;

    public byte PositionA { get => (byte)Volatile.Read(ref positionA); set => Volatile.Write(ref positionA, value); }
    public byte PositionB { get => (byte)Volatile.Read(ref positionB); set => Volatile.Write(ref positionB, value); }

    public bool FireA { get => (Volatile.Read(ref buttons) & FireABit) != 0; set => SetButton(FireABit, value); }
    public bool FireB { get => (Volatile.Read(ref buttons) & FireBBit) != 0; set => SetButton(FireBBit, value); }

    public byte DigitalLines => (byte)~Volatile.Read(ref buttons);
    public byte PotX => PositionA;
    public byte PotY => PositionB;

    void SetButton(int bit, bool pressed)
    {
        if (pressed) Interlocked.Or(ref buttons, bit);
        else Interlocked.And(ref buttons, ~bit);
    }
}
