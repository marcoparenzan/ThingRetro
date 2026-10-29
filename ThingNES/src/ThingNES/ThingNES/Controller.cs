namespace ThingNES;

[Flags]
public enum Buttons : byte { None = 0, A = 1, B = 2, Select = 4, Start = 8, Up = 16, Down = 32, Left = 64, Right = 128 }

public sealed class Controller
{
    bool strobe;
    byte shift;
    public Buttons Pressed { get; set; }
    public void Write(byte value)
    {
        bool next = (value & 1) != 0;
        if (strobe || next) shift = (byte)Pressed;
        strobe = next;
    }
    public byte Read()
    {
        if (strobe) return (byte)((byte)Pressed & 1);
        byte value = (byte)(shift & 1);
        shift = (byte)((shift >> 1) | 0x80);
        return value;
    }
}
