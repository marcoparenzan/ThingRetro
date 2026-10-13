namespace Thing64.C64;
public class ColorRamChip
{
    public ColorRamChip()
    {
        Reset();
    }

    public void Reset()
    {
    }

    public void Clock(long ticks)
    {
    }

    byte[] bytes = new byte[0x0400];

    public byte Address(ushort addr)
    {
        return bytes[addr];
    }

    public void Address(ushort addr, byte value)
    {
        bytes[addr] = value;
    }
}
