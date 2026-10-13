using Fake64.Cli;
using Spectre.Console.Cli;

// fake64: the command line of the project (a .NET global tool). studio and run start the desktop app
//   fake64 studio [PATH]          the studio
//   fake64 run [FILE]             a C64 in a window, running a .prg/.crt (or a source, built first)
//   fake64 asm SOURCE [-o FILE]   assembles (ACME syntax or the project's own)
//   fake64 basic SOURCE [-o FILE] tokenizes a BASIC V2 program
//   fake64 build [MANIFEST]       builds every output of a manifest (e.g. samples/samples.json)
var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("fake64");
    config.AddCommand<StudioCommand>("studio").WithDescription("Opens the studio: file explorer, assembly editor, C64 tabs");
    config.AddCommand<RunCommand>("run").WithDescription("Runs a program or a cartridge in a C64 window");
    config.AddCommand<AsmCommand>("asm").WithDescription("Assembles a 6502 source into a .prg");
    config.AddCommand<BasicCommand>("basic").WithDescription("Tokenizes a BASIC V2 program into a .prg");
    config.AddCommand<BuildCommand>("build").WithDescription("Builds the outputs listed in a build manifest");
    config.AddExample("studio", "samples/src");
    config.AddExample("run", "samples/rasterbars.prg");
    config.AddExample("asm", "main.asm", "--labels", "main.labels");
    config.AddExample("build", "samples/samples.json");
});
return app.Run(args);
