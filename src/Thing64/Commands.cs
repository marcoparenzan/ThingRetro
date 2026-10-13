using Thing64.Build;
using System.ComponentModel;
using System.Globalization;
using Thing64.Asm;
using Thing64.Basic;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Thing64.Cli;

// thing64 studio [PATH]: the studio, with a source, a program or a folder open
sealed class StudioCommand : Command<StudioCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[PATH]")]
        [Description("A source (.asm), a program (.prg, .crt) or a folder to open")]
        public string? Path { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken) =>
        settings.Path is null ? Desktop.Start() : Desktop.Start(System.IO.Path.GetFullPath(settings.Path));
}

// thing64 run FILE: one C64 in a window, running a program or a cartridge; sources are built first
sealed class RunCommand : Command<RunCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[FILE]")]
        [Description("A .prg or .crt to run, or a .asm/.bas source to build and run; nothing for a plain C64")]
        public string? File { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        string? file = settings.File is null ? null : Path.GetFullPath(settings.File);
        if (file is not null && Path.GetExtension(file).ToLowerInvariant() is not (".prg" or ".crt"))
        {
            // A source: built into a temporary .prg
            var program = Commands.TryBuild(file);
            if (program is null) return 1;
            file = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(file) + ".prg");
            File.WriteAllBytes(file, program);
        }
        return file is null ? Desktop.Start("--computer") : Desktop.Start("--computer", file);
    }
}

// thing64 asm SOURCE [-o OUTPUT] [--labels FILE] [--plain]
sealed class AsmCommand : Command<AsmCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<SOURCE>")]
        [Description("The main source file (ACME syntax or the project's own); !source files are found next to it")]
        public string Source { get; init; } = "";

        [CommandOption("-o|--output <FILE>")]
        [Description("Output file (default: the source name with .prg)")]
        public string? Output { get; init; }

        [CommandOption("--labels <FILE>")]
        [Description("Also writes the labels in VICE format (load them in the VICE monitor with ll)")]
        public string? Labels { get; init; }

        [CommandOption("--plain")]
        [Description("No load address in front of the bytes (for ROM images)")]
        public bool Plain { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        Assembler.Result result;
        try
        {
            result = ProgramBuilder.Assemble(settings.Source);
        }
        catch (AssemblerException e)
        {
            Commands.ReportError(e);
            return 1;
        }
        catch (IOException e)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{e.Message}[/]");
            return 1;
        }

        string output = settings.Output ?? Path.ChangeExtension(settings.Source, ".prg");
        File.WriteAllBytes(output, settings.Plain ? result.Bytes : result.Prg);
        int end = result.Origin + result.Bytes.Length - 1;
        AnsiConsole.MarkupLineInterpolated($"[green]{Path.GetFileName(output)}[/]: ${result.Origin:X4}-${end:X4}, {result.Bytes.Length} bytes");

        if (settings.Labels is not null)
        {
            File.WriteAllLines(settings.Labels, result.Symbols.OrderBy(s => s.Value).Select(s =>
                string.Create(CultureInfo.InvariantCulture, $"al C:{s.Value & 0xFFFF:X4} .{s.Key}")));
            AnsiConsole.MarkupLineInterpolated($"[green]{Path.GetFileName(settings.Labels)}[/]: {result.Symbols.Count} labels");
        }
        return 0;
    }
}

// thing64 basic SOURCE [-o OUTPUT]
sealed class BasicCommand : Command<BasicCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<SOURCE>")]
        [Description("A BASIC V2 program in text, one numbered line per line")]
        public string Source { get; init; } = "";

        [CommandOption("-o|--output <FILE>")]
        [Description("Output file (default: the source name with .prg)")]
        public string? Output { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        byte[] program;
        try
        {
            program = BasicTokenizer.Tokenize(File.ReadAllText(settings.Source));
        }
        catch (Exception e) when (e is FormatException or IOException)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{settings.Source}: {e.Message}[/]");
            return 1;
        }
        string output = settings.Output ?? Path.ChangeExtension(settings.Source, ".prg");
        File.WriteAllBytes(output, program);
        AnsiConsole.MarkupLineInterpolated($"[green]{Path.GetFileName(output)}[/]: {program.Length} bytes");
        return 0;
    }
}

// thing64 build [MANIFEST]: every output of a build manifest (see BuildManifest)
sealed class BuildCommand : Command<BuildCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[MANIFEST]")]
        [Description("The build manifest (default: thing64.json in the current folder)")]
        public string Manifest { get; init; } = "thing64.json";
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        BuildManifest manifest;
        try
        {
            manifest = BuildManifest.Load(settings.Manifest);
        }
        catch (Exception e) when (e is IOException or System.Text.Json.JsonException or InvalidDataException)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{settings.Manifest}: {e.Message}[/]");
            return 1;
        }

        var table = new Table().Border(TableBorder.Simple)
            .AddColumn("File").AddColumn(new TableColumn("Bytes").RightAligned()).AddColumn("Sources").AddColumn("Description");
        foreach (string asset in manifest.AssetFiles())
        {
            try
            {
                var result = manifest.BuildAsset(asset);
                foreach (string file in result.Files)
                    table.AddRow(Markup.Escape(file), "", Markup.Escape(Path.GetFileName(asset)), Markup.Escape(result.Conversion.Summary));
            }
            catch (Exception e) when (e is IOException or InvalidDataException or NotSupportedException or System.Text.Json.JsonException)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]{asset}: {e.Message}[/]");
                return 1;
            }
        }
        foreach (var output in manifest.Outputs)
        {
            byte[] bytes;
            try
            {
                bytes = manifest.Build(output);
            }
            catch (AssemblerException e)
            {
                Commands.ReportError(e);
                return 1;
            }
            catch (Exception e) when (e is FormatException or IOException or NotSupportedException or InvalidDataException or ArgumentException)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]{output.File}: {e.Message}[/]");
                return 1;
            }
            File.WriteAllBytes(manifest.PathOf(output.File), bytes);
            table.AddRow(Markup.Escape(output.File), bytes.Length.ToString(CultureInfo.InvariantCulture),
                Markup.Escape(string.Join(" + ", output.Sources.Select(Path.GetFileName))), Markup.Escape(output.Description ?? ""));
        }
        AnsiConsole.Write(table);
        return 0;
    }
}

static class Commands
{
    // As compilers do, "file(line): error: reason", so that editors and terminals can jump to the line
    public static void ReportError(AssemblerException e)
    {
        AnsiConsole.MarkupLineInterpolated($"[red]{e.FilePath ?? e.SourceName}({e.Line}): error: {e.Reason}[/]");
    }

    public static byte[]? TryBuild(string source)
    {
        try
        {
            return ProgramBuilder.Build(source);
        }
        catch (AssemblerException e)
        {
            ReportError(e);
        }
        catch (Exception e) when (e is FormatException or IOException or NotSupportedException)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{source}: {e.Message}[/]");
        }
        return null;
    }
}
