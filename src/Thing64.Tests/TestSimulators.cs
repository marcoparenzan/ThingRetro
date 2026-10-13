using Thing64.WinForms;
using static Thing64.Tests.TestHelpers;

namespace Thing64.Tests;

// Control device simulators: keyboard and USB joystick as joystick, mouse as paddles
[TestClass]
public sealed class SimulatorTests
{
    // Control that raises keyboard and mouse events as if it had the focus
    sealed class InputSource : Control
    {
        public void Down(Keys key) => OnKeyDown(new KeyEventArgs(key));
        public void Up(Keys key) => OnKeyUp(new KeyEventArgs(key));
        public void Blur() => OnLostFocus(EventArgs.Empty);
    }

    #region Keyboard joystick

    [TestMethod]
    [DataRow(Keys.NumPad8, true, false, false, false, false)]
    [DataRow(Keys.NumPad2, false, true, false, false, false)]
    [DataRow(Keys.NumPad4, false, false, true, false, false)]
    [DataRow(Keys.NumPad6, false, false, false, true, false)]
    [DataRow(Keys.NumPad9, true, false, false, true, false)]
    [DataRow(Keys.NumPad1, false, true, true, false, false)]
    [DataRow(Keys.NumPad0, false, false, false, false, true)]
    [DataRow(Keys.NumPad5, false, false, false, false, true)]
    public void NumericKeypad_ShouldCloseTheRightSwitches(Keys key, bool up, bool down, bool left, bool right, bool fire)
    {
        using var source = new InputSource();
        var joystick = new Joystick();
        using var simulator = new KeyboardJoystick(source, joystick, JoystickKeySet.NumericKeypad);

        source.Down(key);

        Assert.AreEqual((up, down, left, right, fire), (joystick.Up, joystick.Down, joystick.Left, joystick.Right, joystick.Fire));
        source.Up(key);
        Assert.AreEqual(0xFF, (int)joystick.DigitalLines);
    }

    [TestMethod]
    public void ArrowsAndCtrl_ShouldCombineKeysAndReleaseOnFocusLoss()
    {
        using var source = new InputSource();
        var joystick = new Joystick();
        using var simulator = new KeyboardJoystick(source, joystick, JoystickKeySet.ArrowsAndCtrl);

        source.Down(Keys.Up);
        source.Down(Keys.Left);
        source.Down(Keys.ControlKey);
        Assert.IsTrue(joystick.Up && joystick.Left && joystick.Fire);

        source.Blur();
        Assert.AreEqual(0xFF, (int)joystick.DigitalLines);
        Assert.IsFalse(simulator.UsesKey(Keys.NumPad8));
    }

    [TestMethod]
    public void JoystickKeys_ShouldNotReachTheC64Keyboard()
    {
        var board = NewBoard();
        using var source = new InputSource();
        using var selector = new ControlPortSelector(board, source);
        using var keyboard = new PcKeyboard(source, board.Keyboard) { Excluded = selector.UsesKey };
        selector.Select(2, new(ControlPortSource.ArrowsJoystick));

        source.Down(Keys.Up);

        Assert.IsFalse(board.Keyboard.IsPressed(C64Key.CursorDown)); // it is not the cursor key
        Assert.IsTrue(((Joystick)board.ControlPort2.Device!).Up);
        board.ParkCpu();
        board.Poke(0xDC02, 0xFF);                          // port A as output, as the KERNAL leaves it
        board.Poke(0xDC00, 0x7F);
        Assert.AreEqual(0x1E, board.Peek(0xDC00) & 0x1F);  // the C64 reads it from port 2
    }

    #endregion

    #region USB joystick

    static readonly UsbJoystick.AxisRange Axis = new(0, 65535);
    const int Center = 32767, Centered = 0xFFFF;

    [TestMethod]
    public void UsbJoystick_CenteredStickShouldCloseNothing()
    {
        Assert.AreEqual(new UsbJoystick.State(false, false, false, false, false), UsbJoystick.Map(Center, Center, Axis, Axis, Centered, 0));
        Assert.AreEqual(new UsbJoystick.State(false, false, false, false, false), UsbJoystick.Map(Center + 9000, Center - 9000, Axis, Axis, Centered, 0)); // dead zone
    }

    [TestMethod]
    public void UsbJoystick_StickShouldMapToDirections()
    {
        Assert.AreEqual(new UsbJoystick.State(true, false, true, false, false), UsbJoystick.Map(0, 0, Axis, Axis, Centered, 0));
        Assert.AreEqual(new UsbJoystick.State(false, true, false, true, false), UsbJoystick.Map(65535, 65535, Axis, Axis, Centered, 0));
    }

    [TestMethod]
    [DataRow(0, true, false, false, false)]
    [DataRow(4500, true, false, false, true)]
    [DataRow(9000, false, false, false, true)]
    [DataRow(18000, false, true, false, false)]
    [DataRow(27000, false, false, true, false)]
    [DataRow(31500, true, false, true, false)]
    public void UsbJoystick_PadShouldMapToDirections(int pov, bool up, bool down, bool left, bool right)
    {
        Assert.AreEqual(new UsbJoystick.State(up, down, left, right, false), UsbJoystick.Map(Center, Center, Axis, Axis, pov, 0));
    }

