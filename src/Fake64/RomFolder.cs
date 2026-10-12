using Fake64.C64;

namespace Fake64;

// Le ROM del C64 stanno nella cartella roms dell'app (file kernal, basic, chargen) e vengono
// copiate accanto all'eseguibile; la Board riceve solo il loro contenuto
static class RomFolder
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
