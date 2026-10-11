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

            var form = new Fake64Form();

            var board = new Board(form);

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

            Application.Run(form);
        }
    }
}
