using System.Globalization;
using System.Text.RegularExpressions;

namespace Thing65.WebStudio.CodeBehind;

// Changes to the characters of a ;@ charmap region, on the text of its file. A character's screen code is its
// position, so inserting, deleting or moving one renumbers the ones after it: the tiles table follows, so that every
// tile keeps showing the same character. Each character is written on a line of its own, with its comment:
//   .byte $FF,$FF,$AA,$55,$AA,$55,$00,$00 ; $70 platform
// (the "$70" of the comment is rewritten with the new screen code, the rest of the comment travels with it)
public static partial class CharmapFile
{
    public sealed record Character(int[] Bytes, string Description);

    [GeneratedRegex(@"^\$[0-9A-Fa-f]{1,2}\s*")]
    private static partial Regex CodePrefix();

    // The characters after the charset label, up to the next label (an end label, other data) or the end of the
    // region, one per 8 values; a line of exactly 8 values lends them its comment
    public static List<Character> Read(string[] lines, AsmRegion region, string charsetLabel, ProgramImage? image)
    {
        var (start, end) = Span(lines, region, charsetLabel);
        var characters = new List<Character>();
        var pending = new List<int>();
        for (int i = start + 1; i < end; i++)
        {
            if (AsmRegion.LineValues(lines[i], image) is not { } values) continue;
            if (pending.Count == 0 && values.Count == 8)
            {
                characters.Add(new Character([.. values], CodePrefix().Replace(AsmRegion.CommentOf(lines[i]), "")));
                continue;
            }
            pending.AddRange(values);
            while (pending.Count >= 8)
            {
                characters.Add(new Character([.. pending.Take(8)], ""));
                pending.RemoveRange(0, 8);
            }
        }
        return characters;
    }

    // The .byte lines of the characters written again, one character per line where they were; the other lines
    // between the charset label and the next one (comments) stay after them, and so does everything from the next
    // label on (an end label like "charactersEnd:", which the program may use to compute the length)
    public static string[] Write(string[] lines, AsmRegion region, string charsetLabel, IReadOnlyList<Character> characters, int first)
    {
        var (start, end) = Span(lines, region, charsetLabel);
        var (indent, keyword) = region.ByteStyle(lines);
        var written = characters.Select((c, i) =>
            $"{indent}{keyword} {string.Join(",", c.Bytes.Select(b => "$" + b.ToString("X2", CultureInfo.InvariantCulture)))} ; ${first + i:X2}"
            + (c.Description.Length > 0 ? " " + c.Description : ""));
        var others = Enumerable.Range(start + 1, end - start - 1).Where(i => AsmRegion.LineValues(lines[i], null) is null).Select(i => lines[i]);
        return [.. lines[..(start + 1)], .. written, .. others, .. lines[end..]];
    }

    // The charset label's line, and where its data ends: the next label (its ;@ lines) or the end of the region
    static (int Start, int End) Span(string[] lines, AsmRegion region, string label)
    {
        var labels = region.LabelLines(lines).ToList();
        int index = labels.FindIndex(l => l.Label == label);
        if (index < 0) throw new FormatException($"The label {label} is not in the region");
        return (labels[index].Line, index + 1 < labels.Count ? labels[index + 1].Top : region.EndLine);
    }
}
