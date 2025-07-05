namespace Fake64;
public class Ram64KbChip
{
    Board board;

    public Ram64KbChip(Board board)
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
