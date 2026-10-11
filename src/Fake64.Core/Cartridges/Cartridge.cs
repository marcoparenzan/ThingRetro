namespace Fake64.Cartridges;

// Cartuccia collegata alla porta di espansione. Le ROM sono divise in banchi da 8 KB:
// ROML compare a $8000-$9FFF, ROMH a $A000-$BFFF (modo 16K) o a $E000-$FFFF (Ultimax)
public abstract class Cartridge
{
    public const int BANK_SIZE = 0x2000;

    protected Cartridge(string name, bool exrom, bool game)
    {
        Name = name;
        initialExrom = exrom;
        initialGame = game;
        Exrom = exrom;
        Game = game;
    }

    public string Name { get; }
    public abstract string TypeName { get; }

    // Linee della porta di espansione, attive basse: true = alta (inattiva)
    public bool Exrom { get; private set; }
    public bool Game { get; private set; }

    readonly bool initialExrom;
    readonly bool initialGame;

    // Avvisa la Board che deve ricalcolare la mappa di memoria
    internal Action? LinesChanged;

    protected void SetLines(bool exrom, bool game)
    {
        if (exrom == Exrom && game == Game) return;
        Exrom = exrom;
        Game = game;
        LinesChanged?.Invoke();
    }

    // Stato all'accensione o dopo il pulsante di reset
    public virtual void Reset()
    {
        SetLines(initialExrom, initialGame);
    }

    // offset: 0-$1FFF all'interno della finestra da 8 KB
    public abstract byte ReadRoml(int offset);
    public abstract byte ReadRomh(int offset);

    // I/O 1 ($DE00-$DEFF) e I/O 2 ($DF00-$DFFF); null = nessun dispositivo risponde (bus aperto)
    public virtual byte? ReadIo1(int offset) => null;
    public virtual byte? ReadIo2(int offset) => null;
    public virtual void WriteIo1(int offset, byte value) { }
    public virtual void WriteIo2(int offset, byte value) { }

    // Banco di ROM da 8 KB non presente nel file: legge $FF come una EPROM vuota
    protected static byte[] EmptyBank()
    {
        var bank = new byte[BANK_SIZE];
        Array.Fill(bank, (byte)0xFF);
        return bank;
    }
}
