using ThingVCS;
using ThingRetroEmu.Chips;
using ThingVCS7800.A7800;
namespace ThingVCS7800.Tests;
public class SallyTests
{
    sealed class Memory : IBus
    {
        public byte[] Data = new byte[65536];
        public int Accesses;
        public byte Read(ushort address) { Accesses++; return Data[address]; }
        public void Write(ushort address, byte value) { Accesses++; Data[address] = value; }
    }
    [Fact] public void HaltBlocksBusAccessButNmiEdgeIsRetained()
    {
        var memory = new Memory();
        byte[] main = [0x4c,0,0xc0]; main.CopyTo(memory.Data, 0xc000);
        byte[] handler = [0xee,0,0x20,0x40]; handler.CopyTo(memory.Data, 0xc600);
        memory.Data[0xfffd] = 0xc0; memory.Data[0xfffb] = 0xc6;
        bool halt = true, nmi = false;
        var cpu = new Sally { Bus = memory, HaltInput = () => halt, NmiInput = () => nmi }; cpu.Reset();
        int before = memory.Accesses;
        for (int i = 0; i < 100; i++) { nmi = i == 10; cpu.Tick(); }
        Assert.Equal(before, memory.Accesses); Assert.Equal(0, memory.Data[0x2000]);
        halt = false;
        for (int i = 0; i < 500; i++) cpu.Tick();
        Assert.Equal(1, memory.Data[0x2000]);
    }
    [Fact] public void ComputerInstallsSallyAndDisconnectReleasesHalt()
    {
        var host = new DevelopmentComputer(); var cpu = Assert.IsType<Sally>(host.Board.CpuSocket.Chip);
        host.Board.Write(0x24, 0); Assert.True(cpu.HaltInput());
        host.Board.CpuSocket.Remove(); Assert.False(cpu.HaltInput()); Assert.Null(cpu.Bus);
    }
    [Fact] public void MariaBusyBudgetExpiresWhileRasterRuns()
    {
        var host = new DevelopmentComputer(); host.Insert(DevelopmentRoms.DirectObjects());
        for (int i = 0; i < 10000 && !host.Board.MariaSocket.Chip!.DmaBusy; i++) host.Board.Tick();
        var maria = Assert.IsType<Maria>(host.Board.MariaSocket.Chip);
        Assert.True(maria.DmaBusy); Assert.True(Assert.IsType<Sally>(host.Board.CpuSocket.Chip).HaltInput());
        Assert.InRange(maria.Dot, Maria.DmaStartDot, Maria.DmaStartDot + 6);
        int line = maria.Scanline;
        for (int i = 0; i < 20; i++) host.Board.Tick();
        Assert.False(maria.DmaBusy); Assert.Equal(line, maria.Scanline);
        host.Reset(); Assert.False(maria.DmaBusy); Assert.Equal(0, maria.LastDmaClocks);
    }
    [Fact] public void DisabledDmaHasNoBusyWindow()
    {
        var host = new DevelopmentComputer(); host.Board.Write(0x3c, 0x60);
        for (int i = 0; i < 2000; i++) host.Board.Tick();
        Assert.False(host.Board.MariaSocket.Chip!.DmaBusy);
    }
}
