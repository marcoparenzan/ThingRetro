using Fake64;
using System.Drawing;
using System.Drawing.Imaging;

namespace Fake64
{
    static class Program
    {
        static void Main()
        {
            var board = new Board();

            board.Address(0xD020, 13);
            board.Address(0xD021, 14);
            for (ushort addr = 0x0400; addr < 0x0400 + 0x3E8; addr++)
            {
                board.Address(addr, 32);
            }
            var i = 0;
            for (ushort addr = 0x0400; addr < 0x0400 + 0x100; addr++)
            {
                board.Address(addr, (byte)i++);
            }
            byte color = 0;
            for (ushort addr = 0xD800; addr < 0xD800 + 0x3E8; addr++)
            {
                board.Address(addr, color);
                color = (byte)((color + 1) & 0xf);
            }

            var bitmap = new Bitmap(403, 284, PixelFormat.Format32bppArgb);
            var cr = new Rectangle(0, 0, bitmap.Width, bitmap.Height);

            var refrate = 50;
            var totalRenderedFrames = 0;
            var totalRenderingTime = 0.0;
            var stay = true;
            var t = Task.Factory.StartNew(() =>
            {
                var timer = new PeriodicTimer(TimeSpan.FromMilliseconds((int)Math.Round(1000.0 / refrate, 0)));
                try
                {
                    while (stay)
                    {
                        var start = DateTime.Now;

                        board.Raster(bitmap, cr);

                        // do something

                        var stop = DateTime.Now;
                        var elapsed = (stop - start).TotalMilliseconds;
                        totalRenderingTime += elapsed;
                        totalRenderedFrames++;
                        var frameRate = (int)Math.Round(1000.0 / elapsed, 0);
                        var msg = $"{totalRenderedFrames}/{Math.Round(totalRenderingTime / totalRenderedFrames, 2)}";
                        //Console.WriteLine(msg);
                    }
                }
                catch (Exception ex)
                {
                    // handling disposing
                }
            });

            Task.WaitAll(t);
        }
    }
}
