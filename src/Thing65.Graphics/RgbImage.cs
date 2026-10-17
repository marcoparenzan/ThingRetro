namespace Thing65.Graphics;

// A picture as 32-bit pixels 0xAARRGGBB, row after row from the top left
public sealed class RgbImage
{
    public int Width { get; }
    public int Height { get; }
    public uint[] Pixels { get; }

    public RgbImage(int width, int height, uint[]? pixels = null)
    {
        if (width <= 0 || height <= 0) throw new ArgumentException($"An image of {width}x{height} pixels");
        Width = width;
        Height = height;
        Pixels = pixels ?? new uint[width * height];
        if (Pixels.Length != width * height) throw new ArgumentException("The pixels do not match the size");
    }

    public uint this[int x, int y]
    {
        get => Pixels[y * Width + x];
        set => Pixels[y * Width + x] = value;
    }

    public static uint Argb(int alpha, int red, int green, int blue) =>
        (uint)(alpha << 24 | red << 16 | green << 8 | blue);
}
