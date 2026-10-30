using ThingMOS;
using ThingVCS;
using ThingVCS7800.A7800;
namespace ThingVCS7800.Tests;
public class BoardTests
{
    [Fact] public void SocketWiresAndDisconnectsCpu()
    {
        var board = new Board(); var cpu = new MOS6502();
        board.CpuSocket.Insert(cpu); Assert.Same(board, cpu.Bus);
        Assert.Throws<InvalidOperationException>(() => board.CpuSocket.Insert(new MOS6502()));
        Assert.Same(cpu, board.CpuSocket.Remove()); Assert.Null(cpu.Bus);
    }
    [Fact] public void RamAndRomAreSeparate()
    {
        var board = new Board(); var rom = new byte[16384]; rom[0] = 42;
        board.CartridgeSocket.Insert(new LinearCartridge(rom)); rom[0] = 99;
        board.Write(0x1800, 17); Assert.Equal(17, board.Read(0x1800));
        board.Write(0xc000, 55); Assert.Equal(42, board.Read(0xc000));
    }
    [Fact] public void CpuExecutesDevelopmentRom()
    {
        var rom = new byte[16384];
        byte[] program = [0xa9, 42, 0x8d, 0x00, 0x18, 0x4c, 0x05, 0xc0];
        program.CopyTo(rom, 0); rom[0x3ffc] = 0; rom[0x3ffd] = 0xc0;
        var board = new Board(); board.CartridgeSocket.Insert(new LinearCartridge(rom));
        board.CpuSocket.Insert(new MOS6502()); board.Reset();
        for (int i = 0; i < 100; i++) board.Tick();
        Assert.Equal(42, board.Read(0x1800)); Assert.Equal(100, board.Cycles);
    }
    [Theory][InlineData(0)][InlineData(128)][InlineData(65536)]
    public void RejectsUnsupportedRom(int size) => Assert.Throws<ArgumentException>(() => new LinearCartridge(new byte[size]));
}
