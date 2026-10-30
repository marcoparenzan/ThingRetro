using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ThingNES.Computer;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddTransient<ComputerForm>();
        using var host = builder.Build();
        using var window = host.Services.GetRequiredService<ComputerForm>();
        if (args.Length > 0) window.Shown += (_, _) => window.LoadRom(args[0]);
        Application.Run(window);
    }
}
