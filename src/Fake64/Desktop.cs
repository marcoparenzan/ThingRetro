using System.Diagnostics;

namespace Fake64.Cli;

// The desktop app (Fake64.Studio.exe: studio and C64 windows), started as a separate process so that the terminal
// is free again. In the tool package it is in the "desktop" folder; while developing, in the build output of
// src/Fake64.Studio next to this project's
static class Desktop
{
    const string ExecutableName = "Fake64.Studio.exe";

    public static string? Find()
    {
        string packaged = Path.Combine(AppContext.BaseDirectory, "desktop", ExecutableName);
        if (File.Exists(packaged)) return packaged;

        // src/Fake64/bin/<Configuration>/net10.0/ -> src/Fake64.Studio/bin/<Configuration>/net10.0-windows/
        var output = new DirectoryInfo(AppContext.BaseDirectory);
        string configuration = output.Parent?.Name ?? "Debug";
        for (var folder = output; folder is not null; folder = folder.Parent)
        {
            var studioBin = Path.Combine(folder.FullName, "Fake64.Studio", "bin");
            foreach (var candidate in new[] { configuration, "Debug", "Release" })
            {
                string path = Path.Combine(studioBin, candidate, "net10.0-windows", ExecutableName);
                if (File.Exists(path)) return path;
            }
        }
        return null;
    }

    public static int Start(params string[] arguments)
    {
        string? executable = Find();
        if (executable is null)
        {
            Spectre.Console.AnsiConsole.MarkupLine("[red]The desktop app (Fake64.Studio) was not found: build src/Fake64.Studio first.[/]");
            return 1;
        }
        var start = new ProcessStartInfo(executable) { UseShellExecute = false };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        Process.Start(start);
        return 0;
    }
}
