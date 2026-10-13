using System.Globalization;
using System.Text.RegularExpressions;

namespace Thing64.WebStudio.CodeBehind;

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

    // The characters after the charset label, one per 8 values; a line of exactly 8 values lends them its comment
    public static List<Character> Read(string[] lines, AsmRegion region, string charsetLabel, ProgramImage? image)
    {
        int start = LabelLine(lines, region, charsetLabel);
        var characters = new List<Character>();
        var pending = new List<int>();
        for (int i = start + 1; i < region.EndLine; i++)
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

    // The lines from the charset label to the end of the region written again: one character per line
    public static string[] Write(string[] lines, AsmRegion region, string charsetLabel, IReadOnlyList<Character> characters, int first)
    {
        int start = LabelLine(lines, region, charsetLabel);
        var (indent, keyword) = region.ByteStyle(lines);
        var written = characters.Select((c, i) =>
            $"{indent}{keyword} {string.Join(",", c.Bytes.Select(b => "$" + b.ToString("X2", CultureInfo.InvariantCulture)))} ; ${first + i:X2}"
            + (c.Description.Length > 0 ? " " + c.Description : ""));
        return [.. lines[..(start + 1)], .. written, .. lines[region.EndLine..]];
    }

    static int LabelLine(string[] lines, AsmRegion region, string label) =>
        region.LabelLines(lines).FirstOrDefault(l => l.Label == label) is { Label: not null } found
            ? found.Line
            : throw new FormatException($"The label {label} is not in the region");
}
