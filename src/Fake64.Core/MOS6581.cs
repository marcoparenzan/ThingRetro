using Fake64;
using System.Drawing;
using System.Drawing.Imaging;

public class MOS6581
{
    Board board;

    public MOS6581(Board board)
    {
        this.board = board;
        Reset();
    }

    public void Reset()
    {
    }

    public void Clock()
    {
    }

    byte[] bytes = new byte[0x0400];

    public byte Address(ushort addr)
    {
        return bytes[addr];
    }

    public void Address(ushort addr, byte value)
    {
        bytes[addr] = value;
    }

    // https://lospec.com/palette-list/commodore64
    unsafe public void Raster(Bitmap bitmap, Rectangle clientRectangle)
    {
        var bitmapData = bitmap.LockBits(clientRectangle, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

        byte* scan0 = (byte*)bitmapData.Scan0.ToPointer();

        for (var y = 0; y < 42; y++)
        {
            for (var x = 0; x < 403; x++)
            {
                var colorAddress = (ushort)(board.Address(0xD020) << 2);
                scan0[0] = p(colorAddress++); // blueComponent;
                scan0++;
                scan0[0] = p(colorAddress++); // greenComponent;
                scan0++;
                scan0[0] = p(colorAddress++); // redComponent;
                scan0++;
                scan0[0] = p(colorAddress++); // transparency;
                scan0++;
            }
        }
        for (var y = 0; y < 200; y++)
        {
            for (var x = 0; x < 42; x++)
            {
                var colorAddress = (ushort)(board.Address(0xD020) << 2);
                scan0[0] = p(colorAddress++); // blueComponent;
                scan0++;
                scan0[0] = p(colorAddress++); // greenComponent;
                scan0++;
                scan0[0] = p(colorAddress++); // redComponent;
                scan0++;
                scan0[0] = p(colorAddress++); // transparency;
                scan0++;
            }
            for (var x = 0; x < 320; x += 8)
            {
                var ch = board.Address((ushort)(0x400 + (y >> 3) * 25 + (x >> 3)));
                var chBits = board.Chargen((ushort)((ch << 3) + (y & 0b00000111)));

                var mask = 0b10000000;
                for (var pp = 0; pp < 8; pp++)
                {
                    var colorAddress = (ushort)(board.Address(0xD021) << 2);
                    if ((chBits & mask) > 0) colorAddress = (ushort)(board.Address((ushort)(0xD800 + (y >> 3) * 25 + (x >> 3))) << 2);
                    mask >>= 1;

                    scan0[0] = p(colorAddress++); // blueComponent;
                    scan0++;
                    scan0[0] = p(colorAddress++); // greenComponent;
                    scan0++;
                    scan0[0] = p(colorAddress++); // redComponent;
                    scan0++;
                    scan0[0] = p(colorAddress++); // transparency;
                    scan0++;
                }
            }
            for (var x = 0; x < 41; x++)
            {
                var colorAddress = (ushort)(board.Address(0xD020) << 2);
                scan0[0] = p(colorAddress++); // blueComponent;
                scan0++;
                scan0[0] = p(colorAddress++); // greenComponent;
                scan0++;
                scan0[0] = p(colorAddress++); // redComponent;
                scan0++;
                scan0[0] = p(colorAddress++); // transparency;
                scan0++;
            }
        }
        for (var y = 0; y < 42; y++)
        {
            for (var x = 0; x < 403; x++)
            {
                var colorAddress = (ushort)(board.Address(0xD020) << 2);
                scan0[0] = p(colorAddress++); // blueComponent;
                scan0++;
                scan0[0] = p(colorAddress++); // greenComponent;
                scan0++;
                scan0[0] = p(colorAddress++); // redComponent;
                scan0++;
                scan0[0] = p(colorAddress++); // transparency;
                scan0++;
            }
        }

        bitmap.UnlockBits(bitmapData);

        byte p(ushort address) {
            if (address >= palette.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(address), "Address is out of bounds for the palette.");
            }
            var value = palette[address];
            return value;
        }
    }

    static byte[] palette = new[] {
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