using System.Reflection;

namespace Fake64.Samples;

// Un esempio: il file da caricare nell'emulatore e i sorgenti da cui nasce (in samples/src)
public sealed record Sample(string FileName, string Description, byte[] Content, IReadOnlyList<string> Sources);

// I programmi di esempio di samples/. I sorgenti (.bas e .asm commentati) stanno in samples/src e sono inclusi
// in questo progetto come risorse: generatore e test leggono gli stessi file
public static class SampleSet
{
    public static IReadOnlyList<Sample> All { get; } =
    [
        // BASIC
        Basic("hello.prg", "BASIC e colori del testo", "hello.bas"),
        Basic("math.prg", "Espressioni in BASIC", "math.bas"),
        Basic("sprite.prg", "Uno sprite espanso che rimbalza", "sprite.bas"),
        Basic("keys.prg", "Tasti funzione e tasti cursore", "keys.bas"),
        Basic("joystick.prg", "Joystick nella porta 2", "joystick.bas"),
        Basic("paddle.prg", "Paddle nella porta 1", "paddle.bas"),

        // Linguaggio macchina: interrupt e CPU
        Program("sys.prg", "Linguaggio macchina a $C000, avviato con SYS 49152", "sys.asm"),
        Program("rasterbars.prg", "Raster interrupt del VIC-II: una barra nel bordo", "rasterbars.asm"),
        Program("irq.prg", "Interrupt del timer della CIA 1 agganciato al KERNAL", "irq.asm"),
        Program("nmi.prg", "NMI dal timer della CIA 2", "nmi.asm"),
        Program("decimal.prg", "Modo decimale (BCD): un contatore a 4 cifre", "decimal.asm"),

        // Linguaggio macchina: VIC-II
        Program("multicolor.prg", "Testo multicolor con un set di caratteri in RAM", "multicolor.asm"),
        Program("ecm.prg", "Extended color mode: quattro colori di sfondo", "ecm.asm"),
        Program("bitmap.prg", "Bitmap ad alta risoluzione", "bitmap.asm"),
        Program("mcbitmap.prg", "Bitmap multicolor", "mcbitmap.asm"),
        Program("sprites.prg", "Otto sprite: espansione, multicolor, priorità, collisioni", "sprites.asm"),
        Program("scroll.prg", "Scroll fine orizzontale di una riga", "scroll.asm"),
        Program("vicbank.prg", "Banco del VIC-II e caratteri copiati dalla ROM", "vicbank.asm"),

        // Cartucce
        Cartridge("hello.crt", "Cartuccia 8K con avvio automatico", "HELLO CART", CrtImage.TypeNormal,
            exromActive: true, gameActive: false, (0, "cartridge.asm")),
        Cartridge("ultimax.crt", "Cartuccia Ultimax: un C64 senza KERNAL", "ULTIMAX", CrtImage.TypeNormal,
            exromActive: false, gameActive: true, (0, "ultimax.asm")),
        Cartridge("banks.crt", "Cartuccia Magic Desk con due banchi", "BANKS", CrtImage.TypeMagicDesk,
            exromActive: true, gameActive: false, (0, "banks0.asm"), (1, "banks1.asm")),
    ];

    public static string Source(string fileName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("samples/" + fileName)
            ?? throw new FileNotFoundException($"Sorgente non trovato: samples/src/{fileName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    static Sample Basic(string fileName, string description, string source) =>
        new(fileName, description, BasicTokenizer.Tokenize(Source(source)), [source]);

    static Sample Program(string fileName, string description, string source) =>
        new(fileName, description, Assembler.Assemble(Source(source), source).Prg, [source]);

    // Ogni banco è un sorgente assemblato a $8000 (ROML) o a $E000 (ROMH, Ultimax), riempito fino a 8 KB
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
