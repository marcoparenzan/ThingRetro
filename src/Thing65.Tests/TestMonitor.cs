using Thing65.WinForms;
using static Thing65.Tests.TestHelpers;

namespace Thing65.Tests;

// Video output, cable and monitor
[TestClass]
public sealed class MonitorTests
{
    const int FRAME_CYCLES = 312 * 63;

    // Fake screen: counts the frames and keeps the pixels
    sealed unsafe class RecordingScreen : IScreen
    {
        public readonly uint[] Pixels = new uint[MOS6569.SCREEN_WIDTH * MOS6569.SCREEN_HEIGHT];
        public int Frames;
        System.Runtime.InteropServices.GCHandle handle;

        public byte* BeginRender()
        {
            handle = System.Runtime.InteropServices.GCHandle.Alloc(Pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
            return (byte*)handle.AddrOfPinnedObject();
        }

        public void EndRender()
        {
            handle.Free();
            Frames++;
        }
    }

    [TestMethod]
    public void WithoutCable_ShouldRunWithoutMonitor()
    {
        var board = NewBoard();

        board.RunCycles(3 * FRAME_CYCLES);

        Assert.IsFalse(board.VideoOut.IsConnected);
    }

    [TestMethod]
    public void Cable_ShouldDeliverOneFramePerVideoFrame()
    {
        var board = NewBoard();
        var screen = new RecordingScreen();

        using (new MonitorCable(board.VideoOut, screen))
        {
            Assert.IsTrue(board.VideoOut.IsConnected);
            board.RunCycles(3 * FRAME_CYCLES);
        }

        Assert.AreEqual(3, screen.Frames);
        Assert.AreEqual(0xFF6C5EB5u, screen.Pixels[0]); // light blue border (color 14) after the VIC-II reset
    }

    [TestMethod]
    public void DisposedCable_ShouldStopFrames()
    {
        var board = NewBoard();
        var screen = new RecordingScreen();
        var cable = new MonitorCable(board.VideoOut, screen);
        board.RunCycles(FRAME_CYCLES);

        cable.Dispose();
        board.RunCycles(2 * FRAME_CYCLES);

        Assert.AreEqual(1, screen.Frames);
        Assert.IsFalse(board.VideoOut.IsConnected);
    }

    [TestMethod]
    public void VideoPort_ShouldAcceptOnlyOneMonitor()
    {
        var board = NewBoard();
        using var cable = new MonitorCable(board.VideoOut, new RecordingScreen());

        Assert.ThrowsException<InvalidOperationException>(() => new MonitorCable(board.VideoOut, new RecordingScreen()));
    }

    [TestMethod]
    public void C65Monitor_ShouldShowBootScreen()
    {
        var board = NewBoard();
        using var monitor = new C65Monitor();
        using var cable = new MonitorCable(board.VideoOut, monitor);

        board.RunCycles(3 * 985248);

        Assert.IsTrue(monitor.FramesReceived > 100);
        using var frame = monitor.CaptureFrame();
        Assert.AreEqual(MOS6569.SCREEN_WIDTH, frame.Width);
        Assert.AreEqual(Color.FromArgb(0xFF, 0x6C, 0x5E, 0xB5), frame.GetPixel(0, 0));    // light blue border
        Assert.AreEqual(Color.FromArgb(0xFF, 0x35, 0x28, 0x79), frame.GetPixel(42, 42));  // blue background
    }

    [TestMethod]
    public void C65Monitor_ShouldKeepAspectRatioInsideLargerArea()
    {
        using var monitor = new C65Monitor { Size = new Size(1000, 284) };

        var bounds = monitor.ImageBounds;

        Assert.AreEqual(284, bounds.Height);
        Assert.AreEqual(403, bounds.Width);
        Assert.AreEqual((1000 - 403) / 2, bounds.X);

        monitor.KeepAspectRatio = false;
        Assert.AreEqual(monitor.ClientRectangle, monitor.ImageBounds);
    }
}
