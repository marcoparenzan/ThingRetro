using Fake64;
using Microsoft.VisualBasic.Devices;

namespace Fake64
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var board = new Board();
            //var board = new Board(kernalName: "kernal-901227-02.bin", chargenName: "chargen-901225-01.bin", basicName: "basic-901226-01.bin");

            //board.Address(0xD020, 14);
            //board.Address(0xD021, 6);
            //for (ushort addr = 0x0400; addr < 0x0400 + 0x3E8; addr++)
            //{
            //    board.Address(addr, 32);
            //}
            //var i = 0;
            //for (ushort addr = 0x0400; addr < 0x0400 + 0x100; addr++)
            //{
            //    board.Address(addr, (byte)i++);
            //}
            //byte color = 0;
            //for (ushort addr = 0xD800; addr < 0xD800 + 0x3E8; addr++)
            //{
            //    board.Address(addr, color);
            //    color = (byte)((color + 1) & 0xf);
            //}

            var form = new Fake64Form();
            form.KeyDown += (s, e) => {

                board.PressKey((char) e.KeyValue);

            };
            form.KeyUp += (s, e) => {
                board.ReleaseKey((char)e.KeyValue);
            };

            form.Show();

            var refrate = 50;
            var totalRenderedFrames = 0;
            var totalRenderingTime = 0.0;
            var stay = true;
            _ = Task.Factory.StartNew(async () => {
                var timer = new PeriodicTimer(TimeSpan.FromMilliseconds((int)Math.Round(1000.0 / refrate, 0)));
                try
                {
                    while (stay)
                    {
                        await timer.WaitForNextTickAsync();
                        form.Invoke(() =>
                        {
                            var start = DateTime.Now;
                            form.Render((bitmap, cr) => {

                                board.Invalidate(bitmap, cr);

                            });

                            var stop = DateTime.Now;
                            var elapsed = (stop - start).TotalMilliseconds;
                            totalRenderingTime += elapsed;
                            totalRenderedFrames++;
                            var frameRate = (int)Math.Round(1000.0 / elapsed, 0);
                            var msg = $"{totalRenderedFrames}/{Math.Round(totalRenderingTime / totalRenderedFrames, 2)}";
                            form.Text = msg;
                        });
                    }
                }
                catch(Exception ex)
                {
                    // handling disposing
                }   
            });

            Application.Run(form);
        }
    }
}
