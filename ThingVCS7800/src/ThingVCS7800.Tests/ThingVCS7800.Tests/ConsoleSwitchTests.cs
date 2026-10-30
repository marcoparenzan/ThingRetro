using ThingVCS;
using ThingVCS7800.A7800;
namespace ThingVCS7800.Tests;

public class ConsoleSwitchTests
{
    [Theory]
    [InlineData(false, false, false, 0x3f)]
    [InlineData(true, false, false, 0x3e)]
    [InlineData(false, true, false, 0x3d)]
    [InlineData(false, false, true, 0x37)]
    [InlineData(true, true, true, 0x34)]
    public void ButtonsReachSwchb(bool reset, bool select, bool pause, int expected)
    {
        var board = new Board(); board.RiotSocket.Insert(new Riot6532());
        board.Console.ResetPressed = reset; board.Console.SelectPressed = select; board.Console.PausePressed = pause;
        Assert.Equal(expected, board.Read(0x282));
    }
    [Theory]
    [InlineData(false, false, 0x3f)] [InlineData(true, false, 0x7f)]
    [InlineData(false, true, 0xbf)] [InlineData(true, true, 0xff)]
    public void DifficultyContactsAreIndependent(bool left, bool right, int expected)
    {
        var contacts = new ConsoleSwitches { LeftDifficultyA = left, RightDifficultyA = right };
        Assert.Equal(expected, contacts.Read7800Contacts());
    }
    [Fact] public void OutputDirectionOverridesContactsAndRemovalDisconnectsPins()
    {
        var board = new Board(); var riot = new Riot6532(); board.RiotSocket.Insert(riot);
        board.Console.ResetPressed = true;
        board.Write(0x283, 1); board.Write(0x282, 1);
        Assert.Equal(0x3f, board.Read(0x282));
        board.RiotSocket.Remove(); riot.WriteRegister(3, 0);
        Assert.Equal(255, riot.ReadRegister(2));
    }
    [Fact] public void ReleaseButtonsPreservesDifficultyAndReleasesAllContacts()
    {
        var contacts = new ConsoleSwitches
        {
            ResetPressed = true, SelectPressed = true, PausePressed = true,
            LeftDifficultyA = true, RightDifficultyA = true
        };
        Assert.Equal(0xf4, contacts.Read7800Contacts());
        contacts.ReleaseButtons();
        Assert.Equal(0xff, contacts.Read7800Contacts());
        contacts.ReleaseButtons();
        Assert.True(contacts.LeftDifficultyA);
        Assert.True(contacts.RightDifficultyA);
    }
    [Fact] public void HardwareResetDoesNotMovePhysicalSwitches()
    {
        var board = new Board(); board.RiotSocket.Insert(new Riot6532());
        board.Console.SelectPressed = true; board.Console.LeftDifficultyA = true;
        board.Reset(); Assert.Equal(0x7d, board.Read(0x282));
        board.Console.ResetPressed = true;
        Assert.Equal(0x7c, board.Read(0x282));
        Assert.Equal(0, board.Cycles);
    }
}
