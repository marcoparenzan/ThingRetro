using Thing65.WinForms;
using static Thing65.Tests.TestHelpers;

namespace Thing65.Tests;

// The C64 keyboard matrix (Thing65.C65) and its mapping to the PC keys (Thing65.WinForms)
[TestClass]
public sealed class KeyboardTests
{
    const long CYCLES_PER_SECOND = 985248;
    const int FRAME_CYCLES = 312 * 63;

    // Matrix rows: a row is selected by setting its bit of CIA1 port A to 0
    static byte Row(Board board, int row) => board.Keyboard.GetKeyboardState((byte)~(1 << row));

    #region Matrix (Thing65.C65)

    [TestMethod]
    [DataRow(C65Key.F1, 0, 4)]
    [DataRow(C65Key.F7, 0, 3)]
    [DataRow(C65Key.A, 1, 2)]
    [DataRow(C65Key.LeftShift, 1, 7)]
    [DataRow(C65Key.Q, 7, 6)]
    [DataRow(C65Key.RunStop, 7, 7)]
    public void PressedKey_ShouldClearItsMatrixBit(C65Key key, int row, int column)
    {
        var board = NewBoard();

        board.Keyboard.SetPressedKeys([key]);

        Assert.AreEqual(0xFF & ~(1 << column), (int)Row(board, row));
        Assert.IsTrue(board.Keyboard.IsPressed(key));
    }

    [TestMethod]
    public void SetPressedKeys_ShouldReplacePreviousState()
    {
        var board = NewBoard();
        board.Keyboard.SetPressedKeys([C65Key.F1, C65Key.LeftShift]);

        board.Keyboard.SetPressedKeys([C65Key.Space]);

        Assert.IsFalse(board.Keyboard.IsPressed(C65Key.F1));
        Assert.IsFalse(board.Keyboard.IsPressed(C65Key.LeftShift));
        Assert.IsTrue(board.Keyboard.IsPressed(C65Key.Space));

        board.Keyboard.ReleaseAll();
        for (int row = 0; row < 8; row++)
            Assert.AreEqual(0xFF, (int)Row(board, row));
    }

    #endregion

    #region PC keyboard (Thing65.WinForms)

    [TestMethod]
    [DataRow(Keys.F1, C65Key.F1, false)]
    [DataRow(Keys.F2, C65Key.F1, true)]
    [DataRow(Keys.F3, C65Key.F3, false)]
    [DataRow(Keys.F4, C65Key.F3, true)]
    [DataRow(Keys.F5, C65Key.F5, false)]
    [DataRow(Keys.F6, C65Key.F5, true)]
    [DataRow(Keys.F7, C65Key.F7, false)]
    [DataRow(Keys.F8, C65Key.F7, true)]
    [DataRow(Keys.Right, C65Key.CursorRight, false)]
    [DataRow(Keys.Left, C65Key.CursorRight, true)]
    [DataRow(Keys.Down, C65Key.CursorDown, false)]
    [DataRow(Keys.Up, C65Key.CursorDown, true)]
    [DataRow(Keys.A, C65Key.A, false)]
    [DataRow(Keys.D7, C65Key.D7, false)]
    [DataRow(Keys.Enter, C65Key.Return, false)]
    public void PcKey_ShouldMapToC65KeyWithShiftWhenNeeded(Keys pcKey, C65Key expected, bool shifted)
    {
        var keys = PcKeyboardLayout.ToC65Keys([pcKey]);

        CollectionAssert.Contains(keys.ToList(), expected);
        Assert.AreEqual(shifted, keys.Contains(C65Key.LeftShift));
        Assert.AreEqual(shifted ? 2 : 1, keys.Count);
    }

    [TestMethod]
    public void PcShiftWithF1_ShouldBeF2()
    {
        var keys = PcKeyboardLayout.ToC65Keys([Keys.ShiftKey, Keys.F1]);

        CollectionAssert.AreEquivalent(new[] { C65Key.F1, C65Key.LeftShift }, keys.ToList());
    }

    [TestMethod]
    public void UnmappedPcKeys_ShouldBeIgnored()
    {
        Assert.IsFalse(PcKeyboardLayout.IsMapped(Keys.Tab));
        Assert.AreEqual(0, PcKeyboardLayout.ToC65Keys([Keys.Tab, Keys.F12]).Count);
    }

