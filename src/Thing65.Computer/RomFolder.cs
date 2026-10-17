using Thing65.C65;

namespace Thing65.Computer;

// The C65 system ROM lives in the app's roms folder, next to the executable, and the board only receives its
// contents. The ROM is not distributed with Thing65 (it has a personal-use license): everybody installs their own,
// see "The system ROM" in the README. Any 128 KB .rom file in the folder will do; c-65-19910828.rom is the one
// that is tested
public static class RomFolder
{
    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "roms");

    // Names tried first, in this order, then any other .rom of the right size
    static readonly string[] PreferredNames =
        ["c65.rom", "c-65-19910828.rom", "c-65-19910626.rom", "c-65-19910523.rom", "c-65-19910429-de.rom", "c-65-19910111.rom"];

    public static SystemRom128 Load(string? folder = null)
    {
        folder ??= DefaultPath;
        var candidates = PreferredNames.Select(name => Path.Combine(folder, name)).Where(File.Exists)
            .Concat(Directory.Exists(folder)
                ? Directory.EnumerateFiles(folder, "*.rom").Where(path => new FileInfo(path).Length == SystemRom128.SIZE)
                : []);
        string? file = candidates.FirstOrDefault();
        if (file is null)
            throw new FileNotFoundException(
                $"The C65 system ROM was not found in {folder}.\n\n" +
                "Install C64 Forever Free Express Edition (Cloanto, free for personal use), copy the file c-65-19910828.rom " +
                "from its Shared\\rom folder into the folder above, and start again.");
        return new SystemRom128(File.ReadAllBytes(file));
    }
}
