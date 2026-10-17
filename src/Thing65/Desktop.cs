using System.Diagnostics;

namespace Thing65.Cli;

// The desktop app (Thing65.Computer.exe: a C64 in a window), started as a separate process so that the terminal
// is free again. In the tool package it is in the "desktop" folder; while developing, in the build output of
// src/Thing65.Computer next to this project's
static class Desktop
{
    const string ExecutableName = "Thing65.Computer.exe";

    public static string? Find()
    {
        string packaged = Path.Combine(AppContext.BaseDirectory, "desktop", ExecutableName);
        if (File.Exists(packaged)) return packaged;

        // src/Thing65/bin/<Configuration>/net10.0/ -> src/Thing65.Computer/bin/<Configuration>/net10.0-windows/
        var output = new DirectoryInfo(AppContext.BaseDirectory);
        string configuration = output.Parent?.Name ?? "Debug";
        for (var folder = output; folder is not null; folder = folder.Parent)
        {
            var computerBin = Path.Combine(folder.FullName, "Thing65.Computer", "bin");
            foreach (var candidate in new[] { configuration, "Debug", "Release" })
            {
                string path = Path.Combine(computerBin, candidate, "net10.0-windows", ExecutableName);
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
            Spectre.Console.AnsiConsole.MarkupLine("[red]The desktop app (Thing65.Computer) was not found: build src/Thing65.Computer first.[/]");
            return 1;
        }
        var start = new ProcessStartInfo(executable) { UseShellExecute = false };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        Process.Start(start);
        return 0;
    }
}