    #endregion

    #region PcKeyboard adapter

    // Control that raises keyboard events as if it had the focus
    sealed class KeySource : Control
    {
        public void Down(Keys key) => OnKeyDown(new KeyEventArgs(key));
        public void Up(Keys key) => OnKeyUp(new KeyEventArgs(key));
        public void Blur() => OnLostFocus(EventArgs.Empty);
    }

    [TestMethod]
    public void PcKeyboard_ShouldForwardKeysFromItsControl()
    {
        var board = NewBoard();
        using var source = new KeySource();
        using var cable = new PcKeyboard(source, board.Keyboard);
        long now = 0;
        cable.Now = () => now;

        source.Down(Keys.F2);
        Assert.IsTrue(board.Keyboard.IsPressed(C65Key.F1));
        Assert.IsTrue(board.Keyboard.IsPressed(C65Key.LeftShift));

        now = 100;
        source.Up(Keys.F2);
        Assert.IsFalse(board.Keyboard.IsPressed(C65Key.F1));
        Assert.IsFalse(board.Keyboard.IsPressed(C65Key.LeftShift));
    }

    [TestMethod]
    public void PcKeyboard_ShortTap_ShouldStayPressedForMinimumHold()
    {
        var board = NewBoard();
        using var source = new KeySource();
        using var cable = new PcKeyboard(source, board.Keyboard);
        long now = 1000;
        cable.Now = () => now;

        source.Down(Keys.A);
        source.Up(Keys.A); // released at once, as with SendKeys

        Assert.IsTrue(board.Keyboard.IsPressed(C65Key.A));
        now += (long)PcKeyboard.MinimumHold.TotalMilliseconds - 1;
        cable.ReleaseDueKeys();
        Assert.IsTrue(board.Keyboard.IsPressed(C65Key.A));
        now += 1;
        cable.ReleaseDueKeys();
        Assert.IsFalse(board.Keyboard.IsPressed(C65Key.A));
    }

    [TestMethod]
    public void PcKeyboard_LosingFocus_ShouldReleaseEverything()
    {
        var board = NewBoard();
        using var source = new KeySource();
        using var cable = new PcKeyboard(source, board.Keyboard);

        source.Down(Keys.A);
        source.Down(Keys.ShiftKey);
        source.Blur();

        Assert.IsFalse(board.Keyboard.IsPressed(C65Key.A));
        Assert.IsFalse(board.Keyboard.IsPressed(C65Key.LeftShift));
    }

    #endregion

    // Integration: the KERNAL scans the matrix in the IRQ and puts the PETSCII code into the buffer at $0277
    [TestMethod]
    [DataRow(Keys.F1, 133)]
    [DataRow(Keys.F3, 134)]
    [DataRow(Keys.F5, 135)]
    [DataRow(Keys.F7, 136)]
    [DataRow(Keys.F2, 137)]
    [DataRow(Keys.F4, 138)]
    [DataRow(Keys.F6, 139)]
    [DataRow(Keys.F8, 140)]
    [DataRow(Keys.Down, 17)]
    [DataRow(Keys.Up, 145)]
    [DataRow(Keys.Right, 29)]
    [DataRow(Keys.Left, 157)]
    public void Kernal_ShouldDecodePcKeyToPetscii(Keys pcKey, int petscii)
    {
        var board = NewBoard();
        board.RunCycles(3 * CYCLES_PER_SECOND);

        // CPU in a loop with IRQs enabled: the KERNAL scans the keyboard but nobody empties the buffer
        board.Load(0xC000, 0x4C, 0x00, 0xC0);
        board.Cpu.Status = 0x20;
        board.Poke(0x00C6, 0);

        board.Keyboard.SetPressedKeys(PcKeyboardLayout.ToC65Keys([pcKey]));
        board.RunCycles(3 * FRAME_CYCLES);
        board.Keyboard.ReleaseAll();
        board.RunCycles(3 * FRAME_CYCLES);

        Assert.IsTrue(board.Peek(0x00C6) >= 1, "no key in the buffer");
        Assert.AreEqual(petscii, (int)board.Peek(0x0277));
    }
}
