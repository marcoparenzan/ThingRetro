using Fake64;
using Microsoft.VisualBasic.Devices;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Fake64
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            var board = new Board();
            //var board = new Board(kernalName: "kernal-901227-02.bin", chargenName: "chargen-901225-01.bin", basicName: "basic-901226-01.bin");

            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var form = new Fake64Form(board.VICII);

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
