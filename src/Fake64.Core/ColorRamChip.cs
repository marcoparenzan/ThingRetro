namespace Fake64;
public class ColorRamChip
{
    Board board;

    public ColorRamChip(Board board)
    {
        this.board = board;
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
