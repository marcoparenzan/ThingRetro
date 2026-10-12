namespace Fake64.C64;
public class Ram64KbChip
{
    public Ram64KbChip()
    {
        Reset();
    }

    public void Reset()
    {
    }

    public void Clock(long ticks)
    {
    }

    byte[] bytes = new byte[0x10000];

    public byte Address(ushort addr)
    {
        return bytes[addr];
    }

    public void Address(ushort addr, byte value)
    {
        bytes[addr] = value;
    }
}
