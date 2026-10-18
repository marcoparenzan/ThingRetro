namespace ThingCommodore.Cartridges;

// Device connected to the expansion port: the electrical contract the Board knows.
// ROML appears at $8000-$9FFF, ROMH at $A000-$BFFF (16K mode) or at $E000-$FFFF (Ultimax).
// The concrete types and the mapping of the .crt format are in this namespace; the container of the format is ThingRetroEmu.Media.CrtImage
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

    // Expansion port lines, active low: true = high (inactive)
    public bool Exrom { get; private set; }
    public bool Game { get; private set; }

    readonly bool initialExrom;
    readonly bool initialGame;

    // Tells the Board to recompute the memory map
    public Action? LinesChanged;

    protected void SetLines(bool exrom, bool game)
    {
        if (exrom == Exrom && game == Game) return;
        Exrom = exrom;
        Game = game;
        LinesChanged?.Invoke();
    }

    // State at power on or after the reset button
    public virtual void Reset()
    {
        SetLines(initialExrom, initialGame);
    }

    // offset: 0-$1FFF within the 8 KB window
    public abstract byte ReadRoml(int offset);
    public abstract byte ReadRomh(int offset);

    // I/O 1 ($DE00-$DEFF) and I/O 2 ($DF00-$DFFF); null = no device answers (open bus)
    public virtual byte? ReadIo1(int offset) => null;
    public virtual byte? ReadIo2(int offset) => null;
    public virtual void WriteIo1(int offset, byte value) { }
    public virtual void WriteIo2(int offset, byte value) { }
}
