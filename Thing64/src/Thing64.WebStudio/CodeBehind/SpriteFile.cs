using System.Globalization;
using System.Text.RegularExpressions;

namespace Thing64.WebStudio.CodeBehind;

// Changes to the structure of a ;@ sprites region, on the text of its file: sprites added, duplicated, deleted,
// renamed (in every source of the project), and the ;@ animation lines of its header
public static class SpriteFile
{
    // A sprite as the region writes them: its label, 7 lines of 3 rows of 3 bytes, the unused 64th byte
    static IEnumerable<string> Block(string name, IReadOnlyList<int> bytes, (string Indent, string Keyword) style)
    {
        yield return name + ":";
        for (int row = 0; row < SpriteSheet.Rows; row += 3)
            yield return style.Indent + style.Keyword + " " + string.Join(", ", Enumerable.Range(row, 3).Select(r =>
                string.Join(",", Enumerable.Range(0, 3).Select(c => "$" + bytes[r * 3 + c].ToString("X2", CultureInfo.InvariantCulture)))));
        yield return style.Indent + style.Keyword + " 0";
    }

    // After the sprite number "after" (-1: at the start), with its own ;@ lines if any
    public static string[] Insert(string[] lines, AsmRegion region, int after, string name, IReadOnlyList<int> bytes, IReadOnlyList<string> directives)
    {
        var labels = region.LabelLines(lines);
        int at = after + 1 < labels.Count ? labels[after + 1].Top : region.EndLine;
        var block = directives.Select(d => ";@ " + d).Concat(Block(name, bytes, region.ByteStyle(lines)));
        return [.. lines[..at], .. block, .. lines[at..]];
    }

    // The sprite's ;@ lines, label and data, up to the next sprite (or the end of the region)
    public static string[] Delete(string[] lines, AsmRegion region, int sprite)
    {
        var labels = region.LabelLines(lines);
        int from = labels[sprite].Top, to = sprite + 1 < labels.Count ? labels[sprite + 1].Top : region.EndLine;
        return [.. lines[..from], .. lines[to..]];
    }

    // The ;@ animation lines of the header written again, after the other header lines
    public static string[] WriteAnimations(string[] lines, AsmRegion region, IEnumerable<(string Name, IEnumerable<string> Frames, int Speed)> animations)
    {
        int headerEnd = region.StartLine + region.Header.Count;
        var kept = lines[region.StartLine..headerEnd].Where(l => !l.TrimStart()[2..].TrimStart().StartsWith("animation ")).ToList();
        var written = animations.Select(a => $";@ animation  {a.Name} frames={string.Join(",", a.Frames)} speed={a.Speed}");
        return [.. lines[..region.StartLine], .. kept, .. written, .. lines[headerEnd..]];
    }

    // A label renamed as a whole word in every source of the project (references, animations); how many changed
    public static int RenameEverywhere(Project project, string oldName, string newName)
    {
        var word = new Regex($@"(?<![\w.@]){Regex.Escape(oldName)}(?!\w)");
        int changes = 0;
        foreach (string file in Directory.EnumerateFiles(project.Folder, "*.*", SearchOption.AllDirectories)
                     .Where(f => Path.GetExtension(f).ToLowerInvariant() is ".asm" or ".a" or ".s" or ".inc"))
        {
            string text = File.ReadAllText(file);
            int count = word.Matches(text).Count;
            if (count == 0) continue;
            File.WriteAllText(file, word.Replace(text, newName));
            changes += count;
        }
        return changes;
    }
}
