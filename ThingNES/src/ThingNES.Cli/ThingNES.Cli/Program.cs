using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ThingNES;

bool busConflicts = !args.Contains("--no-bus-conflicts", StringComparer.Ordinal);
args = args.Where(arg => arg != "--no-bus-conflicts").ToArray();
if (args.Length > 0 && args[0] == "--diagnostic") return await DiagnosticCommand.RunAsync(args, busConflicts);

if (args.Length == 1 && args[0] is "--help" or "-h")
{
    Console.WriteLine("ThingNES NTSC runner (mappers 0/2/3) (functional video/audio including DMC).\nUsage: dotnet run --project src/ThingNES.Cli/ThingNES.Cli -- <rom.nes> [cpu-cycles] [frame.ppm|-] [audio.wav]\nDiagnostic mode: --diagnostic <rom.nes|https-url> [max-cpu-cycles]\nOptional board override (both modes): --no-bus-conflicts");
    return 0;
}
if (args.Length is < 1 or > 4)
{
    Console.Error.WriteLine("Expected <rom.nes> [cpu-cycles] [frame.ppm|-] [audio.wav]. Use --help."); return 1;
}
int cycles = 29781;
if (args.Length >= 2 && (!int.TryParse(args[1], out cycles) || cycles <= 0))
{
    Console.Error.WriteLine("cpu-cycles must be a positive integer."); return 1;
}
try
{
    var builder = Host.CreateApplicationBuilder();
    builder.Services.AddSingleton(new NesMachine(Cartridge.Load(File.ReadAllBytes(args[0]), busConflicts)));
    using var host = builder.Build();
    var machine = host.Services.GetRequiredService<NesMachine>();
    using var audioStream = args.Length == 4 ? File.Create(args[3]) : null;
    using var wave = audioStream is null ? null : new WaveWriter(audioStream);
    if (wave is not null) machine.Apu.SampleProduced += wave.WriteSample;
    machine.RunCycles(cycles);
    if (args.Length >= 3 && args[2] != "-")
    {
        using var output = File.Create(args[2]);
        FrameImage.WritePpm(output, machine.Ppu.Framebuffer.Span);
    }
    host.Services.GetRequiredService<ILogger<NesMachine>>().LogInformation(
        "{Mapper}: {Prg} bytes PRG, {Cycles} CPU cycles, {Frames} frames; PPU {Line}:{Dot}. Functional video/audio; approximate DMA timing.",
        machine.Cartridge.MapperName, machine.Cartridge.PrgSize, machine.CpuCycles, machine.Ppu.Frames, machine.Ppu.Scanline, machine.Ppu.Dot);
    return 0;
}
catch (Exception ex) when (ex is IOException or NotSupportedException or UnauthorizedAccessException or ArgumentException)
{
    Console.Error.WriteLine(ex.Message); return 1;
}
