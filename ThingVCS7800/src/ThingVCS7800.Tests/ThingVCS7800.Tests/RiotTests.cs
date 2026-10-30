using ThingVCS;
using ThingMOS;
using ThingVCS7800.A7800;
namespace ThingVCS7800.Tests;
public class RiotTests
{
    [Fact] public void DirectionsMixInputsAndOutputLatches()
    {
        var chip = new Riot6532 { PortAInput = () => 0x55, PortBInput = () => 0xaa };
        chip.WriteRegister(0, 0xf0); chip.WriteRegister(1, 0xf0);
        Assert.Equal(0xf5, chip.ReadRegister(0));
        chip.WriteRegister(2, 0x0f); chip.WriteRegister(3, 0x0f);
        Assert.Equal(0xaf, chip.ReadRegister(2));
        Assert.Equal(0xf0, chip.PortAOutput); Assert.Equal(15, chip.PortBDirection);
    }
    [Theory][InlineData(0,1)][InlineData(1,8)][InlineData(2,64)][InlineData(3,1024)]
    public void TimerUnderflowsAtSelectedInterval(int rate, int interval)
    {
        var chip = new Riot6532(); chip.WriteRegister(0x1c + rate, 1);
        for (int i = 0; i < interval * 2 - 1; i++) chip.Tick();
        Assert.False(chip.Irq); chip.Tick(); Assert.True(chip.Irq);
        Assert.Equal(128, chip.ReadRegister(5)); Assert.True(chip.Irq);
        Assert.Equal(255, chip.ReadRegister(12)); Assert.False(chip.Irq);
        chip.Tick(); Assert.Equal(254, chip.ReadRegister(12));
    }
    [Fact] public void DisabledInterruptStillSetsFlagAndReloadClearsIt()
    {
        var chip = new Riot6532(); chip.WriteRegister(0x14, 0); chip.Tick();
        Assert.False(chip.Irq); Assert.Equal(128, chip.ReadRegister(5));
        chip.WriteRegister(0x1c, 3); Assert.Equal(0, chip.ReadRegister(5));
        Assert.False(chip.Irq);
    }
    [Fact] public void ResetPreservesRamAndExternalPins()
    {
        var chip = new Riot6532 { PortAInput = () => 42 };
        chip.WriteRam(127, 9); chip.WriteRegister(1, 255);
        chip.WriteRegister(0x1c, 0); chip.Tick(); chip.Reset();
        Assert.False(chip.Irq); Assert.Equal(9, chip.ReadRam(255));
        Assert.Equal(42, chip.ReadRegister(0));
    }
    [Fact] public void BoardRoutesRegistersClockAndInterruptAndDisconnectsCpu()
    {
        var board = new Board(); var cpu = new MOS6502(); var riot = new Riot6532();
        board.RiotSocket.Insert(riot); board.CpuSocket.Insert(cpu);
        board.Write(0x029c, 0); board.Tick(); Assert.True(cpu.IrqInput());
        board.CpuSocket.Remove(); Assert.False(cpu.IrqInput());
        Assert.Equal(128, board.Read(0x0285)); Assert.Equal(255, board.Read(0x028c));
        Assert.False(riot.Irq); board.RiotSocket.Remove();
        board.Tick(); // Empty socket does not throw or invent a chip.
    }
}
