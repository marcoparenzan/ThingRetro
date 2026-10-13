using System.Globalization;
using System.Text.RegularExpressions;

namespace Thing64.WebStudio.CodeBehind;

// Tiles added and removed: a tile is a number, used in the tables of its ;@ charmap region, in the ;@ tilemap regions
// that draw through those tables, and in constants (TILE_NAME = n, TILE_COUNT = n) anywhere in the project.
// Constants are changed only when their value is written as a number
public static partial class TileFile
{
    [GeneratedRegex(@"^(\s*)([A-Za-z_]\w*)(\s*=\s*)(\$[0-9A-Fa-f]+|\d+)(.*)$")]
    private static partial Regex ConstantLine();

    // A file's lines, and how to join them again (keeping its line endings)
    sealed record Text(string Path, string[] Lines, string Newline)
    {
        public static Text Read(string path)
        {
            string text = File.ReadAllText(path);
            return new Text(path, text.Split('\n').Select(l => l.TrimEnd('\r')).ToArray(), text.Contains("\r\n") ? "\r\n" : "\n");
        }

        public void Write(string[] lines) => File.WriteAllText(Path, string.Join(Newline, lines));
    }

    static IEnumerable<string> Sources(Project project) =>
        Directory.EnumerateFiles(project.Folder, "*.asm", SearchOption.AllDirectories)
            .Where(f => !Path.GetRelativePath(project.Folder, f).Split(Path.DirectorySeparatorChar).Any(p => p.StartsWith('.')));

    // The data lines between a label of the region and the next label become one .byte line with these values
    public static string[] WriteTable(string[] lines, AsmRegion region, string label, IReadOnlyList<int> values)
    {
        var labels = region.LabelLines(lines).ToList();
        int index = labels.FindIndex(l => l.Label == label);
        if (index < 0) throw new FormatException($"The label {label} is not in the region");
        int start = labels[index].Line, end = index + 1 < labels.Count ? labels[index + 1].Top : region.EndLine;
        var data = Enumerable.Range(start + 1, end - start - 1).Where(i => AsmRegion.LineValues(lines[i], null) is not null).ToList();
        var (indent, keyword) = region.ByteStyle(lines);
        string comment = data.Count > 0 ? AsmRegion.CommentOf(lines[data[0]]) : "";
        string line = $"{indent}{keyword} {string.Join(",", values.Select(v => "$" + v.ToString("X2", CultureInfo.InvariantCulture)))}"
                      + (comment.Length > 0 ? " ; " + comment : "");
        int at = data.Count > 0 ? data[0] : start + 1;
        var result = lines.Where((_, i) => !data.Contains(i)).ToList();
        result.Insert(at, line);
        return [.. result];
    }

    // The maps (;@ tilemap regions drawing through this tiles table) that use a tile: "file(line)" for each
    public static List<string> MapsUsing(Project project, string tilesLabel, int tile)
    {
        var users = new List<string>();
        foreach (var (file, text, region) in Maps(project, tilesLabel))
            if (region.ReadBytes(text.Lines, null).Contains(tile)) users.Add($"{project.RelativePath(file)}({region.StartLine + 1})");
        return users;
    }

    // The files an addition or a removal may change, besides the region's own: the maps and the constants
    public static IReadOnlyList<string> FilesInvolved(Project project, string prefix, string tilesLabel) =>
        Maps(project, tilesLabel).Select(m => m.File).Concat(Constants(project, prefix).Select(c => c.File))
            .Distinct().Select(project.RelativePath).ToList();

    // Every tile number in those maps changed through a function
    public static void RemapMaps(Project project, string tilesLabel, Func<int, int> remap)
    {
        foreach (var (_, text, region) in Maps(project, tilesLabel).ToList())
        {
            var values = region.ReadBytes(text.Lines, null).Select(remap).ToList();
            int perRow = region.Parameters.TryGetValue("width", out var width) && int.TryParse(width, out int w) ? w : 40;
            text.Write(region.WriteBytes(text.Lines, values, perRow));
        }
    }

