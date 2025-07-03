using Fake64;

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

            //board.Address(0xD020, 13);
            //board.Address(0xD021, 14);
            //for (ushort addr = 0x0400; addr < 0x0400 + 0x3E8 ; addr++)
            //{
            //    board.Address(addr, 32);
            //}
            //var i = 0;
            //for (ushort addr = 0x0400; addr < 0x0400 + 0x100; addr++)
            //{
            //    board.Address(addr, (byte) i++);
            //}
            //byte color = 0;
            //for (ushort addr = 0xD800; addr < 0xD800 + 0x3E8; addr++)
            //{
            //    board.Address(addr, color);
            //    color = (byte) ((color + 1) & 0xf);
            //}

            var form = new Fake64Form2();
            form.KeyDown += (s, e) => {

                switch (e.KeyCode)
                {
                    default:
                        break;
                }
            
            };
            form.KeyUp += (s, e) => {

                switch (e.KeyCode)
                {
                    default:
                        break;
                }

            };
            form.Show();

            var refrate = 50;
            var totalRenderedFrames = 0;
            var totalRenderingTime = 0.0;
            var stay = true;
            _ = Task.Factory.StartNew(() => {
                var timer = new PeriodicTimer(TimeSpan.FromMilliseconds((int)Math.Round(1000.0 / refrate, 0)));
                try
                {
                    while (stay)
                    {
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
