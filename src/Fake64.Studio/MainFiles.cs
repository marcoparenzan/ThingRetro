using System.Text.RegularExpressions;
using Fake64.Computer;

namespace Fake64.Studio;

// Which file to assemble when F5/F7 is pressed on a source. A project is usually a main file that includes the
// others with !source: assembling an included file alone would fail.
// 1. A main file set by the user (explorer or Build menu) for the file's folder or one of its parents;
// 2. otherwise the file that includes this one, followed up to the file that nobody includes
//    (looking in the same folder and in the parent one);
// 3. otherwise the file itself.
// The choices are kept in the settings (folder -> main file), so nothing is written into the project
sealed partial class MainFiles(AppSettings settings)
{
    public string? MainFor(string folder) =>
        settings.MainFiles.TryGetValue(Normalize(folder), out var main) && File.Exists(main) ? main : null;

    public bool IsMain(string path) =>
        Path.GetDirectoryName(path) is string folder &&
        string.Equals(MainFor(folder), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);

    // The main file for the sources of its folder (and of the folders below it)
    public void SetMain(string path)
    {
        settings.MainFiles[Normalize(Path.GetDirectoryName(path)!)] = Path.GetFullPath(path);
        settings.Save();
    }

    public void ClearMain(string folder)
    {
        if (settings.MainFiles.Remove(Normalize(folder))) settings.Save();
    }

    public string Resolve(string path)
    {
        path = Path.GetFullPath(path);
        for (var folder = Path.GetDirectoryName(path); folder is not null; folder = Path.GetDirectoryName(folder))
            if (MainFor(folder) is string main) return main;

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { path };
        string current = path;
        while (Includer(current) is string includer && visited.Add(includer)) current = includer;
        return current;
    }

    [GeneratedRegex("""^\s*(?:[A-Za-z_.@][\w]*:?\s+)?[!.](?:source|src|include)\s+["<]([^">]+)[">]""",
        RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex IncludePattern();

    // The .asm file (in the same folder or in the parent one) whose !source points to this file, if exactly one does
    static string? Includer(string path)
    {
        var folder = Path.GetDirectoryName(path)!;
        var candidates = new[] { folder, Path.GetDirectoryName(folder) }
            .OfType<string>()
            .SelectMany(f => SafeFiles(f))
            .Where(file => !string.Equals(file, path, StringComparison.OrdinalIgnoreCase))
            .Where(file => Includes(file, path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return candidates.Count == 1 ? candidates[0] : null;
    }

    static IEnumerable<string> SafeFiles(string folder)
    {
        try
        {
            return Directory.GetFiles(folder, "*.asm");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    static bool Includes(string file, string target)
    {
        try
        {
            string folder = Path.GetDirectoryName(file)!;
            return IncludePattern().Matches(File.ReadAllText(file)).Any(m =>
                string.Equals(Path.GetFullPath(Path.Combine(folder, m.Groups[1].Value)), target, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    static string Normalize(string folder) => Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar).ToLowerInvariant();
}