    [TestMethod]
    public void UsbJoystick_ButtonsOneToFourShouldFire()
    {
        Assert.IsTrue(UsbJoystick.Map(Center, Center, Axis, Axis, Centered, 0x01).Fire);
        Assert.IsTrue(UsbJoystick.Map(Center, Center, Axis, Axis, Centered, 0x08).Fire);
        Assert.IsFalse(UsbJoystick.Map(Center, Center, Axis, Axis, Centered, 0x10).Fire);
    }

    #endregion

    #region Mouse paddles

    [TestMethod]
    public void MousePaddles_MovingRightShouldTurnClockwiseAndClamp()
    {
        using var source = new InputSource();
        var paddles = new Paddles { PositionA = 128 };
        using var simulator = new MousePaddles(source, paddles);

        simulator.MoveBy(28);
        Assert.AreEqual(100, (int)paddles.PositionA);   // clockwise: the value decreases
        simulator.MoveBy(-50);
        Assert.AreEqual(150, (int)paddles.PositionA);
        simulator.MoveBy(-1000);
        Assert.AreEqual(255, (int)paddles.PositionA);
        simulator.MoveBy(1000);
        Assert.AreEqual(0, (int)paddles.PositionA);

        simulator.Sensitivity = 0.5;
        simulator.MoveBy(-20);
        Assert.AreEqual(10, (int)paddles.PositionA);
    }

    #endregion

    #region Selector

    [TestMethod]
    public void Selector_ShouldPlugTheRightDevice()
    {
        var board = NewBoard();
        using var source = new InputSource();
        using var selector = new ControlPortSelector(board, source);

        selector.Select(2, new(ControlPortSource.KeypadJoystick));
        selector.Select(1, new(ControlPortSource.MousePaddles));

        Assert.IsInstanceOfType<Joystick>(board.ControlPort2.Device);
        Assert.IsInstanceOfType<Paddles>(board.ControlPort1.Device);
        Assert.IsTrue(selector.UsesKey(Keys.NumPad8));
        Assert.IsFalse(selector.UsesKey(Keys.Up));

        selector.Select(1, ControlPortChoice.None);
        Assert.IsTrue(board.ControlPort1.IsEmpty);
    }

    [TestMethod]
    public void Selector_SameSourceShouldMoveBetweenPorts()
    {
        var board = NewBoard();
        using var source = new InputSource();
        using var selector = new ControlPortSelector(board, source);
        int changes = 0;
        selector.Changed += () => changes++;

        selector.Select(2, new(ControlPortSource.KeypadJoystick));
        selector.Select(1, new(ControlPortSource.KeypadJoystick));

        Assert.AreEqual(ControlPortSource.KeypadJoystick, selector[1].Source);
        Assert.AreEqual(ControlPortSource.None, selector[2].Source);
        Assert.IsTrue(board.ControlPort2.IsEmpty);
        Assert.AreEqual(2, changes);
    }

    [TestMethod]
    public void Selector_SwapPorts()
    {
        var board = NewBoard();
        using var source = new InputSource();
        using var selector = new ControlPortSelector(board, source);
        selector.Select(1, new(ControlPortSource.MousePaddles));
        selector.Select(2, new(ControlPortSource.ArrowsJoystick));

        selector.SwapPorts();

        Assert.AreEqual(ControlPortSource.ArrowsJoystick, selector[1].Source);
        Assert.AreEqual(ControlPortSource.MousePaddles, selector[2].Source);
        Assert.IsInstanceOfType<Joystick>(board.ControlPort1.Device);
        Assert.IsInstanceOfType<Paddles>(board.ControlPort2.Device);
    }

    [TestMethod]
    public void Selector_MenuShouldListChoicesAndCheckTheCurrentOne()
    {
        var board = NewBoard();
        using var source = new InputSource();
        using var selector = new ControlPortSelector(board, source);
        selector.Select(2, new(ControlPortSource.KeypadJoystick));
        using var menu = new ContextMenuStrip();

        selector.AddMenuItems(menu.Items);
        var port2 = (ToolStripMenuItem)menu.Items[1];
        selector.FillPortMenu(port2, 2);

        Assert.AreEqual("Control port 1", menu.Items[0].Text);
        Assert.AreEqual(Keys.Control | Keys.F12, ((ToolStripMenuItem)menu.Items[2]).ShortcutKeys);
        var items = port2.DropDownItems.OfType<ToolStripMenuItem>().ToList();
        Assert.IsTrue(items.Single(item => item.Checked).Text!.StartsWith("Joystick: numeric keypad"));
        Assert.IsTrue(items.Any(item => item.Text!.StartsWith("Paddles: mouse")));
        Assert.IsTrue(items.Any(item => item.Text!.StartsWith("USB joystick")));
    }

    #endregion
}
