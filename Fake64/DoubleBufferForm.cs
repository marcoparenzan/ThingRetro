using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Fake64
{
    public partial class DoubleBufferForm : Form
    {
        public DoubleBufferForm()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(403, 284);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            Cursor.Hide();
        }

        BufferedGraphics bufferedGraphics;

        bool Suspended { get;  set; } = true;

        public Graphics g => bufferedGraphics.Graphics;

        Bitmap bitmap;

        protected override void OnResizeEnd(EventArgs e)
        {
            base.OnResizeEnd(e);

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

        // https://lospec.com/palette-list/commodore64
        unsafe internal void Render()
        {
            var bitmapData = bitmap.LockBits(this.ClientRectangle, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            byte* scan0 = (byte*)bitmapData.Scan0.ToPointer();

            for (var y = 0; y < 42; y++)
            {
                for (var x = 0; x < 403; x++)
                {
                    var colorAddress = (Board.Ram(0xD020) << 2);
                    scan0[0] = palette[colorAddress++]; // blueComponent;
                    scan0++;
                    scan0[0] = palette[colorAddress++]; // greenComponent;
                    scan0++;
                    scan0[0] = palette[colorAddress++]; // redComponent;
                    scan0++;
                    scan0[0] = palette[colorAddress++]; // transparency;
                    scan0++;
                }
            }
            for (var y = 0; y < 200; y++)
            {
                for (var x = 0; x < 42; x++)
                {
                    var colorAddress = (Board.Ram(0xD020) << 2);
                    scan0[0] = palette[colorAddress++]; // blueComponent;
                    scan0++;
                    scan0[0] = palette[colorAddress++]; // greenComponent;
                    scan0++;
                    scan0[0] = palette[colorAddress++]; // redComponent;
                    scan0++;
                    scan0[0] = palette[colorAddress++]; // transparency;
                    scan0++;
                }
                for (var x = 0; x < 320; x +=8)
                {
                    var ch = Board.Ram(0x400 + (y >> 3) * 25 + (x >> 3));
                    var chBits = Chips.chargen[(ch << 3) + (y & 0b00000111)];

                    var mask = 0b10000000;
                    for (var p = 0; p < 8; p++)
                    {
                        var colorAddress = (Board.Ram(0xD021) << 2);
                        if ((chBits & mask)>0) colorAddress = (Board.Ram(0xD800 + (y >> 3) * 25 + (x >> 3)) << 2);
                        mask >>= 1;

                        scan0[0] = palette[colorAddress++]; // blueComponent;
                        scan0++;
                        scan0[0] = palette[colorAddress++]; // greenComponent;
                        scan0++;
                        scan0[0] = palette[colorAddress++]; // redComponent;
                        scan0++;
                        scan0[0] = palette[colorAddress++]; // transparency;
                        scan0++;
                    }
                }
                for (var x = 0; x < 41; x++)
                {
                    var colorAddress = (Board.Ram(0xD020) << 2);
                    scan0[0] = palette[colorAddress++]; // blueComponent;
                    scan0++;
                    scan0[0] = palette[colorAddress++]; // greenComponent;
                    scan0++;
                    scan0[0] = palette[colorAddress++]; // redComponent;
                    scan0++;
                    scan0[0] = palette[colorAddress++]; // transparency;
                    scan0++;
                }
            }
            for (var y = 0; y < 42; y++)
            {
                for (var x = 0; x < 403; x++)
                {
                    var colorAddress = (Board.Ram(0xD020) << 2);
                    scan0[0] = palette[colorAddress++]; // blueComponent;
                    scan0++;
                    scan0[0] = palette[colorAddress++]; // greenComponent;
                    scan0++;
                    scan0[0] = palette[colorAddress++]; // redComponent;
                    scan0++;
                    scan0[0] = palette[colorAddress++]; // transparency;
                    scan0++;
                }
            }

            bitmap.UnlockBits(bitmapData);

            g.DrawImage(bitmap, 0, 0);

            this.Invalidate();
        }

        byte[] palette = new[] {
            (byte) 0x00, (byte) 0x00, (byte) 0x00, (byte) 0xFF,
            (byte) 0x62, (byte) 0x62, (byte) 0x62, (byte) 0xFF,
            (byte) 0x89, (byte) 0x89, (byte) 0x89, (byte) 0xFF,
            (byte) 0xad, (byte) 0xad, (byte) 0xad, (byte) 0xFF,
            (byte) 0xff, (byte) 0xff, (byte) 0xff, (byte) 0xFF,
            (byte) 0x44, (byte) 0x4e, (byte) 0x9f, (byte) 0xFF,
            (byte) 0x75, (byte) 0x7e, (byte) 0xcb, (byte) 0xFF,
            (byte) 0x12, (byte) 0x54, (byte) 0x6d, (byte) 0xFF,
            (byte) 0x3c, (byte) 0x68, (byte) 0xa1, (byte) 0xFF,
            (byte) 0x87, (byte) 0xd4, (byte) 0xc9, (byte) 0xFF,
            (byte) 0x9b, (byte) 0xe2, (byte) 0x9a, (byte) 0xFF,
            (byte) 0x5e, (byte) 0xab, (byte) 0x5c, (byte) 0xFF,
            (byte) 0xc6, (byte) 0xbf, (byte) 0x6a, (byte) 0xFF,
            (byte) 0xcb, (byte) 0x7e, (byte) 0x88, (byte) 0xFF,
            (byte) 0x9b, (byte) 0x45, (byte) 0x50, (byte) 0xFF,
            (byte) 0xa3, (byte) 0x57, (byte) 0xa0, (byte) 0xFF
        };
    }
}
