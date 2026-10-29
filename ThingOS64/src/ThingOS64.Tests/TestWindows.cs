using ThingCommodore.Graphics;
using static ThingOS64.Tests.OsBoard;

namespace ThingOS64.Tests;

// The window manager driven as a person would: a 1351 mouse in port 1 moves the pointer over the cells and
// clicks; the screen, the colors and the ownership map tell what happened
[TestClass]
public sealed class TestWindows
{
    const int BLUE = 6, DARK_GREY = 11;
    const int FILE_MENU = 12, THINGOS_MENU = 3;

    static (Board Board, Mouse1351 Mouse) Desktop()
    {
        var board = New();
        var mouse = new Mouse1351();
        board.ControlPort1.Plug(mouse);
        board.RunCycles(CYCLES_PER_SECOND / 2);
        return (board, mouse);
    }

    // The pointer to the middle of a cell, at most 25 mickeys per frame (the 1351 reports 31 at most)
    static void MoveTo(Board board, Mouse1351 mouse, int column, int row)
    {
        int x = column * 8 + 4, y = row * 8 + 4;
        for (int frame = 0; frame < 200; frame++)
        {
            int dx = x - board.PeekWord(Symbol("pointer_x")), dy = y - board.PeekWord(Symbol("pointer_y"));
            if (dx == 0 && dy == 0) { board.RunFrames(2); return; }
            mouse.Move(Math.Clamp(dx, -25, 25), -Math.Clamp(dy, -25, 25));
            board.RunFrames(1);
        }
        Assert.Fail($"the pointer did not reach {column},{row}");
    }

    static void Click(Board board, Mouse1351 mouse, int column, int row)
    {
        MoveTo(board, mouse, column, row);
        mouse.LeftButton = true;
        board.RunFrames(3);
        mouse.LeftButton = false;
        board.RunFrames(15);    // a whole redraw takes a few frames
    }

    static void Drag(Board board, Mouse1351 mouse, (int Column, int Row) from, (int Column, int Row) to)
    {
        MoveTo(board, mouse, from.Column, from.Row);
        mouse.LeftButton = true;
        board.RunFrames(3);
        MoveTo(board, mouse, to.Column, to.Row);
        board.RunFrames(15);
        mouse.LeftButton = false;
        board.RunFrames(5);
    }

    // Press on the menu title, slide down to the item, release
    static void Choose(Board board, Mouse1351 mouse, int menuColumn, int item)
    {
        MoveTo(board, mouse, menuColumn, 0);
        mouse.LeftButton = true;
        board.RunFrames(3);
        MoveTo(board, mouse, menuColumn, 1 + item);
        board.RunFrames(3);
        mouse.LeftButton = false;
        board.RunFrames(20);
    }

    static int Owner(Board board, int column, int row) => board.Peek(Symbol("MAP") + row * 40 + column);

    static int Windows(Board board) => board.Peek(Symbol("z_count"));

    [TestMethod]
    public void MenuFileNewWindow_ShouldOpenAWindowOnTop()
    {
        var (board, mouse) = Desktop();

        Choose(board, mouse, FILE_MENU, 0);

        Assert.AreEqual(1, Windows(board));
        Assert.AreEqual(0x8C, board.ScreenCode(2, 3), "close box");
        Assert.AreEqual("Window", board.ScreenRow(3).Substring(4, 6));
        Assert.AreEqual("Click or type here", board.ScreenRow(4).Substring(3, 18));
        Assert.AreEqual(BLUE, board.ColorAt(4, 3), "the title bar of the window on top");
        Assert.AreEqual(0x04, board.ScreenCode(2, 5), "left frame");
        Assert.AreEqual(0x06, board.ScreenCode(23, 10), "bottom right corner");
        Assert.AreEqual(1, Owner(board, 10, 6));
        Assert.AreEqual(0, Owner(board, 30, 6));
        Assert.AreEqual(0xA0, board.ScreenCode(30, 6), "the desktop is still around it");
    }

    [TestMethod]
    public void Menu_ShouldShowItsItemsWhileTheButtonIsHeld()
    {
        var (board, mouse) = Desktop();

        MoveTo(board, mouse, FILE_MENU, 0);
        mouse.LeftButton = true;
        board.RunFrames(5);

        StringAssert.Contains(board.ScreenRow(1), "New window");
        StringAssert.Contains(board.ScreenRow(2), "Close window");

        MoveTo(board, mouse, 30, 10);      // released away from the items: nothing happens
        mouse.LeftButton = false;
        board.RunFrames(20);
        Assert.AreEqual(0, Windows(board));
        Assert.AreEqual(0xA0, board.ScreenCode(FILE_MENU, 1), "the drop down is gone");
    }

    [TestMethod]
    public void ClickOnAWindowBelow_ShouldBringItToTheFront()
    {
        var (board, mouse) = Desktop();
        Choose(board, mouse, FILE_MENU, 0);     // at 2,3
        Choose(board, mouse, FILE_MENU, 0);     // at 5,5, over the first one

        Assert.AreEqual(2, Owner(board, 8, 7), "the second covers the first");
        Assert.AreEqual(DARK_GREY, board.ColorAt(10, 3));
        Assert.AreEqual(BLUE, board.ColorAt(10, 5));

        Click(board, mouse, 12, 3);             // the title bar of the first

        Assert.AreEqual(1, Owner(board, 8, 7), "now the first covers the second");
        Assert.AreEqual(BLUE, board.ColorAt(10, 3));
        Assert.AreEqual(DARK_GREY, board.ColorAt(24, 5), "what is left of the second's title bar");
        Assert.AreEqual(2, Windows(board));
    }

