namespace Fake64.C64;

// Contenuto delle tre ROM della scheda madre. Il core non sa da dove arrivano (file, risorse, rete):
// chi crea la Board le legge e le passa al costruttore
public sealed record SystemRoms
{
    public const int KERNAL_SIZE = 0x2000;
    public const int BASIC_SIZE = 0x2000;
    public const int CHARGEN_SIZE = 0x1000;

    public SystemRoms(byte[] kernal, byte[] basic, byte[] chargen)
    {
        Kernal = Check(kernal, KERNAL_SIZE, nameof(kernal));
        Basic = Check(basic, BASIC_SIZE, nameof(basic));
        Chargen = Check(chargen, CHARGEN_SIZE, nameof(chargen));
    }

    public byte[] Kernal { get; }   // $E000-$FFFF
    public byte[] Basic { get; }    // $A000-$BFFF
    public byte[] Chargen { get; }  // $D000-$DFFF (CPU) e $1000/$9000 (VIC-II)

    static byte[] Check(byte[] rom, int size, string name)
    {
        ArgumentNullException.ThrowIfNull(rom, name);
        if (rom.Length != size)
            throw new ArgumentException($"La ROM {name} deve essere di {size} byte, non {rom.Length}", name);
        return rom;
    }
}
