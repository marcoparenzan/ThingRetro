using Fake64.C64;

namespace Fake64.Crt;

// Cartuccia con banchi ROML/ROMH selezionabili; i tipi concreti decidono come si cambia banco
public abstract class BankedCartridge : Cartridge
{
    public const int BANK_SIZE = 0x2000;

    protected readonly byte[][] roml;
    protected readonly byte[][] romh;
    protected int bank;

    protected BankedCartridge(string name, bool exrom, bool game, byte[][] roml, byte[][] romh)
        : base(name, exrom, game)
    {
        this.roml = roml;
        this.romh = romh;
    }

    public int Bank => bank;

    public override void Reset()
    {
        bank = 0;
        base.Reset();
    }

    public override byte ReadRoml(int offset) => roml[bank % roml.Length][offset];
    public override byte ReadRomh(int offset) => romh[bank % romh.Length][offset];
}

// Tipo 0: ROM fissa da 8 KB, 16 KB o Ultimax; il modo è dato dalle linee nell'intestazione del .crt
public sealed class NormalCartridge(string name, bool exrom, bool game, byte[][] roml, byte[][] romh)
    : BankedCartridge(name, exrom, game, roml, romh)
{
    public override string TypeName =>
        (Exrom, Game) switch { (false, true) => "Normal 8K", (false, false) => "Normal 16K", _ => "Ultimax" };
}

// Tipo 5: Ocean. Scrivendo a $DE00 si sceglie il banco (bit 0-5); ROML e ROMH mostrano lo stesso banco,
// come in VICE, così funzionano sia i file con tutti i banchi a $8000 sia quelli con metà banchi a $A000
public sealed class OceanCartridge(string name, bool exrom, bool game, byte[][] banks)
    : BankedCartridge(name, exrom, game, banks, banks)
{
    public override string TypeName => "Ocean";

    public override void WriteIo1(int offset, byte value) => bank = value & 0x3F;
}

// Tipo 15: C64 Game System / System 3. Il banco è il numero del registro scritto: $DE00 + n
public sealed class System3Cartridge(string name, byte[][] banks)
    : BankedCartridge(name, exrom: false, game: true, banks, banks)
{
    public override string TypeName => "C64 Game System / System 3";

    public override void WriteIo1(int offset, byte value) => bank = offset & 0x3F;
}

// Tipo 19: Magic Desk / Domark / HES Australia. $DE00: bit 0-6 banco, bit 7 = 1 scollega la cartuccia
public sealed class MagicDeskCartridge(string name, byte[][] banks)
    : BankedCartridge(name, exrom: false, game: true, banks, banks)
{
    public override string TypeName => "Magic Desk";

    public override void WriteIo1(int offset, byte value)
    {
        bank = value & 0x7F;
        SetLines(exrom: (value & 0x80) != 0, game: true);
    }
}

// Tipo 32: EasyFlash, in sola lettura (le scritture nella flash vengono ignorate).
// $DE00: banco (0-63). $DE02: bit 0 GAME, bit 1 EXROM, bit 2 GAME da registro invece che dal jumper.
// Con il jumper in posizione "boot" la cartuccia parte in Ultimax. $DF00-$DFFF: 256 byte di RAM
public sealed class EasyFlashCartridge(string name, byte[][] roml, byte[][] romh)
    : BankedCartridge(name, exrom: true, game: false, roml, romh)
{
    readonly byte[] ram = new byte[256];

    public override string TypeName => "EasyFlash";

    public override void Reset()
    {
        base.Reset();
        WriteControl(0);
    }

    public override void WriteIo1(int offset, byte value)
    {
        if ((offset & 0x02) == 0) bank = value & 0x3F;
        else WriteControl(value);
    }

    void WriteControl(byte value)
    {
        bool exromActive = (value & 0x02) != 0;
        bool gameActive = (value & 0x04) != 0 ? (value & 0x01) != 0 : true; // jumper in posizione "boot"
        SetLines(exrom: !exromActive, game: !gameActive);
    }

    public override byte? ReadIo2(int offset) => ram[offset];
    public override void WriteIo2(int offset, byte value) => ram[offset] = value;
}
