namespace Fake64;

public class Ram8KbChip
{
    Board board;

    public Ram8KbChip(Board board)
    {
        this.board = board;
        Reset();
    }

    public void Reset()
    {
    }

    public void Clock()
    {
    }

    byte[] bytes = new byte[0x2000];

    public byte Address(ushort addr)
    {
        return bytes[addr];
    }

    public void Address(ushort addr, byte value)
    {
        bytes[addr] = value;
    }
}
