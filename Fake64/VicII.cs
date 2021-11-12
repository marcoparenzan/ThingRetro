using System;
using System.Collections.Generic;
using System.Drawing;

namespace Fake64;
 
internal static class VicII
{
    static Bitmap bitmap;

    internal static void Render(Graphics g)
    {
        g.Clear(Color.Yellow);
        g.DrawImage(bitmap, 0, 0);
    }

    static int raster_line = 20;
    static int raster_column = 40;

    internal static void Raster(byte b, Color c)
    {
        // draw 8 pixel horizontal
        byte mask = 0b1000000;
        if ((b & (mask >> 1)) != 0) bitmap.SetPixel(raster_line, raster_column++, c);
        if ((b & (mask >> 1)) != 0) bitmap.SetPixel(raster_line, raster_column++, c);
        if ((b & (mask >> 1)) != 0) bitmap.SetPixel(raster_line, raster_column++, c);
        if ((b & (mask >> 1)) != 0) bitmap.SetPixel(raster_line, raster_column++, c);
        if ((b & (mask >> 1)) != 0) bitmap.SetPixel(raster_line, raster_column++, c);
        if ((b & (mask >> 1)) != 0) bitmap.SetPixel(raster_line, raster_column++, c);
        if ((b & (mask >> 1)) != 0) bitmap.SetPixel(raster_line, raster_column++, c);
        if ((b & (mask >> 1)) != 0) bitmap.SetPixel(raster_line, raster_column++, c);
    }

    internal static void Update(int ms)
    {
        for (ushort addr = 0x0400; addr < 0x0800; addr++)
        {
            ushort addr1 = Board.chargen_base;
            addr1 += (ushort)(Chips.ram[addr >> (ushort)13][addr & (ushort)0x1fff] /* char */ << (ushort)3 /* 8 bytes char def */);

            byte color = 0; // colour ram
            Color c = Color.Black;

            // unroll
            Raster(Board.Address(addr1++), c);
            Raster(Board.Address(addr1++), c);
            Raster(Board.Address(addr1++), c);
            Raster(Board.Address(addr1++), c);
            Raster(Board.Address(addr1++), c);
            Raster(Board.Address(addr1++), c);
            Raster(Board.Address(addr1++), c);
            Raster(Board.Address(addr1++), c);
        }
    }
}
