namespace Fake64.C64;

// Coppia di paddle (Le Mans, REVS...): due potenziometri da 470 kΩ sulle linee POT X (paddle A) e
// POT Y (paddle B), e un pulsante ciascuno: A chiude il pin 3 (sinistra), B il pin 4 (destra).
// La posizione è il valore che il SID misura: 0 con la manopola tutta da una parte, 255 dall'altra
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
