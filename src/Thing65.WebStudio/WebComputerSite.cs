namespace Thing65.WebStudio;

// Where the published web computer is; without it the studio works, but cannot run programs
public sealed record WebComputerSite(string Folder)
{
    public bool Available { get; } = File.Exists(Path.Combine(Folder, "index.html"));
}
