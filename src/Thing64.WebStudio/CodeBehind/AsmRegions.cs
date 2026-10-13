using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Thing64.Asm;
using Thing64.Build;

namespace Thing64.WebStudio.CodeBehind;

// "Code behind" for assembly sources: a region of data marked by comments, which a visual editor opens and rewrites.
//   ;@ tilemap  width=40 height=25
//   ;@          tiles=charmapChars colors=charmapColors     (more ;@ lines, more parameters)
//   levelMap:
//               .byte $00,$00,...
//   ;@ end
// For the assembler they are comments. The parameters are numbers or symbols of the program ($70, TILE_COUNT,
// charmapChars), resolved by assembling the project: the editor sees the same bytes the C64 will see
public sealed partial class AsmRegion
{
    public required string Editor { get; init; }
    public required IReadOnlyDictionary<string, string> Parameters { get; init; }
    public required int Index { get; init; }        // the n-th region of the file
    public required int StartLine { get; init; }    // the ;@ line that opens it (0-based)
    public required int EndLine { get; init; }      // the ;@ end line
    public string? Label => Labels.FirstOrDefault();  // the first label inside, for the tab's name
    public IReadOnlyList<string> Labels { get; init; } = [];   // all the labels inside, in order
    public IReadOnlyList<string> Header { get; init; } = [];   // the text of each opening ;@ line, without ";@"
    public IReadOnlyDictionary<int, string> Inner { get; init; } = new Dictionary<int, string>();   // ;@ lines inside, by line

    [GeneratedRegex(@"^\s*;@\s*(.*)$")]
    private static partial Regex DirectiveLine();

    [GeneratedRegex(@"(\w+)\s*=\s*(\S+)")]
    private static partial Regex Parameter();

    [GeneratedRegex(@"^([A-Za-z_]\w*):?(\s|$)")]
    private static partial Regex LabelLine();

