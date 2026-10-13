using Fake64.Asm;
using System.Reflection;

namespace Fake64.Samples;

// A sample: the file to load into the emulator and the sources it is built from (in samples/src)
public sealed record Sample(string FileName, string Description, byte[] Content, IReadOnlyList<string> Sources);

// The sample programs of samples/. The sources (commented .bas and .asm) live in samples/src and are included
// in this project as resources: the generator and the tests read the same files
public static class SampleSet
{
    public static IReadOnlyList<Sample> All { get; } =
    [
        // BASIC
        Basic("hello.prg", "BASIC and text colors", "hello.bas"),
        Basic("math.prg", "Expressions in BASIC", "math.bas"),
        Basic("sprite.prg", "A bouncing expanded sprite", "sprite.bas"),
        Basic("keys.prg", "Function keys and cursor keys", "keys.bas"),
        Basic("joystick.prg", "Joystick in port 2", "joystick.bas"),
        Basic("paddle.prg", "Paddles in port 1", "paddle.bas"),
        Basic("sound.prg", "A scale played by the SID", "sound.bas"),

        // Machine language: interrupts and CPU
        Program("sys.prg", "Machine language at $C000, started with SYS 49152", "sys.asm"),
        Program("rasterbars.prg", "VIC-II raster interrupt: a bar in the border", "rasterbars.asm"),
        Program("irq.prg", "CIA 1 timer interrupt hooked into the KERNAL", "irq.asm"),
        Program("nmi.prg", "NMI from the CIA 2 timer", "nmi.asm"),
        Program("decimal.prg", "Decimal mode (BCD): a 4-digit counter", "decimal.asm"),

        // Machine language: SID
        Program("music.prg", "Three SID voices played by the interrupt: PWM, filter, noise", "music.asm"),

        // Machine language: VIC-II
        Program("multicolor.prg", "Multicolor text with a character set in RAM", "multicolor.asm"),
        Program("ecm.prg", "Extended color mode: four background colors", "ecm.asm"),
        Program("bitmap.prg", "High-resolution bitmap", "bitmap.asm"),
        Program("mcbitmap.prg", "Bitmap multicolor", "mcbitmap.asm"),
        Program("sprites.prg", "Eight sprites: expansion, multicolor, priority, collisions", "sprites.asm"),
        Program("scroll.prg", "Horizontal fine scroll of one line", "scroll.asm"),
        Program("vicbank.prg", "VIC-II bank and characters copied from ROM", "vicbank.asm"),
        Program("borders.prg", "Top and bottom borders open, with sprites inside", "borders.asm"),

        // Cartridges
        Cartridge("hello.crt", "8K cartridge with autostart", "HELLO CART", CrtImage.TypeNormal,
            exromActive: true, gameActive: false, (0, "cartridge.asm")),
        Cartridge("ultimax.crt", "Ultimax cartridge: a C64 without KERNAL", "ULTIMAX", CrtImage.TypeNormal,
            exromActive: false, gameActive: true, (0, "ultimax.asm")),
        Cartridge("banks.crt", "Magic Desk cartridge with two banks", "BANKS", CrtImage.TypeMagicDesk,
            exromActive: true, gameActive: false, (0, "banks0.asm"), (1, "banks1.asm")),
    ];

    public static string Source(string fileName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("samples/" + fileName)
            ?? throw new FileNotFoundException($"Source not found: samples/src/{fileName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    static Sample Basic(string fileName, string description, string source) =>
        new(fileName, description, BasicTokenizer.Tokenize(Source(source)), [source]);

    static Sample Program(string fileName, string description, string source) =>
        new(fileName, description, Assembler.Assemble(Source(source), source).Prg, [source]);

    // Each bank is a source assembled at $8000 (ROML) or at $E000 (ROMH, Ultimax), filled up to 8 KB
    static Sample Cartridge(string fileName, string description, string name, int type, bool exromActive, bool gameActive,
        params (int Bank, string Source)[] banks)
    {
        var chips = banks.Select(bank =>
        {
            var code = Assembler.Assemble(Source(bank.Source), bank.Source);
            return (bank.Bank, code.Origin, CrtImage.Rom8K(code.Bytes));
        }).ToArray();
        return new(fileName, description, CrtImage.Build(name, type, exromActive, gameActive, chips), banks.Select(bank => bank.Source).ToList());
    }
}
