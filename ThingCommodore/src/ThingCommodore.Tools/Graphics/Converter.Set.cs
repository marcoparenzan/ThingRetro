namespace ThingCommodore.Graphics;

// Several pictures into screens of characters that share one charset (see ImageSetAsset)
public static partial class Converter
{
    // The pictures of a set converted together. The shared colors (background and the two multicolors) are chosen from all of them,
    // each picture is reduced to the screen's cells and the cells' colors as a single picture is, and every character of every
    // screen goes into one charset (a character is its 8 bytes: the same bytes are the same character), which above the limit
    // merges the least used characters into the most similar ones.
    // "existing" is a charset that was already made, perhaps edited: its characters keep their codes, and the pictures reuse
    // them when they have the same bytes, and add the others after them
    public static ImageSetConversion ConvertSet(IReadOnlyList<(string Name, RgbImage Image)> pictures, ImageSetAsset set, string name,
        IReadOnlyList<(byte[] Bytes, string Description)>? existing = null)
    {
        if (pictures.Count == 0) throw new InvalidDataException("The image set has no pictures");
        var asset = set.ToGraphics();
        var palette = C64Palette.Named(asset.Palette);
        var layout = new Layout(asset);
        int cellCount = layout.Columns * layout.Rows;

        var resized = pictures.Select(p => Resize(p.Image, layout, asset)).ToList();

        // Shared colors: those all the pictures use most, unless the set fixes them
        var total = new int[16];
        foreach (var (colors, opaque) in resized)
        {
            var counts = Counts(colors, opaque, palette);
            for (int c = 0; c < 16; c++) total[c] += counts[c];
        }
        var popular = Ranked(total);
        int background = asset.Background ?? popular[0];
        int multicolor1 = asset.Multicolor1 ?? popular.First(c => c != background);
        int multicolor2 = asset.Multicolor2 ?? popular.First(c => c != background && c != multicolor1);

        var charset = new Charset(layout.Multicolor);
        if (existing is not null) charset.Freeze(existing.Select(e => (e.Bytes, e.Description)));
        int before = charset.Count;

        var pictureCells = new List<int[][]>();
        var pictureCodes = new List<int[]>();
        var screens = new int[pictures.Count * cellCount];
        for (int p = 0; p < pictures.Count; p++)
        {
            var (colors, opaque) = resized[p];
            // Pixels outside the picture (fit) are background
            for (int i = 0; i < colors.Length; i++) if (!opaque[i]) colors[i] = palette[background];

            var cells = new int[cellCount][];
            for (int cell = 0; cell < cellCount; cell++)
                cells[cell] = BestCellColors(cell, colors, opaque, layout, palette, asset.Dither, background, multicolor1, multicolor2);
            var codes = PlacePixels(colors, opaque, cells, layout, palette, asset.Dither);
            pictureCells.Add(cells);
            pictureCodes.Add(codes);
            for (int cell = 0; cell < cellCount; cell++)
                screens[p * cellCount + cell] = charset.Add(CellBytes(codes, layout, cell), $"from {pictures[p].Name}");
        }

        int found = charset.Count;
        charset.Reduce(screens, set.CharacterLimit);

        var result = new List<SetScreen>();
        var used = new HashSet<string>();
        for (int p = 0; p < pictures.Count; p++)
        {
            var codes = pictureCodes[p];
            var screen = screens[(p * cellCount)..((p + 1) * cellCount)];
            for (int cell = 0; cell < cellCount; cell++) WriteCell(codes, layout, cell, charset[screen[cell]]);
            var colorRam = new byte[cellCount];
            for (int cell = 0; cell < cellCount; cell++)
                colorRam[cell] = (byte)(layout.Multicolor ? pictureCells[p][cell][3] | 8 : pictureCells[p][cell][1]);

            string screenName = CapturedCharset.Identifier(Path.GetFileNameWithoutExtension(pictures[p].Name));
            for (int n = 2; !used.Add(screenName); n++) screenName = $"{CapturedCharset.Identifier(Path.GetFileNameWithoutExtension(pictures[p].Name))}_{n}";
            result.Add(new SetScreen(screenName, pictures[p].Name, screen, colorRam, Render(codes, pictureCells[p], layout, palette)));
        }

        int added = charset.Count - before;
        return new ImageSetConversion
        {
            Name = CapturedCharset.Identifier(name),
            First = set.First,
            Multicolor = layout.Multicolor,
            Columns = layout.Columns,
            Rows = layout.Rows,
            Background = background,
            Multicolor1 = multicolor1,
            Multicolor2 = multicolor2,
            Palette = asset.Palette,
            Characters = Enumerable.Range(0, charset.Count).Select(i => charset[i]).ToList(),
            Descriptions = Enumerable.Range(0, charset.Count).Select(charset.Tag).ToList(),
            Screens = result,
            Frozen = before,
            Summary = $"{pictures.Count} screen{(pictures.Count == 1 ? "" : "s")} of {layout.Columns}x{layout.Rows}, {(layout.Multicolor ? "multicolor" : "hires")}: " +
                      $"{charset.Count} characters" + (before > 0 ? $" ({before} from the charset that was there, {added} new)" : "") +
                      (found > charset.Count ? $", from {found}: similar ones merged" : ""),
        };
    }
}
