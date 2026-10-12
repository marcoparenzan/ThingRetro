namespace Fake64;

// Dispositivo collegato alla porta di espansione: il contratto elettrico che la Board conosce.
// ROML compare a $8000-$9FFF, ROMH a $A000-$BFFF (modo 16K) o a $E000-$FFFF (Ultimax).
// I tipi concreti e il formato dei file (.crt) stanno fuori dal core, nel progetto Fake64.Crt
public abstract class Cartridge
{
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
}
