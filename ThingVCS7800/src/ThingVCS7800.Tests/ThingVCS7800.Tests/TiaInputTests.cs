using ThingVCS;
using ThingRetroEmu.Ports;
using ThingVCS7800.A7800;
namespace ThingVCS7800.Tests;
public class TiaInputTests
{
    [Theory][InlineData(0)][InlineData(0x55)][InlineData(0x80)][InlineData(0xff)]
    public void InputsDriveOnlyD7(int bus)
    {
        var chip = new TiaInputs { Fire1High = () => false };
        Assert.Equal(bus & 127, chip.ReadRegister(12, (byte)bus));
        Assert.Equal((bus & 127) | 128, chip.ReadRegister(13, (byte)bus));
    }
    [Fact] public void DisabledLatchTracksRelease()
    {
        bool high = false; var chip = new TiaInputs { Fire1High = () => high };
        Assert.Equal(0, chip.ReadRegister(12, 0)); high = true;
        Assert.Equal(128, chip.ReadRegister(12, 0));
    }
    [Fact] public void EnabledLatchCapturesPulseBetweenReadsAndClearsWhenDisabled()
    {
        bool high = true; var chip = new TiaInputs { Fire1High = () => high };
        chip.WriteRegister(1, 0x40); high = false; chip.Tick(); high = true;
        Assert.Equal(0, chip.ReadRegister(12, 0)); Assert.Equal(128, chip.ReadRegister(13, 0));
        chip.WriteRegister(1, 0); Assert.Equal(128, chip.ReadRegister(12, 0));
    }
    [Fact] public void ResetClearsLatchButPreservesInputConnection()
    {
        bool high = false; var chip = new TiaInputs { Fire2High = () => high };
        chip.WriteRegister(1, 0x40); high = true; chip.Reset();
        Assert.Equal(128, chip.ReadRegister(13, 0)); high = false;
        Assert.Equal(0, chip.ReadRegister(13, 0));
    }
    [Fact] public void UnsupportedRegistersDoNotInventState()
    {
        var chip = new TiaInputs(); chip.WriteRegister(0x41, 0x40);
        Assert.Equal(42, chip.ReadRegister(4, 42));
    }
    [Fact] public void BoardRoutesBothFirePinsAndDisconnectedChipFloats()
    {
        var host = new DevelopmentComputer(); var board = host.Board;
        var first = new Joystick { Fire = true }; var second = new Joystick();
        board.ControlPort1.Plug(first); board.ControlPort2.Plug(second);
        board.Write(0x1800, 0x35); Assert.Equal(0x35, board.Read(12));
        Assert.Equal(0xb5, board.Read(13)); second.Fire = true;
        Assert.Equal(0x35, board.Read(13));
        first.ReleaseAll(); Assert.Equal(0xb5, board.Read(12));
        var detached = board.TiaInputSocket.Remove()!; detached.Reset();
        Assert.Equal(128, detached.ReadRegister(13, 0));
        board.Write(0x1800, 42); Assert.Equal(42, board.Read(13));
    }
    [Fact] public void BoardClockAndResetReachLatch()
    {
        var host = new DevelopmentComputer(); var joystick = new Joystick();
        host.Board.ControlPort1.Plug(joystick); host.Board.Write(1, 0x40);
        joystick.Fire = true; host.Board.Tick(); joystick.Fire = false;
        host.Board.Write(0x1800, 0); Assert.Equal(0, host.Board.Read(12));
        host.Reset(); host.Board.Write(0x1800, 0); Assert.Equal(128, host.Board.Read(12));
    }
}
