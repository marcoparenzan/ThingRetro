using System.IO.Compression;
using System.Text;

namespace ThingCommodore.Printers;

// The sheets of a DotPaper as a PDF: A4 pages with the dots as black rectangles (vector graphics, no fonts), one stream per page
public static class PdfFile
{
    const double PAGE_WIDTH = 595, PAGE_HEIGHT = 842;
    const double LEFT = (PAGE_WIDTH - 576) / 2;       // 8 inches of paper centered
    const double TOP = 36;
    const double DOT_WIDTH = 72.0 / 240;               // a unit of the paper across, in points
    const double DOT_HEIGHT = 1;                       // down

    public static byte[] Write(DotPaper paper)
    {
        var offsets = new List<int>();
        using var file = new MemoryStream();

        void Put(string text) { var bytes = Encoding.ASCII.GetBytes(text); file.Write(bytes); }
        void Object(int number, Action body)
        {
            offsets.Add((int)file.Length);
            Put($"{number} 0 obj\n");
            body();
            Put("endobj\n");
        }

        Put("%PDF-1.4\n%âãÏÓ\n");

        int sheets = paper.Sheets;
        // 1 = catalog, 2 = pages, then a page and its content for each sheet
        Object(1, () => Put("<< /Type /Catalog /Pages 2 0 R >>\n"));
        Object(2, () =>
        {
            Put("<< /Type /Pages /Count " + sheets + " /Kids [");
            for (int i = 0; i < sheets; i++) Put($"{3 + 2 * i} 0 R ");
            Put("] >>\n");
        });
        for (int i = 0; i < sheets; i++)
        {
            int content = 4 + 2 * i;
            Object(3 + 2 * i, () => Put($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PAGE_WIDTH} {PAGE_HEIGHT}] /Contents {content} 0 R >>\n"));
            var stream = Content(paper, i);
            Object(content, () =>
            {
                Put($"<< /Length {stream.Length} /Filter /FlateDecode >>\nstream\n");
                file.Write(stream);
                Put("\nendstream\n");
            });
        }

        int xref = (int)file.Length;
        Put($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (int offset in offsets) Put($"{offset:D10} 00000 n \n");
        Put($"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return file.ToArray();
    }

    static byte[] Content(DotPaper paper, int sheet)
    {
        var text = new StringBuilder("0 g\n");
        var rectangles = paper.Rectangles(sheet);
        for (int i = 0; i < rectangles.Count; i++)
        {
            var (x, y, w, h) = rectangles[i];
            double left = LEFT + x * DOT_WIDTH;
            double bottom = PAGE_HEIGHT - TOP - (y + h) * DOT_HEIGHT;
            text.Append(Num(left)).Append(' ').Append(Num(bottom)).Append(' ')
                .Append(Num(w * DOT_WIDTH)).Append(' ').Append(Num(h * DOT_HEIGHT)).Append(" re\n");
            if (i % 500 == 499) text.Append("f\n");
        }
        text.Append("f\n");

        using var packed = new MemoryStream();
        using (var zip = new ZLibStream(packed, CompressionLevel.Optimal, leaveOpen: true))
            zip.Write(Encoding.ASCII.GetBytes(text.ToString()));
        return packed.ToArray();
    }

    static string Num(double value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}
