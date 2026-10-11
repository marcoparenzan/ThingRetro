using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Fake64;

public partial class Fake64Form : Form, IScreen
{
    // La bitmap ha sempre la dimensione del frame del VIC-II: Render scrive righe contigue di
    // SCREEN_WIDTH pixel, quindi non deve dipendere dalla dimensione (o dal DPI) della finestra
    readonly Bitmap bitmap = new(MOS6569.SCREEN_WIDTH, MOS6569.SCREEN_HEIGHT, PixelFormat.Format32bppArgb);
    readonly object bitmapLock = new();
    BitmapData bitmapData;

    public Fake64Form(int scale = 2)
    {
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = System.Drawing.Color.Black;
        this.ClientSize = new System.Drawing.Size(MOS6569.SCREEN_WIDTH * scale, MOS6569.SCREEN_HEIGHT * scale);

        this.DoubleBuffered = true;
        this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
    }

    // Chiamati dal thread di emulazione a fine frame: il lock resta preso fra BeginRender ed EndRender
    unsafe public byte* BeginRender()
    {
        Monitor.Enter(bitmapLock);
        bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        return (byte*)bitmapData.Scan0;
    }

    public void EndRender()
    {
        bitmap.UnlockBits(bitmapData);
        Monitor.Exit(bitmapLock);

        // Invalidate è sicuro da qualunque thread: il disegno avviene poi sul thread della UI
        if (IsHandleCreated) Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        lock (bitmapLock)
        {
            e.Graphics.DrawImage(bitmap, ClientRectangle);
        }
    }
}
