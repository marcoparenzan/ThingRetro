using System.Text;

namespace ThingNES;

public static class FrameImage
{
    // Fixed approximate NTSC RGB palette; emphasis/composite decoding are not modeled.
    static readonly uint[] Palette = [
        0x666666,0x002a88,0x1412a7,0x3b00a4,0x5c007e,0x6e0040,0x6c0600,0x561d00,0x333500,0x0b4800,0x005200,0x004f08,0x00404d,0x000000,0x000000,0x000000,
        0xadadad,0x155fd9,0x4240ff,0x7527fe,0xa01acc,0xb71e7b,0xb53120,0x994e00,0x6b6d00,0x388700,0x0c9300,0x008f32,0x007c8d,0x000000,0x000000,0x000000,
        0xfffeff,0x64b0ff,0x9290ff,0xc676ff,0xf36aff,0xfe6ecc,0xfe8170,0xea9e22,0xbcbe00,0x88d800,0x5ce430,0x45e082,0x48cdde,0x4f4f4f,0x000000,0x000000,
        0xfffeff,0xc0dfff,0xd3d2ff,0xe8c8ff,0xfbc2ff,0xfec4ea,0xfeccc5,0xf7d8a5,0xe4e594,0xcfef96,0xbdf4ab,0xb3f3cc,0xb5ebf2,0xb8b8b8,0x000000,0x000000];

    public static void CopyArgb(ReadOnlySpan<byte> pixels, Span<int> destination)
    {
        if (pixels.Length != 256 * 240 || destination.Length != pixels.Length)
            throw new ArgumentException("Expected matching 256x240 buffers.");
        for (int i = 0; i < pixels.Length; i++) destination[i] = unchecked((int)(0xff000000u | Palette[pixels[i] & 63]));
    }

    public static void WritePpm(Stream output, ReadOnlySpan<byte> pixels)
    {
        if (pixels.Length != 256 * 240) throw new ArgumentException("Expected a 256x240 frame.", nameof(pixels));
        output.Write(Encoding.ASCII.GetBytes("P6\n256 240\n255\n"));
        foreach (byte pixel in pixels)
        {
            uint rgb = Palette[pixel & 63];
            output.WriteByte((byte)(rgb >> 16)); output.WriteByte((byte)(rgb >> 8)); output.WriteByte((byte)rgb);
        }
    }
}
