namespace ThingCommodore.Printers;

/*
    The paper of a dot matrix printer: sheets of dots. A dot is a point of the grid of the page, not a pixel of a screen: 240 per
    inch across (so that the 80 dots per inch of the character cells and the 60 of the bit images both fit) and 72 per inch down,
    one per point of the PDF. The sheet is A4 with the printable width of 8 inches.
 */
public sealed class DotPaper
{
    public const int WIDTH = 1920;     // 8 inches at 240 dots per inch
    public const int HEIGHT = 780;     // rows of 1/72 inch: a little more than 10.8 inches, A4 minus the margins

    readonly List<bool[]> sheets = [];

    public DotPaper() => NewSheet();

    public int Sheets => sheets.Count;

    // The sheet the head is on
    public int Current => sheets.Count - 1;

    public void NewSheet() => sheets.Add(new bool[WIDTH * HEIGHT]);

    public bool this[int sheet, int x, int y] => sheets[sheet][y * WIDTH + x];

    public void Plot(int x, int y)
    {
        if (x < 0 || x >= WIDTH || y < 0 || y >= HEIGHT) return;
        sheets[^1][y * WIDTH + x] = true;
    }

    public void Plot(int x, int y, int width)
    {
        for (int i = 0; i < width; i++) Plot(x + i, y);
    }

    public void Clear()
    {
        sheets.Clear();
        NewSheet();
    }

    // The runs of dots of a sheet as rectangles (x, y, width, height), the horizontal runs of consecutive rows with the same
    // extent merged into one: this is what the PDF draws
    public List<(int X, int Y, int W, int H)> Rectangles(int sheet)
    {
        var dots = sheets[sheet];
        var result = new List<(int X, int Y, int W, int H)>();
        var open = new Dictionary<(int X, int W), int>();      // runs of the previous row -> index in result
        var next = new Dictionary<(int X, int W), int>();
        for (int y = 0; y < HEIGHT; y++)
        {
            next.Clear();
            int x = 0;
            while (x < WIDTH)
            {
                if (!dots[y * WIDTH + x]) { x++; continue; }
                int start = x;
                while (x < WIDTH && dots[y * WIDTH + x]) x++;
                var key = (start, x - start);
                if (open.TryGetValue(key, out int index))
                {
                    var r = result[index];
                    result[index] = (r.X, r.Y, r.W, r.H + 1);
                    next[key] = index;
                }
                else
                {
                    result.Add((start, y, x - start, 1));
                    next[key] = result.Count - 1;
                }
            }
            (open, next) = (next, open);
        }
        return result;
    }
}
