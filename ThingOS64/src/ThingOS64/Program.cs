using ThingMOS.Asm;
using ThingOS64;

// Builds the ThingOS ROMs and writes them where a C64 emulator reads them
//   ThingOS64 [output folder]      default: ThingOS64/build/roms
try
{
    string romFolder = OsRoms.FindRomFolder();
    string output = args.Length > 0 ? args[0] : Path.Combine(Path.GetDirectoryName(romFolder)!, "build", "roms");
    var roms = OsRoms.Build(romFolder);
    roms.Save(output);
    Console.WriteLine($"ThingOS ROMs written to {Path.GetFullPath(output)}");
    Console.WriteLine($"  upper ROM (basic)  ${0xA000:X4}-${roms.Symbols["upper_end"] - 1:X4}, {0xC000 - roms.Symbols["upper_end"]} bytes free");
    Console.WriteLine($"  lower ROM (kernal) ${0xE000:X4}-${roms.Symbols["lower_end"] - 1:X4}, {roms.Symbols["JUMP_TABLE"] - roms.Symbols["lower_end"]} bytes free before the jump table");
    return 0;
}
catch (Exception e) when (e is AssemblerException or IOException or InvalidOperationException)
{
    Console.Error.WriteLine(e.Message);
    return 1;
}
