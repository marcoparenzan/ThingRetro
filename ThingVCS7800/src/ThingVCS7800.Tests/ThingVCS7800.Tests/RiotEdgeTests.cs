using ThingVCS;
using ThingVCS7800.A7800;
using ThingRetroEmu.Ports;
namespace ThingVCS7800.Tests;
public class RiotEdgeTests
{
    [Theory][InlineData(4,false,false)][InlineData(5,true,false)][InlineData(6,false,true)][InlineData(7,true,true)]
    public void PolarityAndEnableAreAddressSelected(int address, bool rising, bool enabled)
    {
        byte pins = rising ? (byte)0 : (byte)255;
        var chip = new Riot6532 { PortAInput = () => pins }; chip.Reset();
        chip.WriteRegister(address, 255); chip.Tick(); Assert.False(chip.Irq);
        pins = rising ? (byte)255 : (byte)0; chip.Tick();
        Assert.Equal(enabled, chip.Irq); Assert.Equal(64, chip.ReadRegister(5));
        Assert.False(chip.Irq); Assert.Equal(0, chip.ReadRegister(5));
    }
    [Fact] public void OppositeEdgeDoesNotSetFlag()
    {
        byte pins = 0; var chip = new Riot6532 { PortAInput = () => pins };
        chip.Reset(); chip.WriteRegister(6, 0); pins = 255; chip.Tick();
        Assert.False(chip.Irq); Assert.Equal(0, chip.ReadRegister(5));
    }
    [Fact] public void TimerAndEdgeAcknowledgementsAreIndependent()
    {
        byte pins = 255; var chip = new Riot6532 { PortAInput = () => pins };
        chip.Reset(); chip.WriteRegister(6, 0); chip.WriteRegister(0x1c, 0);
        pins = 0; chip.Tick(); Assert.True(chip.Irq);
        Assert.Equal(192, chip.ReadRegister(5)); Assert.True(chip.Irq);
        Assert.Equal(128, chip.ReadRegister(5));
        chip.ReadRegister(12); Assert.False(chip.Irq);
        pins = 255; chip.Tick(); pins = 0; chip.Tick(); Assert.True(chip.Irq);
        chip.ReadRegister(4); Assert.True(chip.Irq); // Timer read cannot acknowledge edge.
        Assert.Equal(64, chip.ReadRegister(5)); Assert.False(chip.Irq);
    }
    [Fact] public void OutputPinTransitionsAreObservedAndResetDoesNotInventAnEdge()
    {
        var chip = new Riot6532(); chip.Reset(); chip.WriteRegister(6, 0);
        chip.WriteRegister(1, 128); chip.WriteRegister(0, 0); chip.Tick();
        Assert.True(chip.Irq); chip.Reset(); chip.Tick();
        Assert.False(chip.Irq); Assert.Equal(0, chip.ReadRegister(5));
    }
    [Fact] public void JoystickRightContactReachesPa7OnBoard()
    {
        var host = new DevelopmentComputer(); var joystick = new Joystick();
        host.Board.ControlPort1.Plug(joystick); host.Reset();
        host.Board.Write(0x0286, 0); joystick.Right = true; host.Board.Tick();
        Assert.True(host.Board.CpuSocket.Chip!.IrqInput());
        Assert.Equal(64, host.Board.Read(0x0285));
        Assert.False(host.Board.CpuSocket.Chip.IrqInput());
    }
}
