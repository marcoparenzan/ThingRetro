using Thing65.Cli;
using Spectre.Console.Cli;

// thing65: the command line of the project (a .NET global tool). run starts the desktop app
//   thing65 run [FILE]             a C64 in a window, running a .prg/.crt (or a source, built first)
//   thing65 asm SOURCE [-o FILE]   assembles (ACME syntax or the project's own)
//   thing65 basic SOURCE [-o FILE] tokenizes a BASIC V2 program
//   thing65 build [MANIFEST]       builds every output of a manifest (e.g. samples/samples.json)
var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("thing65");
    config.AddCommand<RunCommand>("run").WithDescription("Runs a program or a cartridge in a C64 window");
    config.AddCommand<AsmCommand>("asm").WithDescription("Assembles a 6502 source into a .prg");
    config.AddCommand<BasicCommand>("basic").WithDescription("Tokenizes a BASIC V2 program into a .prg");
    config.AddCommand<BuildCommand>("build").WithDescription("Builds the outputs listed in a build manifest");
    config.AddExample("run", "samples/rasterbars.prg");
    config.AddExample("asm", "main.asm", "--labels", "main.labels");
    config.AddExample("build", "samples/samples.json");
});
return app.Run(args);
