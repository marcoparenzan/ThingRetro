using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Thing65.C65;
using Thing65.Mos;

namespace Thing65.WinForms;

// The monitor: a UserControl that shows the frames received from the video output of a Board.
// It does not know the Board: it is connected with a MonitorCable (new MonitorCable(board.VideoOut, monitor)).
// It can go into any form; the picture is scaled keeping its proportions
public class C65Monitor : UserControl, IScreen
{
    // The bitmap always has the size of the video chip's frame: Render writes contiguous rows of
    // "width" pixels, so it must not depend on the size (or the DPI) of the control
    readonly int width, height;
    readonly Bitmap frame;
    readonly object frameLock = new();
    BitmapData? frameData;
    long framesReceived;

    // The monitor of the C65: the frame of the VIC-III (806 x 568 dots)
    public C65Monitor() : this(CSG4567.SCREEN_WIDTH, CSG4567.SCREEN_HEIGHT)
    {
    }

    // A monitor for a frame of another size (for example the VIC-II's 403 x 284 of the C64 board)
    public C65Monitor(int frameWidth, int frameHeight)
    {
        width = frameWidth;
        height = frameHeight;
        frame = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        DoubleBuffered = true;
        ResizeRedraw = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.Selectable, true);
        BackColor = Color.Black;
        TabStop = true;
        Size = width > 500 ? new Size(width, height) : new Size(width * 2, height * 2); // the VIC-III frame at 1:1
    }

    // If false the picture fills the whole control, even if distorted
    [DefaultValue(true)]
    public bool KeepAspectRatio { get; set; } = true;

    // Frames received from the video cable since the control exists
    [Browsable(false)]
    public long FramesReceived => Interlocked.Read(ref framesReceived);

    // Copy of the last frame received, at the original resolution
    public Bitmap CaptureFrame()
    {
        lock (frameLock)
        {
            return new Bitmap(frame);
        }
    }

    // IScreen: called by the emulation thread at the end of a frame; the lock is held between BeginRender and EndRender
    unsafe byte* IScreen.BeginRender()
    {
        Monitor.Enter(frameLock);
        frameData = frame.LockBits(new Rectangle(0, 0, frame.Width, frame.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        return (byte*)frameData.Scan0;
    }

    void IScreen.EndRender()
    {
        frame.UnlockBits(frameData!);
        frameData = null;
        Monitor.Exit(frameLock);
        Interlocked.Increment(ref framesReceived);

        // Invalidate is safe from any thread: the painting then happens on the UI thread
        if (IsHandleCreated && !IsDisposed) Invalidate();
    }

    // Area of the control taken by the picture (with black bars if the proportions differ)
    public Rectangle ImageBounds
    {
        get
        {
            var area = ClientRectangle;
            if (!KeepAspectRatio || area.Width == 0 || area.Height == 0) return area;

            float scale = Math.Min((float)area.Width / this.width, (float)area.Height / this.height);
            int scaledWidth = (int)(this.width * scale);
            int scaledHeight = (int)(this.height * scale);
            return new Rectangle(area.X + (area.Width - scaledWidth) / 2, area.Y + (area.Height - scaledHeight) / 2, scaledWidth, scaledHeight);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        lock (frameLock)
        {
            e.Graphics.DrawImage(frame, ImageBounds);
        }
        base.OnPaint(e);
    }

    // A click gives the focus to the monitor, so the PC keyboard reaches the C64 (see PcKeyboard)
    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        base.OnMouseDown(e);
    }

    // The cursor keys must reach the control instead of moving the focus between the form's controls
    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Left or Keys.Right || base.IsInputKey(keyData);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (frameLock)
            {
                frame.Dispose();
            }
        }
        base.Dispose(disposing);
    }
}
