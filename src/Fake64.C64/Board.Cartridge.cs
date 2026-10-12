namespace Fake64.C64;

// Porta di espansione: inserire o togliere una cartuccia equivale a farlo a macchina spenta,
// quindi segue un reset (il KERNAL cerca la firma "CBM80" a $8004 all'avvio).
// Per caricare un file .crt vedere Fake64.Crt.CrtFile
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
