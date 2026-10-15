using Thing64.Asm;
using Thing64.Build;

namespace Thing64.WebStudio;

// The projects of the studio: one folder each under the projects folder (Studio:ProjectsFolder in appsettings.json),
// with everything inside: the sources, the build manifest thing64.json, the built .prg and .crt files.
// The folders are ordinary folders: they can be edited, copied and built (thing64 build) without the studio
public sealed class ProjectStore(IConfiguration configuration)
{
    public string Root { get; } = Path.GetFullPath(configuration["Studio:ProjectsFolder"] is { Length: > 0 } folder
        ? folder
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Thing64", "Projects"));

    // Most recently changed first
    public IReadOnlyList<ProjectSummary> List()
    {
        Directory.CreateDirectory(Root);
        return new DirectoryInfo(Root).EnumerateDirectories()
            .Where(d => !d.Name.StartsWith('.'))
            .Select(d => new ProjectSummary(d.Name, LastChange(d), File.Exists(Path.Combine(d.FullName, Project.ManifestName))))
            .OrderByDescending(p => p.Changed)
            .ToList();
    }

    static DateTime LastChange(DirectoryInfo folder) =>
        folder.EnumerateFiles("*", SearchOption.AllDirectories).Select(f => f.LastWriteTime).DefaultIfEmpty(folder.LastWriteTime).Max();

    public Project Open(string name)
    {
        string folder = Path.Combine(Root, Project.ValidName(name));
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException($"There is no project {name}");
        return new Project(name, folder);
    }

    // A new project: main.asm, a small program that starts with RUN, and the manifest that builds it
    public Project Create(string name)
    {
        string folder = Path.Combine(Root, Project.ValidName(name));
        if (Directory.Exists(folder)) throw new IOException($"The project {name} already exists");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "main.asm"), Templates.MainAsm(name));
        File.WriteAllText(Path.Combine(folder, Project.ManifestName), Templates.Manifest(name));
        File.WriteAllText(Path.Combine(folder, Project.StructureName), Templates.Structure(name));
        return new Project(name, folder);
    }
}

public sealed record ProjectSummary(string Name, DateTime Changed, bool HasManifest);

// A file or folder of a project; Path is relative to the project folder, with '/'
public sealed record ProjectEntry(string Name, string Path, bool IsFolder, IReadOnlyList<ProjectEntry> Children);

// One line of the build log; File and Line point to the source of an error, when known
public sealed record BuildMessage(string Text, bool IsError = false, string? File = null, int Line = 0);

// Run: the file to start in the C64 after a successful build
public sealed record BuildReport(bool Succeeded, IReadOnlyList<BuildMessage> Messages, string? Run);

public sealed class Project(string name, string folder)
{
    public const string ManifestName = "thing64.json";
    public const string StructureName = "_structure.md";   // the map of the project, for the user and for RalfAI
    static readonly string[] SourceExtensions = [".asm", ".bas"];

    public string Name { get; } = name;
    public string Folder { get; } = Path.GetFullPath(folder);

