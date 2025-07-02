using System.Drawing.Imaging;
using System.Drawing;

namespace Fake64;

public partial class Fake64Form2 : Form
{
    public Fake64Form2(int w = 403, int h = 284)
    {
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.BackColor = System.Drawing.Color.White;
        this.ClientSize = new System.Drawing.Size(w, h);

        this.DoubleBuffered = true;
        this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        //Cursor.Hide();
    }

    BufferedGraphics bufferedGraphics;

    bool Suspended { get; set; } = true;

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
        if (bitmap is null) return;

        ab(bitmap, this.ClientRectangle);

        g.DrawImage(bitmap, 0, 0);

        this.Invalidate();
    }
}
