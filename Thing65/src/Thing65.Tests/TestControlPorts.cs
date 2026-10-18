using static Thing65.Tests.TestHelpers;

namespace Thing65.Tests;

// Control ports: the DE-9 socket, the devices and the wiring to CIA 1 and the SID
[TestClass]
public sealed class ControlPortTests
{
    const long CYCLES_PER_SECOND = 985248;
    const int FRAME_CYCLES = 312 * 63;
    const int POT_CYCLES = 512;

    // CIA 1 as the KERNAL leaves it: port A output, port B input
    static Board BoardWithKernalCiaSetup()
    {
        var board = NewBoard();
        board.ParkCpu();
        board.Poke(0xDC02, 0xFF);
        board.Poke(0xDC03, 0x00);
        board.Poke(0xDC00, 0x7F);
        return board;
    }

    // POT line switch: PA6 = port 1, PA7 = port 2
    static void SelectPotPort(Board board, int port) => board.Poke(0xDC00, port == 1 ? (byte)0x40 : (byte)0x80);

    #region Joystick

    [TestMethod]
    public void EmptyPorts_ShouldLeaveLinesHigh()
    {
        var board = BoardWithKernalCiaSetup();

        Assert.IsTrue(board.ControlPort1.IsEmpty && board.ControlPort2.IsEmpty);
        Assert.AreEqual(0x1F, board.Peek(0xDC00) & 0x1F);
        Assert.AreEqual(0x1F, board.Peek(0xDC01) & 0x1F);
    }

    [TestMethod]
    public void JoystickInPort2_ShouldPullPortALowEvenWhenOutput()
    {
        var board = BoardWithKernalCiaSetup();
        var joystick = new Joystick();
        board.ControlPort2.Plug(joystick);

        joystick.Up = true;
        Assert.AreEqual(0x1E, board.Peek(0xDC00) & 0x1F);

        joystick.Up = false;
        joystick.Right = true;
        joystick.Fire = true;
        Assert.AreEqual(0x07, board.Peek(0xDC00) & 0x1F);
        Assert.AreEqual(0x1F, board.Peek(0xDC01) & 0x1F); // port 1 is not involved
    }

    [TestMethod]
    public void JoystickInPort1_ShouldPullPortBLow()
    {
        var board = BoardWithKernalCiaSetup();
        var joystick = new Joystick();
        board.ControlPort1.Plug(joystick);

        joystick.SetState(up: false, down: true, left: true, right: false, fire: false);

        Assert.AreEqual(0x19, board.Peek(0xDC01) & 0x1F);
        Assert.AreEqual(0x1F, board.Peek(0xDC00) & 0x1F);
    }

    [TestMethod]
    public void JoystickInPort1_ShouldTypePhantomKeysDuringKeyboardScan()
    {
        // As on the real C64: the contacts of port 1 are on the same lines as the keyboard columns
        var board = NewBoard();
        board.RunCycles(3 * CYCLES_PER_SECOND);
        board.Load(0xC000, 0x4C, 0x00, 0xC0);
        board.Cpu.Status = 0x20;  // IRQs enabled: the KERNAL scans the keyboard
        board.Poke(0x00C6, 0);
        var joystick = new Joystick();
        board.ControlPort1.Plug(joystick);

        joystick.Fire = true;
        board.RunCycles(3 * FRAME_CYCLES);

        Assert.IsTrue(board.Peek(0x00C6) > 0, "no phantom key in the buffer");
    }

    [TestMethod]
    public void Port_ShouldAcceptOneDeviceAtATime()
    {
        var board = NewBoard();
        var joystick = new Joystick();
        board.ControlPort2.Plug(joystick);

        Assert.ThrowsException<InvalidOperationException>(() => board.ControlPort2.Plug(new Paddles()));
        Assert.AreSame(joystick, board.ControlPort2.Unplug());
        Assert.IsTrue(board.ControlPort2.IsEmpty);
    }

    #endregion

    #region Paddles and SID

    [TestMethod]
    public void Paddles_ShouldBeReadByTheSidThroughThePotSwitch()
    {
        var board = BoardWithKernalCiaSetup();
        var port1 = new Paddles { PositionA = 100, PositionB = 200 };
        var port2 = new Paddles { PositionA = 10, PositionB = 20 };
        board.ControlPort1.Plug(port1);
        board.ControlPort2.Plug(port2);

        SelectPotPort(board, 1);
        board.RunCycles(POT_CYCLES);
        Assert.AreEqual(100, (int)board.Peek(0xD419));
        Assert.AreEqual(200, (int)board.Peek(0xD41A));

        SelectPotPort(board, 2);
        board.RunCycles(POT_CYCLES);
        Assert.AreEqual(10, (int)board.Peek(0xD419));
        Assert.AreEqual(20, (int)board.Peek(0xD41A));

        board.Poke(0xDC00, 0x00); // no port selected: lines floating
        board.RunCycles(POT_CYCLES);
        Assert.AreEqual(0xFF, (int)board.Peek(0xD419));
    }

    [TestMethod]
    public void Sid_ShouldUpdatePotRegistersEvery512Cycles()
    {
        var board = BoardWithKernalCiaSetup();
        var paddles = new Paddles { PositionA = 50 };
        board.ControlPort1.Plug(paddles);
        SelectPotPort(board, 1);
        board.RunCycles(POT_CYCLES);
        Assert.AreEqual(50, (int)board.Peek(0xD419));

        paddles.PositionA = 60;
        board.RunCycles(100);
        Assert.AreEqual(50, (int)board.Peek(0xD419)); // measurement in progress: the previous value stays
        board.RunCycles(POT_CYCLES);
        Assert.AreEqual(60, (int)board.Peek(0xD419));
    }

    [TestMethod]
    public void PaddleButtons_ShouldUseLeftAndRightPins()
    {
        var board = BoardWithKernalCiaSetup();
        var paddles = new Paddles();
        board.ControlPort1.Plug(paddles);

        paddles.FireA = true;
        Assert.AreEqual(0x1B, board.Peek(0xDC01) & 0x1F); // pin 3 (left)
        paddles.FireA = false;
        paddles.FireB = true;
        Assert.AreEqual(0x17, board.Peek(0xDC01) & 0x1F); // pin 4 (right)
    }

    #endregion

    #region Mouse 1351

    [TestMethod]
    public void Mouse1351_ShouldPresentPositionModulo64OnPotLines()
    {
        var board = BoardWithKernalCiaSetup();
        var mouse = new Mouse1351();
        board.ControlPort1.Plug(mouse);
        SelectPotPort(board, 1);

        mouse.Move(5, -3);
        board.RunCycles(POT_CYCLES);

        Assert.AreEqual(5 << 1, (int)board.Peek(0xD419));
        Assert.AreEqual((-3 & 0x3F) << 1, (int)board.Peek(0xD41A));

        mouse.Move(60, 0); // 65 mod 64 = 1
        board.RunCycles(POT_CYCLES);
        Assert.AreEqual(1 << 1, (int)board.Peek(0xD419));
    }

    [TestMethod]
    public void Mouse1351_ButtonsShouldUseFireAndUpPins()
    {
        var board = BoardWithKernalCiaSetup();
        var mouse = new Mouse1351();
        board.ControlPort1.Plug(mouse);

        mouse.LeftButton = true;
        Assert.AreEqual(0x0F, board.Peek(0xDC01) & 0x1F);
        mouse.LeftButton = false;
        mouse.RightButton = true;
        Assert.AreEqual(0x1E, board.Peek(0xDC01) & 0x1F);
    }

    #endregion
}
