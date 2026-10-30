using ThingVCS;
using ThingRetroEmu.Ports;
using ThingVCS7800.A7800;
namespace ThingVCS7800.Tests;
public class TwoButtonControllerTests
{
    [Theory][InlineData(1, false, 9)][InlineData(1, true, 8)][InlineData(2, false, 11)][InlineData(2, true, 10)]
    public void NativeButtonsDriveCorrectTiaComparators(int port, bool second, int register)
    {
        var host = new DevelopmentComputer(); var joystick = new Joystick();
        (port == 1 ? host.Board.ControlPort1 : host.Board.ControlPort2).Plug(joystick);
        host.Board.Write(0x283, 0x14); host.Board.Write(0x282, 0); // PB2/PB4 outputs low.
        if (second) joystick.Fire2 = true; else joystick.Fire = true;
        for (int input = 8; input <= 11; input++)
        {
            host.Board.Write(0x1800, 0x55); // Known held low bits.
            Assert.Equal((input == register ? 0x80 : 0) | 0x55, host.Board.Read((ushort)input));
            host.Board.Write(0x1800, 0x55);
            Assert.Equal((input == register ? 0x80 : 0) | 0x55, host.Board.Read((ushort)(0x300 + input)));
        }
        Assert.Equal(0x80, host.Board.Read((ushort)(port == 1 ? 12 : 13)) & 0x80);
    }
    [Theory][InlineData(1)][InlineData(2)]
    public void RiotsDdrAndLatchSelectModesPerPortAndCombineBothButtons(int port)
    {
        var host = new DevelopmentComputer(); var joystick = new Joystick { Fire2 = true };
        (port == 1 ? host.Board.ControlPort1 : host.Board.ControlPort2).Plug(joystick);
        ushort fire = (ushort)(port == 1 ? 12 : 13); byte bit = port == 1 ? (byte)4 : (byte)16;
        Assert.Equal(0, host.Board.Read(fire) & 0x80); // Input defaults to compatibility mode.
        host.Board.Write(0x282, 0); Assert.Equal(0, host.Board.Read(fire) & 0x80);
        host.Board.Write(0x283, bit); Assert.Equal(0x80, host.Board.Read(fire) & 0x80);
        host.Board.Write(0x282, bit); Assert.Equal(0, host.Board.Read(fire) & 0x80);
        joystick.Fire2 = false; joystick.Fire = true; Assert.Equal(0, host.Board.Read(fire) & 0x80);
        joystick.ReleaseAll(); Assert.Equal(0x80, host.Board.Read(fire) & 0x80);
    }
    [Fact] public void ComparatorInputsAreDisconnectedOnChipRemovalAndDoNotAffectAudio()
    {
        var host = new DevelopmentComputer(); var joystick = new Joystick { Fire = true, Fire2 = true };
        host.Board.ControlPort1.Plug(joystick); var tia = Assert.IsType<TiaInputs>(host.Board.TiaInputSocket.Chip);
        host.Board.Write(0x19, 15); Assert.Equal(.5, tia.AudioLevel);
        Assert.True(tia.ButtonInputHigh(0)); Assert.True(tia.ButtonInputHigh(1));
        host.Board.TiaInputSocket.Remove(); Assert.False(tia.ButtonInputHigh(0)); Assert.True(tia.Fire1High());
    }
    [Fact] public void SecondButtonDoesNotInventUniversalDigitalPin()
    {
        var joystick = new Joystick { Fire2 = true }; Assert.Equal(0xff, joystick.DigitalLines);
        Assert.True(((ITwoButtonJoystick)joystick).Fire2);
        joystick.ReleaseAll(); Assert.False(joystick.Fire2);
    }
}
