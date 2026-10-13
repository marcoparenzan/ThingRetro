namespace Thing64.Asm;

// An error in the source: file, line (from 1) and reason, kept apart so that the editor can mark the line.
// FilePath is the full path of the file with the error, when known (for files included with !source).
// The full message is "name, line N: reason" followed by the text of the line
public sealed class AssemblerException(string sourceName, int line, string lineText, string reason, string? filePath = null)
    : FormatException($"{sourceName}, line {line}: {reason}" + (lineText.Trim().Length > 0 ? $"\n    {lineText.Trim()}" : ""))
{
    public string SourceName { get; } = sourceName;
    public int Line { get; } = line;
    public string Reason { get; } = reason;
    public string? FilePath { get; } = filePath;
}
