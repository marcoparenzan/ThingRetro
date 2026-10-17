using System.Buffers.Binary;
using System.IO.Compression;

namespace Thing65.Graphics;

// PNG, the lossless format of the pictures to convert and of the previews. A PNG is a signature and chunks
// (length, type, data, CRC): IHDR (size, depth, color type), PLTE (palette), tRNS (transparency), IDAT (the rows,
// compressed with zlib, each one prefixed by a filter byte) and IEND.
// Read: every color type and depth, not interlaced. Write: 8-bit RGBA
public static class Png
{
    static readonly byte[] Signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    public static RgbImage Read(byte[] file)
    {
        if (file.Length < 8 || !file.AsSpan(0, 8).SequenceEqual(Signature)) throw new InvalidDataException("Not a PNG file");

        int width = 0, height = 0, depth = 0, colorType = 0;
        byte[] palette = [];
        byte[] transparency = [];
        using var compressed = new MemoryStream();
        for (int at = 8; at + 8 <= file.Length;)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(file.AsSpan(at));
            string type = System.Text.Encoding.ASCII.GetString(file, at + 4, 4);
            var data = file.AsSpan(at + 8, length);
            switch (type)
            {
                case "IHDR":
                    width = BinaryPrimitives.ReadInt32BigEndian(data);
                    height = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
                    depth = data[8];
                    colorType = data[9];
                    if (data[12] != 0) throw new NotSupportedException("Interlaced PNG: save the image without interlacing");
                    break;
                case "PLTE": palette = data.ToArray(); break;
                case "tRNS": transparency = data.ToArray(); break;
                case "IDAT": compressed.Write(data); break;
            }
            if (type == "IEND") break;
            at += 12 + length;
        }

        int channels = colorType switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => throw new InvalidDataException($"PNG color type {colorType}") };
        int bitsPerPixel = channels * depth;
        int stride = (width * bitsPerPixel + 7) / 8;
        int step = Math.Max(1, bitsPerPixel / 8);   // the byte distance the filters use

        compressed.Position = 0;
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        using var raw = new MemoryStream();
        zlib.CopyTo(raw);
        byte[] rows = raw.ToArray();
        if (rows.Length < (stride + 1) * height) throw new InvalidDataException("The PNG is cut short");

        var image = new RgbImage(width, height);
        var previous = new byte[stride];
        var row = new byte[stride];
        for (int y = 0; y < height; y++)
        {
            int start = y * (stride + 1);
            Unfilter(rows[start], rows.AsSpan(start + 1, stride), previous, row, step);
            for (int x = 0; x < width; x++) image[x, y] = Pixel(row, x, colorType, depth, palette, transparency);
            (previous, row) = (row, previous);
        }
        return image;
    }

    // The five filters: each byte was stored as the difference from a prediction made of its left, upper and
    // upper-left neighbors
    static void Unfilter(byte filter, ReadOnlySpan<byte> line, byte[] previous, byte[] row, int step)
    {
        for (int i = 0; i < line.Length; i++)
        {
            int left = i >= step ? row[i - step] : 0;
            int up = previous[i];
            int upLeft = i >= step ? previous[i - step] : 0;
            row[i] = (byte)(line[i] + filter switch
            {
                0 => 0,
                1 => left,
                2 => up,
                3 => (left + up) / 2,
                4 => Paeth(left, up, upLeft),
                _ => throw new InvalidDataException($"PNG filter {filter}"),
            });
        }
    }

    static int Paeth(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    // One pixel of an unfiltered row; 16-bit samples keep their high byte
    static uint Pixel(byte[] row, int x, int colorType, int depth, byte[] palette, byte[] transparency)
    {
        int Sample(int index)
        {
            if (depth == 16) return row[index * 2];
            if (depth == 8) return row[index];
            int perByte = 8 / depth;
            int value = row[index / perByte] >> (8 - depth * (index % perByte + 1)) & ((1 << depth) - 1);
            return colorType == 3 ? value : value * 255 / ((1 << depth) - 1);
        }

        switch (colorType)
        {
            case 0:
            {
                int gray = Sample(x);
                bool clear = transparency.Length >= 2 && BinaryPrimitives.ReadUInt16BigEndian(transparency) >> (depth == 16 ? 8 : 0) == gray;
                return RgbImage.Argb(clear ? 0 : 255, gray, gray, gray);
            }
            case 2:
                return RgbImage.Argb(255, Sample(x * 3), Sample(x * 3 + 1), Sample(x * 3 + 2));
            case 3:
            {
                int index = Sample(x);
                if (index * 3 + 2 >= palette.Length) return 0;
                int alpha = index < transparency.Length ? transparency[index] : 255;
                return RgbImage.Argb(alpha, palette[index * 3], palette[index * 3 + 1], palette[index * 3 + 2]);
            }
            case 4:
            {
                int gray = Sample(x * 2);
                return RgbImage.Argb(Sample(x * 2 + 1), gray, gray, gray);
            }
            default:
                return RgbImage.Argb(Sample(x * 4 + 3), Sample(x * 4), Sample(x * 4 + 1), Sample(x * 4 + 2));
        }
    }

    public static byte[] Write(RgbImage image)
    {
        var rows = new byte[(image.Width * 4 + 1) * image.Height];
        for (int y = 0, at = 0; y < image.Height; y++)
        {
            rows[at++] = 0;   // no filter
            for (int x = 0; x < image.Width; x++)
            {
                uint pixel = image[x, y];
                rows[at++] = (byte)(pixel >> 16);
                rows[at++] = (byte)(pixel >> 8);
                rows[at++] = (byte)pixel;
                rows[at++] = (byte)(pixel >> 24);
            }
        }
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true)) zlib.Write(rows);

        using var file = new MemoryStream();
        file.Write(Signature);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, image.Width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), image.Height);
        header[8] = 8;    // bits per sample
        header[9] = 6;    // RGBA
        Chunk(file, "IHDR", header);
        Chunk(file, "IDAT", compressed.ToArray());
        Chunk(file, "IEND", []);
        return file.ToArray();
    }

    static void Chunk(Stream file, string type, byte[] data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        file.Write(number);
        byte[] typeAndData = [.. System.Text.Encoding.ASCII.GetBytes(type), .. data];
        file.Write(typeAndData);
        BinaryPrimitives.WriteUInt32BigEndian(number, Crc32(typeAndData));
        file.Write(number);
    }

    static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(n =>
    {
        uint c = (uint)n;
        for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    static uint Crc32(byte[] bytes)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in bytes) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFF;
    }
}
