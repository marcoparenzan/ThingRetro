using Chips;
using System.Drawing;
using System.Drawing.Imaging;

public class MOS6569
{
    Board board;

    public MOS6569(Board board)
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
    unsafe public void Mode0(Bitmap bitmap, Rectangle clientRectangle)
    {
        var bitmapData = bitmap.LockBits(clientRectangle, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

        byte* scan0 = (byte*)bitmapData.Scan0.ToPointer();

        //var borderColorAddress = (ushort)(board.Address(0xD020) << 2);
        //var foregroundColorAddress = (ushort)(board.Address(0xD021) << 2);
        // access registers directly
        var borderColorAddress = (ushort)(bytes[0x20] << 2);
        var foregroundColorAddress = (ushort)(bytes[0x21] << 2);

        for (var y = 0; y < 42; y++)
        {
            for (var x = 0; x < 403; x++)
            {
                var colorAddress = borderColorAddress;
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
                var colorAddress = borderColorAddress;
                scan0[0] = palette[colorAddress++]; // blueComponent;
                scan0++;
                scan0[0] = palette[colorAddress++]; // greenComponent;
                scan0++;
                scan0[0] = palette[colorAddress++]; // redComponent;
                scan0++;
                scan0[0] = palette[colorAddress++]; // transparency;
                scan0++;
            }
            for (var x = 0; x < 320; x += 8)
            {
                var offset = (y >> 3) * 25 + (x >> 3);
                var ch = board.Address((ushort)(0x400 + offset));
                var chBits = board.Chargen((ushort)((ch << 3) + (y & 0b00000111)));
                var baseColorAddress = (ushort)(board.Address((ushort)(0xD800 + offset)) << 2);
                var mask = 0b10000000;
                while (mask>0)
                {
                    var colorAddress = baseColorAddress;
                    if ((chBits & mask) == 0)
                        colorAddress = foregroundColorAddress;
                    scan0[0] = palette[colorAddress++]; // blueComponent;
                    scan0++;
                    scan0[0] = palette[colorAddress++]; // greenComponent;
                    scan0++;
                    scan0[0] = palette[colorAddress++]; // redComponent;
                    scan0++;
                    scan0[0] = palette[colorAddress++]; // transparency;
                    scan0++;

                    mask >>= 1;
                }
            }
            for (var x = 0; x < 41; x++)
            {
                var colorAddress = borderColorAddress;
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
                var colorAddress = borderColorAddress;
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