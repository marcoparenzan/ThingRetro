namespace Fake64.C64;

// Expansion port: inserting or removing a cartridge is like doing it with the machine off,
// so a reset follows (the KERNAL looks for the "CBM80" signature at $8004 at power on).
// To load a .crt file see Fake64.Crt.CrtFile
public partial class Board
{
    Cartridge? cartridge;

    public Cartridge? Cartridge => cartridge;

    public void InsertCartridge(Cartridge newCartridge)
    {
        if (cartridge is not null) cartridge.LinesChanged = null;
        cartridge = newCartridge;
        cartridge.LinesChanged = MemoryMap;
        Reset();
    }

    public void RemoveCartridge()
    {
        if (cartridge is null) return;
        cartridge.LinesChanged = null;
        cartridge = null;
        Reset();
    }
}
