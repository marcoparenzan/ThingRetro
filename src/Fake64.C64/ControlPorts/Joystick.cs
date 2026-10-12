namespace Fake64.C64;

// Joystick digitale: cinque interruttori che chiudono a massa i pin su, giù, sinistra, destra e fuoco.
// Lo stato si può cambiare da qualunque thread (per esempio dal simulatore che legge la tastiera del PC)
public sealed class Joystick : IControlPortDevice
{
    const int UpBit = 0x01, DownBit = 0x02, LeftBit = 0x04, RightBit = 0x08, FireBit = 0x10;

    int closed; // contatti chiusi, un bit per interruttore

    public bool Up { get => Get(UpBit); set => Set(UpBit, value); }
    public bool Down { get => Get(DownBit); set => Set(DownBit, value); }
    public bool Left { get => Get(LeftBit); set => Set(LeftBit, value); }
    public bool Right { get => Get(RightBit); set => Set(RightBit, value); }
    public bool Fire { get => Get(FireBit); set => Set(FireBit, value); }

    // Tutti gli interruttori in una volta (utile quando lo stato arriva tutto insieme, come da un gamepad)
    public void SetState(bool up, bool down, bool left, bool right, bool fire) =>
        Volatile.Write(ref closed, (up ? UpBit : 0) | (down ? DownBit : 0) | (left ? LeftBit : 0) | (right ? RightBit : 0) | (fire ? FireBit : 0));

    public void ReleaseAll() => Volatile.Write(ref closed, 0);

    public byte DigitalLines => (byte)~Volatile.Read(ref closed);
    public byte PotX => 0xFF;
    public byte PotY => 0xFF;

    bool Get(int bit) => (Volatile.Read(ref closed) & bit) != 0;

    void Set(int bit, bool value)
    {
        if (value) Interlocked.Or(ref closed, bit);
        else Interlocked.And(ref closed, ~bit);
    }
}
