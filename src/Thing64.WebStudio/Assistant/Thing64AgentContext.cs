using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.AI;
using RalfAI.Contexts;
using Thing64.Asm;

namespace Thing64.WebStudio.Assistant;

// What the studio does when the assistant asks: run a program in the C64, and know which files changed
public interface IStudioHost
{
    Task RunAsync(string path);
    Task FilesChangedAsync(IReadOnlyCollection<string> paths);
}

// RalfAI in the studio: a C64 programmer that works on one project. Its tools are the studio's own: the files of the
// project (always through Project, so never outside the folder), the build with errors by file and line, the
// assembler's reference, and the C64 to run the result
public sealed class Thing64AgentContext(Project project, IStudioHost host) : IAgentContext
{
    public string Name => "thing64";
    public string Description => "Commodore 64 programming in a Thing64 project: 6502 assembly (ACME syntax), BASIC V2, builds and runs";

    public string BuildSystemPrompt(string targetFolder) => $"""
        You are RalfAI, working inside Thing64 Studio with the user on a Commodore 64 (PAL) project: "{project.Name}".
        The project is a folder; every path is relative to it, with '/' as separator.

        ## The machine
        - CPU 6510 (a 6502 with an I/O port at $00/$01), about 985248 cycles per second, 312 raster lines, 63 cycles
          per line. VIC-II at $D000 (border $D020, background $D021, raster $D012, sprites $D000-$D02E), SID at
          $D400, CIA 1 at $DC00 (keyboard, joystick port 2 in $DC00), CIA 2 at $DD00 (VIC-II bank), color RAM at $D800,
          screen at $0400 by default. KERNAL: CHROUT $FFD2, GETIN $FFE4, PLOT $FFF0; IRQ vector $0314.
        - A program that starts with RUN begins at $0801 with a BASIC line "10 SYS 2061" (or similar) before the code.

        ## The assembler (Thing64.Asm, compatible with ACME 0.97)
        - Pseudo-ops with '!' (ACME: !byte !word !text !fill !source !binary !for !if !macro, +macro calls) or with '.'
          (.byte .word .text .fill ...). Labels with or without ':'; local labels .name, cheap @name, anonymous - and +.
        - "* = $0801" sets the address; "NAME = value" defines a constant; ';' starts a comment.
        - Directives: {string.Join(" ", Assembler.Directives)}.

        ## The project
        - thing64.json is the build manifest: "outputs" with "file" and "source" (or "cartridge" with banks). F5 in
          the studio builds and runs the first output. Without a manifest the open source is built by itself.
        - Built files (.prg, .crt) are outputs: never edit them, rebuild them.

        ## How to work
        - Read the files you are about to change, and keep their style: comments explain the C64, in English.
        - After changing sources, call Project_Build. If it fails, read the errors (file(line): reason), fix them and
          build again, until it succeeds. Then say what you changed.
        - Call Project_Run when the user wants to see the result, or when running it is the natural next step.
        - Answer in the user's language. Be concise; show code only when it helps.
        """;

    public IEnumerable<AITool> GetTools(string targetFolder) =>
    [
        AIFunctionFactory.Create(ListFiles, "Files_List"),
        AIFunctionFactory.Create(ReadFile, "Files_Read"),
        AIFunctionFactory.Create(WriteFile, "Files_Write"),
        AIFunctionFactory.Create(DeleteFile, "Files_Delete"),
        AIFunctionFactory.Create(Build, "Project_Build"),
        AIFunctionFactory.Create(Run, "Project_Run"),
        AIFunctionFactory.Create(Instruction, "Asm_Instruction"),
    ];

    [Description("Lists the files of the project (or of one of its folders), one relative path per line.")]
    string ListFiles([Description("Folder relative to the project; empty for all")] string folder = "")
    {
        var lines = new StringBuilder();
        void Add(ProjectEntry entry)
        {
            foreach (var child in entry.Children)
            {
                if (child.IsFolder) Add(child); else lines.AppendLine(child.Path);
            }
        }
        var tree = project.Tree();
        foreach (var part in folder.Split('/', StringSplitOptions.RemoveEmptyEntries))
            tree = tree.Children.FirstOrDefault(c => c.IsFolder && c.Name.Equals(part, StringComparison.OrdinalIgnoreCase))
                ?? throw new DirectoryNotFoundException($"No folder {folder}");
        Add(tree);
        return lines.Length == 0 ? "(no files)" : lines.ToString();
    }

    [Description("Reads a text file of the project.")]
    string ReadFile([Description("Path relative to the project")] string path) => project.ReadText(path);

    [Description("Creates or replaces a text file of the project with the whole content given.")]
    async Task<string> WriteFile([Description("Path relative to the project")] string path, [Description("The whole new content")] string content)
    {
        string full = project.FullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllTextAsync(full, content);
        await host.FilesChangedAsync([project.RelativePath(full)]);
        return $"Written {path} ({content.Length} characters)";
    }

    [Description("Deletes a file of the project.")]
    async Task<string> DeleteFile([Description("Path relative to the project")] string path)
    {
        project.Delete(path);
        await host.FilesChangedAsync([path]);
        return $"Deleted {path}";
    }

    [Description("Builds the project (the outputs of thing64.json). Returns 'succeeded' or 'failed' with the errors as file(line): reason.")]
    async Task<string> Build()
    {
        var report = project.Build(activeFile: null);
        await host.FilesChangedAsync([]);
        return (report.Succeeded ? "Build succeeded" : "Build failed") + Environment.NewLine
            + string.Join(Environment.NewLine, report.Messages.Select(m => m.Text));
    }

    [Description("Builds the project and runs its first output in the C64 of the studio, which the user sees.")]
    async Task<string> Run()
    {
        var report = project.Build(activeFile: null);
        await host.FilesChangedAsync([]);
        if (!report.Succeeded || report.Run is null)
            return "Build failed" + Environment.NewLine + string.Join(Environment.NewLine, report.Messages.Select(m => m.Text));
        await host.RunAsync(report.Run);
        return $"Running {report.Run} in the C64";
    }

    [Description("What a 6502 instruction does and which flags it changes.")]
    static string Instruction([Description("The mnemonic, for example LDA")] string mnemonic) =>
        InstructionSet.Describe(mnemonic) ?? $"{mnemonic} is not a 6502 instruction";
}
