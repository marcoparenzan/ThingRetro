using ThingVCS;
using ThingVCS7800.A7800;
namespace ThingVCS7800.Tests;
public class BoardRamTests
{
    [Theory][InlineData(0x40,0x2040)][InlineData(0xff,0x20ff)][InlineData(0x140,0x2140)][InlineData(0x1ff,0x21ff)]
    public void LowAndCanonicalAddressesShareStorage(int low, int high)
    {
        var board = new Board(); board.Write((ushort)low, 42);
        Assert.Equal(42, board.Read((ushort)high));
        board.Write((ushort)high, 99); Assert.Equal(99, board.Read((ushort)low));
        board.Reset(); Assert.Equal(99, board.Read((ushort)low));
    }
    [Fact] public void ZeroPageAndStackAliasesAreIndependent()
    {
        var board = new Board(); board.Write(0x40, 1); board.Write(0x140, 2);
        Assert.Equal(1, board.Read(0x2040)); Assert.Equal(2, board.Read(0x2140));
    }
    [Fact] public void UnimplementedLowIoIsNotInventedAsRam()
    {
        var board = new Board(); board.Write(0x213f, 42); board.Write(0x13f, 99);
        Assert.Equal(42, board.Read(0x213f));
        board.Write(0x2000, 17); board.Write(0, 66); Assert.Equal(17, board.Read(0x2000));
    }
    [Fact] public void CpuPushPullAndSubroutineReturnUseBoardStack()
    {
        var rom = new byte[16384];
        byte[] main = [0xa2,0xff,0x9a,0xa9,42,0x48,0x68,0x8d,0x00,0x18,0x20,0x20,0xc0,0x8d,0x01,0x18,0x4c,0x10,0xc0];
        main.CopyTo(rom, 0); byte[] sub = [0xa9,55,0x60]; sub.CopyTo(rom, 0x20);
        rom[0x3ffc] = 0; rom[0x3ffd] = 0xc0;
        var host = new DevelopmentComputer(); host.Insert(rom);
        for (int i = 0; i < 1000; i++) host.Board.Tick();
        Assert.Equal(42, host.Board.Read(0x1800)); Assert.Equal(55, host.Board.Read(0x1801));
        Assert.Equal(host.Board.Read(0x1ff), host.Board.Read(0x21ff));
    }
    [Fact] public void RepeatedDliHandlersReturnOnActualBoard()
    {
        var host = new DevelopmentComputer(); host.Insert(DevelopmentRoms.DisplayListInterrupts(startupDli: false));
        for (int i = 0; i < 30000; i++) host.Board.Tick();
        var maria = Assert.IsType<Maria>(host.Board.MariaSocket.Chip);
        // One post-render edge per visible transition; no provisional startup edge.
        Assert.Equal(240, maria.DisplayListInterrupts); Assert.Equal(240, host.Board.Read(0x1800));
        for (int i = 0; i < 30000; i++) host.Board.Tick();
        Assert.Equal(480, maria.DisplayListInterrupts); Assert.Equal(224, host.Board.Read(0x1800));
        Assert.Equal(0, maria.UnsupportedLines);
    }
}
