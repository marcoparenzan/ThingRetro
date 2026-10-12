using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Fake64.WinForms;

// Il monitor: uno UserControl che mostra i frame ricevuti dall'uscita video di una Board.
// Non conosce la Board: si collega con un MonitorCable (new MonitorCable(board.VideoOut, monitor)).
// Si può inserire in qualunque form; l'immagine viene scalata mantenendo le proporzioni
public class C64Monitor : UserControl, IScreen
{
    // La bitmap ha sempre la dimensione del frame del VIC-II: Render scrive righe contigue di
    // SCREEN_WIDTH pixel, quindi non deve dipendere dalla dimensione (o dal DPI) del controllo
    readonly Bitmap frame = new(MOS6569.SCREEN_WIDTH, MOS6569.SCREEN_HEIGHT, PixelFormat.Format32bppArgb);
    readonly object frameLock = new();
    BitmapData? frameData;
    long framesReceived;

    public C64Monitor()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.Selectable, true);
        BackColor = Color.Black;
        TabStop = true;
        Size = new Size(MOS6569.SCREEN_WIDTH * 2, MOS6569.SCREEN_HEIGHT * 2);
    }

    // Se falso l'immagine riempie tutto il controllo anche deformandosi
    [DefaultValue(true)]
    public bool KeepAspectRatio { get; set; } = true;

    // Frame ricevuti dal cavo video da quando il controllo esiste
    [Browsable(false)]
    public long FramesReceived => Interlocked.Read(ref framesReceived);

    // Copia dell'ultimo frame ricevuto, alla risoluzione originale
    public Bitmap CaptureFrame()
    {
        lock (frameLock)
        {
            return new Bitmap(frame);
        }
    }

    // IScreen: chiamati dal thread di emulazione a fine frame; il lock resta preso fra BeginRender ed EndRender
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

        // Invalidate è sicuro da qualunque thread: il disegno avviene poi sul thread della UI
        if (IsHandleCreated && !IsDisposed) Invalidate();
    }

    // Area del controllo occupata dall'immagine (con bande nere se le proporzioni sono diverse)
    public Rectangle ImageBounds
    {
        get
        {
            var area = ClientRectangle;
            if (!KeepAspectRatio || area.Width == 0 || area.Height == 0) return area;

            float scale = Math.Min((float)area.Width / MOS6569.SCREEN_WIDTH, (float)area.Height / MOS6569.SCREEN_HEIGHT);
            int width = (int)(MOS6569.SCREEN_WIDTH * scale);
            int height = (int)(MOS6569.SCREEN_HEIGHT * scale);
            return new Rectangle(area.X + (area.Width - width) / 2, area.Y + (area.Height - height) / 2, width, height);
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

    // Il clic dà il focus al monitor, così la tastiera del PC arriva al C64 (vedi PcKeyboard)
    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        base.OnMouseDown(e);
    }

    // I tasti cursore devono arrivare al controllo invece di spostare il focus fra i controlli della form
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
