using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using ThingRetroEmu.Video;

namespace ThingRetroEmu.WinForms;

/// <summary>
/// The monitor: a UserControl that shows the frames received from the video output of a machine. It does not know the machine: it is
/// connected with a <see cref="MonitorCable"/> (<c>new MonitorCable(machine.VideoOut, monitor)</c>), and it follows whatever picture
/// the video source sends: the size (a VIC-II's 403 x 284, a VIC-III's 806 x 568, an Atari 2600's 160 x 192...) and the shape of the dots
/// can change from one frame to the next. It can go into any form; the picture is scaled keeping its proportions
/// </summary>
public class MonitorControl : UserControl, IMonitor
{
    readonly object frameLock = new();
    Bitmap frame;
    VideoMode mode;
    BitmapData? frameData;
    long framesReceived;

    /// <summary>A monitor that waits for the first frame; <paramref name="initial"/> is what it shows (black) and how big it is until then.</summary>
    public MonitorControl(VideoMode initial)
    {
        mode = initial;
        frame = new Bitmap(initial.Width, initial.Height, PixelFormat.Format32bppArgb);
        DoubleBuffered = true;
        ResizeRedraw = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.Selectable, true);
        BackColor = Color.Black;
        TabStop = true;
        Size = DefaultSizeFor(initial);
    }

    public MonitorControl() : this(new VideoMode(403, 284))
    {
    }

    /// <summary>The size of the control that shows a picture of this mode at a whole multiple (1:1 for the big ones, 2:1 for the small ones).</summary>
    public static Size DefaultSizeFor(VideoMode mode)
    {
        int zoom = mode.Width > 500 ? 1 : 2;
        return new Size((int)(mode.Width * mode.PixelAspect * zoom), mode.Height * zoom);
    }

    /// <summary>If false the picture fills the whole control, even if distorted.</summary>
    [DefaultValue(true)]
    public bool KeepAspectRatio { get; set; } = true;

    /// <summary>Frames received from the video cable since the control exists.</summary>
    [Browsable(false)]
    public long FramesReceived => Interlocked.Read(ref framesReceived);

    /// <summary>The picture the monitor shows now.</summary>
    [Browsable(false)]
    public VideoMode Mode
    {
        get { lock (frameLock) return mode; }
    }

    /// <summary>Raised on the emulation thread when the source changes the picture (its size or the shape of its dots).</summary>
    public event Action<VideoMode>? ModeChanged;

    /// <summary>Copy of the last frame received, at the original resolution.</summary>
    public Bitmap CaptureFrame()
    {
        lock (frameLock)
        {
            return new Bitmap(frame);
        }
    }

    // IMonitor: called by the emulation thread at the end of a frame; the lock is held between BeginFrame and EndFrame
    unsafe byte* IMonitor.BeginFrame(VideoMode newMode)
    {
        Monitor.Enter(frameLock);
        bool changed = newMode != mode;
        if (newMode.Width != frame.Width || newMode.Height != frame.Height)
        {
            var old = frame;
            frame = new Bitmap(newMode.Width, newMode.Height, PixelFormat.Format32bppArgb);
            old.Dispose();
        }
        mode = newMode;
        if (changed) ModeChanged?.Invoke(newMode);
        frameData = frame.LockBits(new Rectangle(0, 0, frame.Width, frame.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        return (byte*)frameData.Scan0;
    }

    void IMonitor.EndFrame()
    {
        frame.UnlockBits(frameData!);
        frameData = null;
        Monitor.Exit(frameLock);
        Interlocked.Increment(ref framesReceived);

        // Invalidate is safe from any thread: the painting then happens on the UI thread
        if (IsHandleCreated && !IsDisposed) Invalidate();
    }

    /// <summary>Area of the control taken by the picture (with black bars if the proportions differ).</summary>
    public Rectangle ImageBounds
    {
        get
        {
            var area = ClientRectangle;
            if (!KeepAspectRatio || area.Width == 0 || area.Height == 0) return area;

            double aspect;
            lock (frameLock) aspect = mode.DisplayAspect;
            double areaAspect = (double)area.Width / area.Height;
            int scaledWidth = areaAspect > aspect ? (int)(area.Height * aspect) : area.Width;
            int scaledHeight = areaAspect > aspect ? area.Height : (int)(area.Width / aspect);
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

    // A click gives the focus to the monitor, so the PC keyboard reaches the machine (see the machine's PcKeyboard)
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
