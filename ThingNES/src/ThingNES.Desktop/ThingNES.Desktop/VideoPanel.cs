using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ThingNES.Desktop;

internal sealed class VideoPanel : Control
{
    readonly Bitmap bitmap = new(256, 240, PixelFormat.Format32bppArgb);
    readonly int[] argb = new int[256 * 240];
    bool hasFrame;
    public VideoPanel() { DoubleBuffered = true; BackColor = Color.Black; Dock = DockStyle.Fill; }
    public void Present(ReadOnlySpan<byte> pixels)
    {
        FrameImage.CopyArgb(pixels, argb);
        var data = bitmap.LockBits(new Rectangle(0, 0, 256, 240), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try { for (int row = 0; row < 240; row++) Marshal.Copy(argb, row * 256, data.Scan0 + row * data.Stride, 256); }
        finally { bitmap.UnlockBits(data); }
        hasFrame = true; Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!hasFrame) { TextRenderer.DrawText(e.Graphics, "Open an iNES mapper-0/2/3 ROM to begin", Font, ClientRectangle, Color.Silver, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter); return; }
        double scale = Math.Min(ClientSize.Width / 256.0, ClientSize.Height / 240.0);
        if (scale <= 0) return;
        if (scale >= 1) scale = Math.Floor(scale);
        int w = (int)(256 * scale), h = (int)(240 * scale);
        e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        e.Graphics.DrawImage(bitmap, new Rectangle((Width - w) / 2, (Height - h) / 2, w, h), 0, 0, 256, 240, GraphicsUnit.Pixel);
    }
    protected override void Dispose(bool disposing) { if (disposing) bitmap.Dispose(); base.Dispose(disposing); }
}