    [TestMethod]
    public void DragOnTheTitleBar_ShouldMoveTheWindow()
    {
        var (board, mouse) = Desktop();
        Choose(board, mouse, FILE_MENU, 0);     // at 2,3

        Drag(board, mouse, (10, 3), (15, 8));   // grabbed 8 cells to the right of its corner

        Assert.AreEqual("Window", board.ScreenRow(8).Substring(9, 6));
        Assert.AreEqual(1, Owner(board, 7, 8));
        Assert.AreEqual(0, Owner(board, 2, 3));
        Assert.AreEqual(0xA0, board.ScreenCode(2, 3), "the desktop where the window was");
    }

    [TestMethod]
    public void DragPastTheEdge_ShouldKeepTheWindowOnTheScreen()
    {
        var (board, mouse) = Desktop();
        Choose(board, mouse, FILE_MENU, 0);     // 22 x 8 at 2,3

        Drag(board, mouse, (10, 3), (39, 24));

        Assert.AreEqual(1, Owner(board, 18, 17), "top left corner at 18,17");
        Assert.AreEqual(1, Owner(board, 39, 24), "bottom right corner at 39,24");
        Assert.AreEqual(0, Owner(board, 17, 17));
    }

    [TestMethod]
    public void CloseBox_ShouldCloseTheWindow()
    {
        var (board, mouse) = Desktop();
        Choose(board, mouse, FILE_MENU, 0);

        Click(board, mouse, 2, 3);

        Assert.AreEqual(0, Windows(board));
        Assert.AreEqual(0xA0, board.ScreenCode(10, 5));
        Assert.AreEqual(0, Owner(board, 10, 5));
    }

    [TestMethod]
    public void ClickOnTheContent_ShouldReachTheWindowAtItsOwnCell()
    {
        var (board, mouse) = Desktop();
        Choose(board, mouse, FILE_MENU, 0);     // content from 3,4

        Click(board, mouse, 10, 6);

        Assert.AreEqual('*', (char)board.ScreenCode(10, 6));
    }

    [TestMethod]
    public void Keys_ShouldGoToTheWindowOnTop()
    {
        var (board, mouse) = Desktop();
        Choose(board, mouse, FILE_MENU, 0);

        board.Press(C64Key.H);
        board.Press(C64Key.I);

        Assert.AreEqual("hi", board.ScreenRow(6).Substring(3, 2));
    }

    [TestMethod]
    public void AWindowBelow_ShouldShowOnlyWhereTheOneAboveDoesNotCoverIt()
    {
        var (board, mouse) = Desktop();
        Choose(board, mouse, FILE_MENU, 0);     // 2,3 - 23,10
        Choose(board, mouse, FILE_MENU, 0);     // 5,5 - 26,12

        Assert.AreEqual(0x04, board.ScreenCode(23, 4), "the first one's right frame, above the second");
        Assert.AreEqual(' ', (char)board.ScreenCode(23, 7), "the second one's (empty) content covers it below");
        Assert.AreEqual('e', (char)board.ScreenCode(23, 6), "the end of the second one's text");
        Assert.AreEqual(0x04, board.ScreenCode(2, 8), "the first one's left frame, left of the second");
        Assert.AreEqual(0x04, board.ScreenCode(5, 8), "the second one's left frame");

        board.Press(C64Key.X);                  // the key goes to the second, on top
        Assert.AreEqual('x', (char)board.ScreenCode(6, 8));
        Assert.AreEqual(' ', (char)board.ScreenCode(3, 6), "not to the first");
    }

    [TestMethod]
    public void MenuAbout_ShouldOpenOneAboutWindowAndCloseItFromTheFileMenu()
    {
        var (board, mouse) = Desktop();

        Choose(board, mouse, THINGOS_MENU, 0);
        Choose(board, mouse, THINGOS_MENU, 0);  // already open: brought to the front, not opened again

        Assert.AreEqual(1, Windows(board));
        Assert.AreEqual("About", board.ScreenRow(6).Substring(9, 5));
        Assert.AreEqual("ThingOS 0.2", board.ScreenRow(8).Substring(10, 11));

        Choose(board, mouse, FILE_MENU, 1);     // Close window

        Assert.AreEqual(0, Windows(board));
        Assert.AreEqual(0xFF, board.Peek(Symbol("about_slot")), "the About window knows it was closed");
    }

    // Three windows as the monitor shows them, saved next to the tests (thingos-windows.png)
    [TestMethod]
    public void Screenshot_ShouldShowOverlappingWindows()
    {
        using var monitor = new CaptureMonitor();
        var (board, mouse) = Desktop();
        board.VideoOut.Plug(monitor);
        Choose(board, mouse, FILE_MENU, 0);
        Choose(board, mouse, FILE_MENU, 0);
        Click(board, mouse, 12, 9);
        board.Press(C64Key.O);
        board.Press(C64Key.K);
        Choose(board, mouse, THINGOS_MENU, 0);
        MoveTo(board, mouse, 30, 16);

        var frame = monitor.Frame ?? throw new AssertFailedException("no frame");
        File.WriteAllBytes(Path.Combine(AppContext.BaseDirectory, "thingos-windows.png"), Png.Write(frame));
        Assert.AreEqual(3, Windows(board));
    }
}