    static IEnumerable<(string File, Text Text, AsmRegion Region)> Maps(Project project, string tilesLabel)
    {
        foreach (string file in Sources(project))
        {
            var text = Text.Read(file);
            if (!text.Lines.Any(l => l.Contains(";@"))) continue;
            foreach (var region in AsmRegion.Find(text.Lines))
                if (region.Editor == "tilemap" && region.Parameters.GetValueOrDefault("tiles") == tilesLabel)
                    yield return (file, text, region);
        }
    }

    // A new constant prefix+name = value, written after the constant with the highest value (in its style), and
    // prefix+COUNT set to count
    public static void AddConstant(Project project, string prefix, string name, int value, int count)
    {
        var constants = Constants(project, prefix);
        var last = constants.Where(c => !c.Name.EndsWith("_COUNT")).MaxBy(c => c.Value);
        if (last is not null)
        {
            var text = Text.Read(last.File);
            var match = ConstantLine().Match(text.Lines[last.Line]);
            // The "=" in the same column as the line it follows (one space at least after a longer name)
            string assign = match.Groups[3].Value;
            int equals = match.Groups[3].Index + assign.IndexOf('=');
            string left = match.Groups[1].Value + prefix + name + " ";
            string line = left.PadRight(equals) + assign[assign.IndexOf('=')..] + value.ToString(CultureInfo.InvariantCulture);
            int column = text.Lines[last.Line].IndexOf(';');   // the comment in the same column
            if (column > 0) line = line.PadRight(column - 1) + " ; added in the charmap editor";
            var lines = text.Lines.ToList();
            lines.Insert(last.Line + 1, line);
            text.Write([.. lines]);
        }
        SetCount(project, prefix, count);
    }

    // The constant of a tile removed, those above it moved down by one, prefix+COUNT set to count
    public static void RemoveConstant(Project project, string prefix, int value, int count)
    {
        foreach (var group in Constants(project, prefix).Where(c => !c.Name.EndsWith("_COUNT") && c.Value >= value).GroupBy(c => c.File))
        {
            var text = Text.Read(group.Key);
            var lines = text.Lines.ToList();
            foreach (var constant in group.OrderByDescending(c => c.Line))
            {
                if (constant.Value == value) lines.RemoveAt(constant.Line);
                else lines[constant.Line] = Renumber(lines[constant.Line], constant.Value - 1);
            }
            text.Write([.. lines]);
        }
        SetCount(project, prefix, count);
    }

    static void SetCount(Project project, string prefix, int count)
    {
        foreach (var constant in Constants(project, prefix).Where(c => c.Name == prefix + "COUNT"))
        {
            var text = Text.Read(constant.File);
            var lines = text.Lines.ToArray();
            lines[constant.Line] = Renumber(lines[constant.Line], count);
            text.Write(lines);
        }
    }

    // The number of a constant's line replaced, keeping the comment in its column
    static string Renumber(string line, int value)
    {
        var match = ConstantLine().Match(line);
        string number = value.ToString(CultureInfo.InvariantCulture);
        string rest = match.Groups[5].Value;
        int spaces = rest.Length - rest.TrimStart().Length + match.Groups[4].Length - number.Length;
        return match.Groups[1].Value + match.Groups[2].Value + match.Groups[3].Value + number
               + (rest.Trim().Length > 0 ? new string(' ', Math.Max(1, spaces)) + rest.TrimStart() : "");
    }

    sealed record Constant(string File, int Line, string Name, int Value);

    static List<Constant> Constants(Project project, string prefix)
    {
        var found = new List<Constant>();
        foreach (string file in Sources(project))
        {
            var text = Text.Read(file);
            for (int i = 0; i < text.Lines.Length; i++)
            {
                var match = ConstantLine().Match(text.Lines[i]);
                if (!match.Success || !match.Groups[2].Value.StartsWith(prefix, StringComparison.Ordinal)) continue;
                string number = match.Groups[4].Value;
                int value = number.StartsWith('$') ? int.Parse(number[1..], NumberStyles.HexNumber) : int.Parse(number);
                found.Add(new Constant(file, i, match.Groups[2].Value, value));
            }
        }
        return found;
    }
}
