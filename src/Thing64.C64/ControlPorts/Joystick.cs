namespace Thing64.C64;

// Digital joystick: five switches that close the up, down, left, right and fire pins to ground.
// The state can be changed from any thread (for example from the simulator that reads the PC keyboard)
public sealed class Joystick : IControlPortDevice
{
    const int UpBit = 0x01, DownBit = 0x02, LeftBit = 0x04, RightBit = 0x08, FireBit = 0x10;

    int closed; // closed contacts, one bit per switch

    public bool Up { get => Get(UpBit); set => Set(UpBit, value); }
    public bool Down { get => Get(DownBit); set => Set(DownBit, value); }
    public bool Left { get => Get(LeftBit); set => Set(LeftBit, value); }
    public bool Right { get => Get(RightBit); set => Set(RightBit, value); }
    public bool Fire { get => Get(FireBit); set => Set(FireBit, value); }

    // All the switches at once (useful when the state comes all together, as from a gamepad)
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
