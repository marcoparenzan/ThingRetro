using System.Drawing.Imaging;

namespace Fake64;

public partial class DoubleBufferControl : UserControl
{
    public DoubleBufferControl()
    {
        Init();
    }

    private void Init()
    {
        this.DoubleBuffered = true;
        this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        this.ClientSize = new System.Drawing.Size(403, 284);
        Cursor.Hide();
    }

    BufferedGraphics bufferedGraphics;

    bool Suspended { get;  set; } = true;

    public Graphics g => bufferedGraphics.Graphics;

    Bitmap bitmap;

    protected override void OnResize(EventArgs e)
    {
        if (this.bufferedGraphics != null)
        {
            this.Suspended = true;
            this.bufferedGraphics.Dispose();
            this.bufferedGraphics = null;
            this.Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (this.bufferedGraphics == null)
        {
            this.bufferedGraphics = BufferedGraphicsManager.Current.Allocate(e.Graphics,
                this.ClientRectangle
            );

            this.bitmap = new Bitmap(this.ClientSize.Width, this.ClientSize.Height, PixelFormat.Format32bppArgb);

            this.Suspended = false;
        }
        else
        {
            this.bufferedGraphics.Render(e.Graphics);
        }
    }

    unsafe internal void Render(Action<Bitmap, Rectangle> ab)
    {
        ab(bitmap, this.ClientRectangle);

        g.DrawImage(bitmap, 0, 0);

        this.Invalidate();
    }
}
