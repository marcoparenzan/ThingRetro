
namespace ThingCommodore.Cartridges;

// Cartridge with selectable ROML/ROMH banks; the concrete types decide how banks are switched
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

// Type 0: fixed 8 KB, 16 KB or Ultimax ROM; the mode comes from the lines in the .crt header
public sealed class NormalCartridge(string name, bool exrom, bool game, byte[][] roml, byte[][] romh)
    : BankedCartridge(name, exrom, game, roml, romh)
{
    public override string TypeName =>
        (Exrom, Game) switch { (false, true) => "Normal 8K", (false, false) => "Normal 16K", _ => "Ultimax" };
}

// Type 5: Ocean. Writing to $DE00 selects the bank (bits 0-5); ROML and ROMH show the same bank, as in VICE,
// so both files with all the banks at $8000 and files with half of the banks at $A000 work
public sealed class OceanCartridge(string name, bool exrom, bool game, byte[][] banks)
    : BankedCartridge(name, exrom, game, banks, banks)
{
    public override string TypeName => "Ocean";

    public override void WriteIo1(int offset, byte value) => bank = value & 0x3F;
}

// Type 15: C64 Game System / System 3. The bank is the number of the register written: $DE00 + n
public sealed class System3Cartridge(string name, byte[][] banks)
    : BankedCartridge(name, exrom: false, game: true, banks, banks)
{
    public override string TypeName => "C64 Game System / System 3";

    public override void WriteIo1(int offset, byte value) => bank = offset & 0x3F;
}

// Type 19: Magic Desk / Domark / HES Australia. $DE00: bits 0-6 bank, bit 7 = 1 disconnects the cartridge
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

// Type 32: EasyFlash, read-only (writes to the flash are ignored).
// $DE00: bank (0-63). $DE02: bit 0 GAME, bit 1 EXROM, bit 2 GAME from the register instead of the jumper.
// With the jumper in the "boot" position the cartridge starts in Ultimax. $DF00-$DFFF: 256 bytes of RAM
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
        bool gameActive = (value & 0x04) != 0 ? (value & 0x01) != 0 : true; // jumper in the "boot" position
        SetLines(exrom: !exromActive, game: !gameActive);
    }

    public override byte? ReadIo2(int offset) => ram[offset];
    public override void WriteIo2(int offset, byte value) => ram[offset] = value;
}
