using ThingMOS.Asm;

namespace ThingOS64;

// The three ROMs of ThingOS, built from the sources in the rom folder:
//   thingos.asm  assembled once from $A000 to $FFFF: $A000-$BFFF is the BASIC socket, $E000-$FFFF the KERNAL one
//   font.asm     128 characters; the other 128 are their reverse, and the second half of the CHARGEN repeats the first
public sealed record OsRoms(byte[] Kernal, byte[] Basic, byte[] Chargen, IReadOnlyDictionary<string, int> Symbols)
{
    public const int ROM_SIZE = 0x2000;
    public const int CHARGEN_SIZE = 0x1000;
    const int UPPER = 0xA000, LOWER = 0xE000;

    public static OsRoms Build(string romFolder)
    {
        var system = Assemble(romFolder, "thingos.asm");
        if (system.Origin != UPPER || system.Origin + system.Bytes.Length != 0x10000)
            throw new InvalidOperationException($"thingos.asm must fill ${UPPER:X4}-$FFFF, not ${system.Origin:X4}-${system.Origin + system.Bytes.Length - 1:X4}");
        var basic = system.Bytes[..ROM_SIZE];
        var kernal = system.Bytes[(LOWER - UPPER)..];

        var font = Assemble(romFolder, "font.asm").Bytes;
        if (font.Length != 128 * 8)
            throw new InvalidOperationException($"font.asm must define 128 characters, not {font.Length / 8.0}");
        var chargen = new byte[CHARGEN_SIZE];
        for (int i = 0; i < font.Length; i++)
        {
            chargen[i] = font[i];
            chargen[font.Length + i] = (byte)~font[i];
        }
        Array.Copy(chargen, 0, chargen, CHARGEN_SIZE / 2, CHARGEN_SIZE / 2);

        return new OsRoms(kernal, basic, chargen, system.Symbols);
    }

    // The files the C64 emulators read (kernal, basic, chargen)
    public void Save(string folder)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "kernal"), Kernal);
        File.WriteAllBytes(Path.Combine(folder, "basic"), Basic);
        File.WriteAllBytes(Path.Combine(folder, "chargen"), Chargen);
    }

    static Assembler.Result Assemble(string folder, string file) =>
        Assembler.Assemble(File.ReadAllText(Path.Combine(folder, file)), file, folder);

    // The rom folder of ThingOS64, looking up from a folder (by default the program's)
    public static string FindRomFolder(string? start = null)
    {
        for (var folder = new DirectoryInfo(start ?? AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            foreach (var candidate in new[] { Path.Combine(folder.FullName, "rom"), Path.Combine(folder.FullName, "ThingOS64", "rom") })
                if (File.Exists(Path.Combine(candidate, "thingos.asm"))) return candidate;
        }
        throw new DirectoryNotFoundException("ThingOS64/rom not found above " + (start ?? AppContext.BaseDirectory));
    }
}
