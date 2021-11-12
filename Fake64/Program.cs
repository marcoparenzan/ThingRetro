using System;
using System.Drawing;
using System.Windows.Forms;
using static Fake64.StaticBasic;

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

            for (ushort addr = 0x0400; addr < 0x0800; addr++)
                POKE(addr, ASC(' '));

            var form = new DoubleBufferForm();
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

            var timer = new System.Windows.Forms.Timer();
            timer.Interval = (int)Math.Round(1000.0 / refrate, 0);
            timer.Tick += (s, e) =>
            {
                var start = DateTime.Now;

                VicII.Render(form.g);
                form.Invalidate();

                VicII.Update(timer.Interval);

                var stop = DateTime.Now;
                var frameRate = (int)Math.Round(1000.0 / (stop - start).TotalMilliseconds, 0);
            };
            timer.Start();

            Application.Run(form);
        }
    }
}
