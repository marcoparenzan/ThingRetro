using ThingVCS;
using ThingVCS7800.A7800;
using ThingRetroEmu.Ports;
namespace ThingVCS7800.Tests;
public class ControllerTests
{
    [Theory]
    [InlineData(1,0,0xef)][InlineData(1,1,0xdf)][InlineData(1,2,0xbf)][InlineData(1,3,0x7f)]
    [InlineData(2,0,0xfe)][InlineData(2,1,0xfd)][InlineData(2,2,0xfb)][InlineData(2,3,0xf7)]
    public void DirectionsReachCorrectActiveLowPins(int port, int direction, int expected)
    {
        var board = new Board(); board.RiotSocket.Insert(new Riot6532());
        var joystick = new Joystick();
        joystick.SetState(direction == 0, direction == 1, direction == 2, direction == 3, false);
        (port == 1 ? board.ControlPort1 : board.ControlPort2).Plug(joystick);
        Assert.Equal(expected, board.Read(0x0280));
    }
    [Fact] public void PortsAreIndependentAndRemovalReleasesContacts()
    {
        var board = new Board(); board.RiotSocket.Insert(new Riot6532());
        var first = new Joystick { Up = true }; var second = new Joystick { Right = true };
        board.ControlPort1.Plug(first); board.ControlPort2.Plug(second);
        Assert.Equal(0xe7, board.Read(0x0280));
        board.ControlPort1.Unplug(); Assert.Equal(0xf7, board.Read(0x0280));
        second.ReleaseAll(); Assert.Equal(255, board.Read(0x0280));
    }
    [Fact] public void OutputDirectionsOverrideInputAndDetachedChipHasFloatingPins()
    {
        var board = new Board(); var riot = new Riot6532(); board.RiotSocket.Insert(riot);
        board.ControlPort1.Plug(new Joystick { Up = true });
        board.Write(0x0281, 0xf0); board.Write(0x0280, 0xa0);
        Assert.Equal(0xaf, board.Read(0x0280));
        board.RiotSocket.Remove(); riot.WriteRegister(1, 0);
        Assert.Equal(255, riot.ReadRegister(0));
    }
    [Fact] public void FireDoesNotInventUnimplementedTiaInput()
    {
        var board = new Board(); board.RiotSocket.Insert(new Riot6532());
        board.ControlPort1.Plug(new Joystick { Fire = true });
        Assert.Equal(255, board.Read(0x0280));
    }
}
