using static ThingOS64.Tests.OsBoard;

namespace ThingOS64.Tests;

[TestClass]
public sealed class TestInput
{
    static (int X, int Y) Pointer(Board board) => (board.PeekWord(Symbol("pointer_x")), board.PeekWord(Symbol("pointer_y")));

    static (int X, int Y) Sprite(Board board, int sprite) =>
        (board.Peek(0xD000 + sprite * 2) | (board.Peek(0xD010) >> sprite & 1) << 8, board.Peek(0xD001 + sprite * 2));

    [TestMethod]
    public void Pointer_ShouldStartInTheMiddleWithBothSpritesOnIt()
    {
        var board = Booted();

        Assert.AreEqual((160, 100), Pointer(board));
        Assert.AreEqual((160 + 24, 100 + 50), Sprite(board, 0));
        Assert.AreEqual((160 + 24, 100 + 50), Sprite(board, 1));
    }

    [TestMethod]
    public void Mouse1351_ShouldMoveThePointer()
    {
        var board = New();
        var mouse = new Mouse1351();
        board.ControlPort1.Plug(mouse);
        board.RunCycles(CYCLES_PER_SECOND / 2);

        mouse.Move(12, 0);
        board.RunFrames(2);
        mouse.Move(0, 7);               // the mouse counts upwards
        board.RunFrames(2);
        mouse.Move(-30, -20);
        board.RunFrames(2);

        Assert.AreEqual((160 + 12 - 30, 100 - 7 + 20), Pointer(board));
    }

    [TestMethod]
    public void Mouse1351_ShouldMoveFarInSmallSteps()
    {
        var board = New();
        var mouse = new Mouse1351();
        board.ControlPort1.Plug(mouse);
        board.RunCycles(CYCLES_PER_SECOND / 2);

        for (int i = 0; i < 10; i++)
        {
            mouse.Move(20, -10);
            board.RunFrames(1);
        }

        Assert.AreEqual((319, 199), Pointer(board), "stopped at the right and bottom edges");
        var (x, y) = Sprite(board, 0);
        Assert.AreEqual((319 + 24, 199 + 50), (x, y));
    }

    [TestMethod]
    public void Mouse1351_ButtonsShouldBeLeftAndRight()
    {
        var board = New();
        var mouse = new Mouse1351();
        board.ControlPort1.Plug(mouse);
        board.RunCycles(CYCLES_PER_SECOND / 2);

        mouse.LeftButton = true;
        board.RunFrames(2);
        Assert.AreEqual(1, board.Peek(Symbol("buttons")));
        mouse.LeftButton = false;
        mouse.RightButton = true;
        board.RunFrames(2);
        Assert.AreEqual(2, board.Peek(Symbol("buttons")));
        mouse.RightButton = false;
        board.RunFrames(2);
        Assert.AreEqual(0, board.Peek(Symbol("buttons")));
        Assert.AreEqual(' ', (char)board.Peek(Symbol("last_key")), "the buttons are not keys");
    }

    [TestMethod]
    public void Joystick_ShouldMoveThePointerFasterWhenHeld()
    {
        var board = New();
        var joystick = new Joystick();
        board.ControlPort2.Plug(joystick);
        board.RunCycles(CYCLES_PER_SECOND / 2);

        joystick.Left = true;
        board.RunFrames(10);
        joystick.Left = false;
        board.RunFrames(1);
        Assert.AreEqual((150, 100), Pointer(board), "one pixel per frame at first");

        joystick.Down = true;
        board.RunFrames(30);
        joystick.Down = false;
        board.RunFrames(1);
        Assert.AreEqual((150, 100 + 15 + 15 * 3), Pointer(board), "three pixels per frame after 15 frames");

        joystick.Fire = true;
        board.RunFrames(2);
        Assert.AreEqual(1, board.Peek(Symbol("buttons")));
    }

    [TestMethod]
    public void Keyboard_ShouldTypeAsciiWithShiftAndCtrl()
    {
        var board = Booted();
        int lastKey = Symbol("last_key");

        board.Press(C64Key.A);
        Assert.AreEqual('a', (char)board.Peek(lastKey));
        board.Press(C64Key.LeftShift, C64Key.A);
        Assert.AreEqual('A', (char)board.Peek(lastKey));
        board.Press(C64Key.RightShift, C64Key.D1);
        Assert.AreEqual('!', (char)board.Peek(lastKey));
        board.Press(C64Key.Control, C64Key.C);
        Assert.AreEqual((char)3, (char)board.Peek(lastKey));
        board.Press(C64Key.Return);
        Assert.AreEqual((char)13, (char)board.Peek(lastKey));
        board.Press(C64Key.LeftShift, C64Key.CursorDown);
        Assert.AreEqual((char)0x81, (char)board.Peek(lastKey), "SHIFT + CRSR down is up");
    }

    [TestMethod]
    public void Keyboard_ShouldNotRepeatAHeldKeyNorTypeTheModifiers()
    {
        var board = Booted();
        int buffer = Symbol("key_tail");

        board.Keyboard.SetPressedKeys([C64Key.LeftShift, C64Key.Commodore, C64Key.Control]);
        board.RunFrames(5);
        board.Keyboard.ReleaseAll();
        board.RunFrames(2);
        Assert.AreEqual(' ', (char)board.Peek(Symbol("last_key")), "modifiers alone type nothing");

        board.Keyboard.SetPressedKeys([C64Key.Q]);
        board.RunFrames(20);
        Assert.AreEqual('q', (char)board.Peek(Symbol("last_key")));
        Assert.AreEqual(board.Peek(Symbol("key_head")), board.Peek(buffer), "one key, already read");
    }

    [TestMethod]
    public void Desktop_ShouldShowWhatTheDriversSee()
    {
        var board = Booted();

        board.Press(C64Key.Z);

        Assert.AreEqual(" ThingOS   File   Disk     160,100 -- z", board.ScreenRow(0).TrimEnd());
    }
}
