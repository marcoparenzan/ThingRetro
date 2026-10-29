using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ThingNES.Desktop;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddTransient<MainWindow>();
        using var host = builder.Build();
        using var window = host.Services.GetRequiredService<MainWindow>();
        if (args.Length > 0) window.Shown += (_, _) => window.LoadRom(args[0]);
        Application.Run(window);
    }
}
