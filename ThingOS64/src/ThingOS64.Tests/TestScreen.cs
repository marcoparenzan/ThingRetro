using ThingCommodore.Graphics;
using static ThingOS64.Tests.OsBoard;

namespace ThingOS64.Tests;

[TestClass]
public sealed class TestScreen
{
    public TestContext TestContext { get; set; } = null!;

    // The desktop as the monitor shows it; the picture is saved next to the tests (thingos-desktop.png)
    [TestMethod]
    public void Desktop_ShouldShowTheMenuBarTheDesktopAndThePointer()
    {
        using var monitor = new CaptureMonitor();
        var board = New();
        board.VideoOut.Plug(monitor);
        board.RunCycles(CYCLES_PER_SECOND / 2);

        var frame = monitor.Frame ?? throw new AssertFailedException("no frame");
        string file = Path.Combine(AppContext.BaseDirectory, "thingos-desktop.png");
        File.WriteAllBytes(file, Png.Write(frame));
        TestContext.AddResultFile(file);

        // The pointer is at (160, 100): its tip is black, the pixel inside it white, both over the desktop
        var (left, top) = ScreenOrigin(frame);
        uint desktop = frame[left + 8, top + 150];
        uint menuBar = frame[left + 300, top + 2];
        Assert.AreNotEqual(desktop, menuBar, "the menu bar is not the desktop color");
        Assert.AreNotEqual(desktop, frame[left + 160, top + 100], "the tip of the pointer");
        Assert.AreNotEqual(desktop, frame[left + 161, top + 102], "the inside of the pointer");
        Assert.AreNotEqual(frame[left + 160, top + 100], frame[left + 161, top + 102], "outline and fill differ");
    }

    // The top left pixel of the 320x200 screen inside the frame with its border: the first pixel that is
    // not the border color on the row through the middle of the menu bar is the left edge
    static (int Left, int Top) ScreenOrigin(RgbImage frame)
    {
        uint border = frame[0, 0];
        int top = Enumerable.Range(0, frame.Height).First(y => frame[frame.Width / 2, y] != border);
        int left = Enumerable.Range(0, frame.Width).First(x => frame[x, top + 3] != border);
        return (left, top);
    }
}
