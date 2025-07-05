using Fake64.Core;
using System.Drawing.Imaging;
using System.Threading.Channels;

namespace Fake64;

public partial class Fake64Form : Form
{
    MOS6569 vicii;
    Bitmap bitmap;
    Task render;

    public Fake64Form(MOS6569 vicii, int w = 403, int h = 284)
    {
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.BackColor = System.Drawing.Color.White;
        this.ClientSize = new System.Drawing.Size(w, h);

        this.DoubleBuffered = true;
        this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);

        //
        //  C64 specific
        //

        this.vicii = vicii;
    }

    unsafe void Render()
    {
        var bitmapData = bitmap.LockBits(this.ClientRectangle, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

        var scan0 = (byte*)bitmapData.Scan0.ToPointer();

        vicii.Render(scan0);

        bitmap.UnlockBits(bitmapData);

        g.DrawImage(bitmap, 0, 0);

        this.Invalidate();
    }

    BufferedGraphics bufferedGraphics;

    bool Suspended { get; set; } = true;

    public Graphics g => bufferedGraphics.Graphics;

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

            double totalRenderingTime = 0;
            var totalRenderedFrames = 0;

            int i = 0;

            this.render = Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    await vicii.WaitForRetraceAsync();
                    while (vicii.TryRead(out var count))
                    {
                        //var start = DateTime.Now;

                        Render();

                        //var stop = DateTime.Now;
                        //var elapsed = (int) (stop - start).TotalMilliseconds;
                        //totalRenderingTime += elapsed;
                        //totalRenderedFrames++;
                        //var msg = $"{totalRenderedFrames}/{Math.Round(totalRenderingTime / totalRenderedFrames, 2)}";
                        //Invoke(() =>
                        //{
                        //    Text = msg;
                        //});
                        Invoke(() =>
                        {
                            Text = $"{i++} {count}";
                        });
                    }
                }
            });
        }
        else
        {
            this.bufferedGraphics.Render(e.Graphics);
        }
    }
}