    [GeneratedRegex(@"^\s*[.!]byte\s+(.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex ByteLine();

    public static IReadOnlyList<AsmRegion> Find(string[] lines)
    {
        var regions = new List<AsmRegion>();
        for (int i = 0; i < lines.Length; i++)
        {
            var open = DirectiveLine().Match(lines[i]);
            if (!open.Success) continue;
            string[] words = open.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0 || words[0] == "end") continue;

            var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var header = new List<string>();
            int line = i;
            for (; line < lines.Length && DirectiveLine().Match(lines[line]) is { Success: true } more && !more.Groups[1].Value.Trim().Equals("end"); line++)
            {
                header.Add(more.Groups[1].Value.Trim());
                if (more.Groups[1].Value.TrimStart().StartsWith("animation ")) continue;   // the animations' own parameters
                foreach (Match p in Parameter().Matches(more.Groups[1].Value)) parameters[p.Groups[1].Value] = p.Groups[2].Value;
            }

            int end = line;
            while (end < lines.Length && !(DirectiveLine().Match(lines[end]) is { Success: true } close && close.Groups[1].Value.Trim() == "end")) end++;
            if (end == lines.Length) throw new FormatException($"Line {i + 1}: ;@ {words[0]} without ;@ end");

            var labels = lines[line..end].Select(l => LabelLine().Match(l)).Where(m => m.Success).Select(m => m.Groups[1].Value).ToList();
            var inner = new Dictionary<int, string>();
            for (int k = line; k < end; k++)
                if (DirectiveLine().Match(lines[k]) is { Success: true } directive) inner[k] = directive.Groups[1].Value.Trim();
            regions.Add(new AsmRegion
            {
                Editor = words[0], Parameters = parameters, Index = regions.Count, StartLine = i, EndLine = end,
                Labels = labels, Header = header, Inner = inner,
            });
            i = end;
        }
        return regions;
    }

    // The values of the .byte lines of the region, in order (numbers, characters, or symbols of the program)
    public List<int> ReadBytes(string[] lines, ProgramImage? image)
    {
        var values = new List<int>();
        for (int i = StartLine + 1; i < EndLine; i++)
        {
            var data = ByteLine().Match(StripComment(lines[i]));
            if (!data.Success) continue;
            foreach (string item in data.Groups[1].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                values.Add(ProgramImage.Value(item, image) & 0xFF);
        }
        return values;
    }

    // The values written back. With as many values as there are, each one takes the place of the old one: labels,
    // spacing, groups and comments stay. Otherwise the .byte lines are replaced by new ones, perRow to a line, in the
    // style of the first one, each keeping the comment at the end of the line it replaces. Nothing else changes
    public string[] WriteBytes(string[] lines, IReadOnlyList<int> values, int perRow)
    {
        if (CountValues(lines) == values.Count) return WriteInPlace(lines, values);

        int first = -1, last = -1;
        var comments = new List<string>();
        for (int i = StartLine + 1; i < EndLine; i++)
            if (ByteLine().IsMatch(StripComment(lines[i])))
            {
                if (first < 0) first = i;
                last = i;
                string code = StripComment(lines[i]);
                comments.Add(code.Length < lines[i].Length ? lines[i][code.TrimEnd().Length..] : "");
            }

        string indent = "            ", keyword = ".byte";
        if (first >= 0)
        {
            indent = lines[first][..(lines[first].Length - lines[first].TrimStart().Length)];
            keyword = lines[first].TrimStart()[..5];
        }
        var rows = new List<string>();
        for (int at = 0; at < values.Count; at += perRow)
            rows.Add(indent + keyword + " " + string.Join(",", values.Skip(at).Take(perRow).Select(v => "$" + v.ToString("X2", CultureInfo.InvariantCulture)))
                     + comments.ElementAtOrDefault(rows.Count));

        // Without .byte lines yet, the data goes right before ;@ end
        int from = first >= 0 ? first : EndLine, to = first >= 0 ? last + 1 : EndLine;
        return [.. lines[..from], .. rows, .. lines[to..]];
    }

    // Each label of the region with the number of values before it, and the ;@ lines right above it
    public IReadOnlyList<(string Label, int Offset, IReadOnlyList<string> Directives)> LabelOffsets(string[] lines)
    {
        var result = new List<(string, int, IReadOnlyList<string>)>();
        var pending = new List<string>();
        int count = 0;
        for (int i = StartLine + 1; i < EndLine; i++)
        {
            if (Inner.TryGetValue(i, out var directive)) { pending.Add(directive); continue; }
            if (LabelLine().Match(lines[i]) is { Success: true } label)
            {
                result.Add((label.Groups[1].Value, count, pending.ToList()));
                pending.Clear();
            }
            if (ByteLine().Match(StripComment(lines[i])) is { Success: true } data)
                count += data.Groups[1].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Length;
        }
        return result;
    }

    // Where each label of the region is: its line, and the first of the ;@ lines right above it (or the label's)
    public IReadOnlyList<(string Label, int Line, int Top)> LabelLines(string[] lines)
    {
        var result = new List<(string, int, int)>();
        for (int i = StartLine + 1; i < EndLine; i++)
        {
            if (LabelLine().Match(lines[i]) is not { Success: true } label) continue;
            int top = i;
            while (top - 1 > StartLine && Inner.ContainsKey(top - 1)) top--;
            result.Add((label.Groups[1].Value, i, top));
        }
        return result;
    }

    // The indentation and keyword of the region's first .byte line, to write new lines in the same style
    public (string Indent, string Keyword) ByteStyle(string[] lines)
    {
        for (int i = StartLine + 1; i < EndLine; i++)
            if (ByteLine().IsMatch(StripComment(lines[i])))
                return (lines[i][..(lines[i].Length - lines[i].TrimStart().Length)], lines[i].TrimStart()[..5]);
        return ("            ", ".byte");
    }

    public static bool IsIdentifier(string name) => Regex.IsMatch(name, @"^[A-Za-z_]\w*$");

    // name=value pairs of a ;@ line
    public static Dictionary<string, string> ParametersOf(string text) =>
        Parameter().Matches(text).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value, StringComparer.OrdinalIgnoreCase);

    int CountValues(string[] lines)
    {
        int count = 0;
        for (int i = StartLine + 1; i < EndLine; i++)
            if (ByteLine().Match(StripComment(lines[i])) is { Success: true } data)
                count += data.Groups[1].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Length;
        return count;
    }

    string[] WriteInPlace(string[] lines, IReadOnlyList<int> values)
    {
        var result = (string[])lines.Clone();
        int next = 0;
        for (int i = StartLine + 1; i < EndLine; i++)
        {
            string code = StripComment(lines[i]);
            var data = ByteLine().Match(code);
            if (!data.Success) continue;
            int start = data.Groups[1].Index;
            var items = code[start..].Split(',');
            for (int k = 0; k < items.Length; k++)
            {
                string item = items[k];
                if (item.Trim().Length == 0) continue;
                // A value that did not change keeps the way it was written (0, a symbol, 'A')
                int value = values[next++];
                if (TryValue(item, out int old) && (old & 0xFF) == value) continue;
                string lead = item[..(item.Length - item.TrimStart().Length)];
                string trail = item[item.TrimEnd().Length..];
                items[k] = lead + "$" + value.ToString("X2", CultureInfo.InvariantCulture) + trail;
            }
            result[i] = code[..start] + string.Join(",", items) + lines[i][code.Length..];
        }
        return result;
    }

    static bool TryValue(string text, out int value)
    {
        try
        {
            value = ProgramImage.Value(text, null);
            return true;
        }
        catch (Exception e) when (e is FormatException or OverflowException)
        {
            value = 0;
            return false;
        }
    }

    static string StripComment(string line)
    {
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"') quoted = !quoted;
            else if (line[i] == ';' && !quoted) return line[..i];
        }
        return line;
    }
}