    public static string ValidName(string name)
    {
        name = name.Trim();
        if (name.Length == 0 || name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException($"\"{name}\" is not a valid name");
        return name;
    }

    // Every path from the browser goes through here: it must stay inside the project folder
    public string FullPath(string relative)
    {
        string full = Path.GetFullPath(Path.Combine(Folder, relative));
        if (full != Folder && !full.StartsWith(Folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException($"{relative} is outside the project");
        return full;
    }

    public string RelativePath(string full) => Path.GetRelativePath(Folder, full).Replace('\\', '/');

    // Folders first, then files, alphabetically
    public ProjectEntry Tree() => Entry(new DirectoryInfo(Folder));

    ProjectEntry Entry(DirectoryInfo directory) => new(directory.Name, RelativePath(directory.FullName), true,
        directory.EnumerateDirectories().Where(d => !d.Name.StartsWith('.')).OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).Select(Entry)
            .Concat(directory.EnumerateFiles().OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .Select(f => new ProjectEntry(f.Name, RelativePath(f.FullName), false, [])))
            .ToList());

    public string ReadText(string path) => File.ReadAllText(FullPath(path));

    public byte[] ReadBytes(string path) => File.ReadAllBytes(FullPath(path));

    public void WriteText(string path, string text) => File.WriteAllText(FullPath(path), text);

    // In: the folder (relative) where to create it; returns the new path
    public string CreateFile(string inFolder, string fileName)
    {
        string full = FullPath(Path.Combine(inFolder, ValidName(fileName)));
        if (File.Exists(full) || Directory.Exists(full)) throw new IOException($"{fileName} already exists");
        File.WriteAllText(full, Path.GetExtension(full).Equals(".asm", StringComparison.OrdinalIgnoreCase) ? Templates.EmptyAsm(fileName) : "");
        return RelativePath(full);
    }

    public string CreateFolder(string inFolder, string folderName)
    {
        string full = FullPath(Path.Combine(inFolder, ValidName(folderName)));
        if (File.Exists(full) || Directory.Exists(full)) throw new IOException($"{folderName} already exists");
        Directory.CreateDirectory(full);
        return RelativePath(full);
    }

    public string Rename(string path, string newName)
    {
        string from = FullPath(path);
        string to = FullPath(Path.Combine(Path.GetDirectoryName(path) ?? "", ValidName(newName)));
        if (from == Folder) throw new InvalidOperationException("The project folder cannot be renamed here");
        if (Directory.Exists(from)) Directory.Move(from, to); else File.Move(from, to);
        return RelativePath(to);
    }

    public void Delete(string path)
    {
        string full = FullPath(path);
        if (full == Folder) throw new InvalidOperationException("The project folder cannot be deleted here");
        if (Directory.Exists(full)) Directory.Delete(full, recursive: true); else File.Delete(full);
    }

    // With thing64.json: every output of the manifest, and the first one runs.
    // Without: the active source (.asm or .bas) becomes the .prg next to it
    public BuildReport Build(string? activeFile)
    {
        var messages = new List<BuildMessage>();
        string manifestPath = Path.Combine(Folder, ManifestName);
        try
        {
            // The graphics assets first: the sources include what they generate
            var assets = File.Exists(manifestPath) ? BuildManifest.Load(manifestPath).AssetFiles() : BuildManifest.FindAssets(Folder);
            foreach (string asset in assets)
                if (!BuildAsset(asset, messages)) return new BuildReport(false, messages, null);

            if (File.Exists(manifestPath))
            {
                var manifest = BuildManifest.Load(manifestPath);
                if (manifest.Outputs.Count == 0) throw new InvalidDataException($"{ManifestName} has no outputs");
                foreach (var output in manifest.Outputs)
                {
                    if (!BuildOne(output.File, output.Sources.ToList(), () => manifest.Build(output), messages))
                        return new BuildReport(false, messages, null);
                }
                return new BuildReport(true, messages, RelativePath(manifest.PathOf(manifest.Outputs[0].File)));
            }

            if (activeFile is null || !SourceExtensions.Contains(Path.GetExtension(activeFile).ToLowerInvariant()))
                throw new InvalidDataException($"Open a .asm or .bas file to build it, or add a {ManifestName} to the project");
            string prg = Path.ChangeExtension(activeFile, ".prg");
            bool built = BuildOne(prg, [activeFile], () => ProgramBuilder.Build(FullPath(activeFile)), messages);
            return new BuildReport(built, messages, built ? prg : null);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            messages.Add(new BuildMessage(e.Message, IsError: true));
            return new BuildReport(false, messages, null);
        }
    }

    bool BuildAsset(string asset, List<BuildMessage> messages)
    {
        try
        {
            var result = Thing64.Graphics.GraphicsBuilder.Build(FullPath(asset));
            messages.Add(new BuildMessage($"{asset}: {result.Conversion.Summary} -> {string.Join(", ", result.Files)}"));
            return true;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or NotSupportedException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            messages.Add(new BuildMessage($"{asset}: {e.Message}", IsError: true));
            return false;
        }
    }

    bool BuildOne(string file, IReadOnlyList<string> sources, Func<byte[]> build, List<BuildMessage> messages)
    {
        try
        {
            byte[] bytes = build();
            File.WriteAllBytes(FullPath(file), bytes);
            messages.Add(new BuildMessage($"{file}: {bytes.Length} bytes from {string.Join(", ", sources)}"));
            return true;
        }
        catch (AssemblerException e)
        {
            // FilePath is known for included files; otherwise it is the source (or cartridge bank) with that name
            string? source = e.FilePath is not null ? RelativePath(e.FilePath)
                : sources.FirstOrDefault(s => Path.GetFileName(s).Equals(e.SourceName, StringComparison.OrdinalIgnoreCase));
            messages.Add(new BuildMessage($"{source ?? e.SourceName}({e.Line}): {e.Reason}", true, source, e.Line));
        }
        catch (Exception e) when (e is FormatException or OverflowException or NotSupportedException or InvalidOperationException
                                       or ArgumentException or IOException or InvalidDataException)
        {
            messages.Add(new BuildMessage($"{file}: {e.Message}", IsError: true, sources.Count == 1 ? sources[0] : null));
        }
        return false;
    }
}
