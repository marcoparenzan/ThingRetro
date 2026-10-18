using Thing64.C64;

namespace Thing64.Computer;

// The C64 ROMs live in the app's roms folder (files kernal, basic, chargen) and are copied next to the
// executable; the Board only receives their contents
public static class RomFolder
{
    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "roms");

    public static SystemRoms Load(string? folder = null)
    {
        folder ??= DefaultPath;
        return new SystemRoms(
            kernal: File.ReadAllBytes(Path.Combine(folder, "kernal")),
            basic: File.ReadAllBytes(Path.Combine(folder, "basic")),
            chargen: File.ReadAllBytes(Path.Combine(folder, "chargen")));
    }
}