// The program assembled: its symbols and its memory, to resolve the parameters of the regions
public sealed class ProgramImage(Assembler.Result result)
{
    public IReadOnlyDictionary<string, int> Symbols => result.Symbols;

    public int? Symbol(string name) => result.Symbols.TryGetValue(name, out int value) ? value : null;

    public byte[] Read(int address, int count)
    {
        var bytes = new byte[Math.Max(0, count)];
        for (int i = 0; i < bytes.Length; i++)
        {
            int at = address + i - result.Origin;
            bytes[i] = at >= 0 && at < result.Bytes.Length ? result.Bytes[at] : (byte)0;
        }
        return bytes;
    }

    // Where the next label after this address starts: how long the data at a label probably is
    public int NextLabelAfter(int address) =>
        result.Symbols.Values.Where(v => v > address && v <= result.Origin + result.Bytes.Length).DefaultIfEmpty(result.Origin + result.Bytes.Length).Min();

    // $hex, %binary, decimal, 'c', a symbol, or symbol+number
    public static int Value(string text, ProgramImage? image)
    {
        text = text.Trim();
        int plus = text.LastIndexOf('+');
        if (plus > 0) return Value(text[..plus], image) + Value(text[(plus + 1)..], image);
        if (text.StartsWith('$')) return int.Parse(text[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        if (text.StartsWith('%')) return System.Convert.ToInt32(text[1..], 2);
        if (text.Length == 3 && text[0] == '\'' && text[2] == '\'') return text[1];
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)) return number;
        return image?.Symbol(text) ?? throw new FormatException($"Unknown symbol {text}");
    }

    // The program of the project: the source of the first output of thing64.json, or main.asm
    public static ProgramImage Assemble(Project project)
    {
        string manifest = Path.Combine(project.Folder, Project.ManifestName);
        string source = "main.asm";
        if (File.Exists(manifest) && BuildManifest.Load(manifest).Outputs.FirstOrDefault(o => o.Source?.EndsWith(".asm", StringComparison.OrdinalIgnoreCase) == true) is { } output)
            source = output.Source!;
        return new ProgramImage(ProgramBuilder.Assemble(project.FullPath(source)));
    }
}
