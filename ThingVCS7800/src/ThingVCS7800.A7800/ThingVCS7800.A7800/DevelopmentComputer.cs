using ThingMOS;
using ThingVCS;
namespace ThingVCS7800.A7800;

/// <summary>Single-owner host for the development board. Not yet a full Atari computer.</summary>
public sealed class DevelopmentComputer
{
    public const int ClockHz = 1789773;
    public Board Board { get; } = new();
    public bool HasCartridge => !Board.CartridgeSocket.IsEmpty;
    public bool Paused { get; set; } = true;
    double credit;

    public DevelopmentComputer()
    {
        Board.MariaSocket.Insert(new Maria());
        Board.RiotSocket.Insert(new Riot6532());
        Board.TiaInputSocket.Insert(new TiaInputs());
        Board.CpuSocket.Insert(new Sally());
    }
    public void Insert(ReadOnlySpan<byte> rom)
    {
        // Validate before disconnecting the existing cartridge.
        var cartridge = new LinearCartridge(rom);
        if (!Board.HardwareBoot) Board.SelectDevelopmentMode(false);
        Board.CartridgeSocket.Remove(); Board.CartridgeSocket.Insert(cartridge);
        Board.Video.DetectedSystem = ThingRetroEmu.Video.VideoSystem.Ntsc;
        Reset(); Paused = false;
    }
    public void InstallBios(ReadOnlySpan<byte> firmware)
    {
        var bios = new BootRom(firmware);
        Board.BiosSocket.Remove(); Board.BiosSocket.Insert(bios);
        Board.HardwareBoot = true; Reset();
    }
    public void RemoveBios()
    {
        Board.BiosSocket.Remove(); Board.SelectDevelopmentMode(false); Reset();
    }
    public void Insert2600(ReadOnlySpan<byte> rom)
    {
        var cartridge = new Vcs2600LinearCartridge(rom);
        Board.CartridgeSocket.Remove(); Board.CartridgeSocket.Insert(cartridge);
        Board.SelectDevelopmentMode(true); credit = 0; Paused = false;
    }
    public string InsertImage(ReadOnlySpan<byte> bytes, bool a78, bool allowLegacy0502 = false, bool allowUnknownControllers = false, bool allowAbsolute0300 = false, bool allowUnknownSavePeripheral = false, bool allowMirroredRam0006 = false, bool allowKnownRamCorrection = false)
    {
        var image = CartridgeImage.Parse(bytes, a78, allowLegacy0502, allowUnknownControllers, allowAbsolute0300, allowUnknownSavePeripheral, allowMirroredRam0006, allowKnownRamCorrection);
        var cartridge = image.CreateCartridge();
        if (!Board.HardwareBoot) Board.SelectDevelopmentMode(false);
        Board.CartridgeSocket.Remove(); Board.CartridgeSocket.Insert(cartridge);
        Reset(); Paused = false;
        Board.Video.DetectedSystem = image.TvSystem;
        Reset();
        return image.Title;
    }
    public void Eject()
    {
        Paused = true; credit = 0; Board.CartridgeSocket.Remove();
    }
    public void Reset() { credit = 0; Board.Reset(); }
    public int Advance(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        if (Paused || !HasCartridge) { credit = 0; return 0; }
        // Bound catch-up to 50 ms: a stalled UI must not freeze while executing seconds of CPU work.
        credit += Math.Min(elapsed.TotalSeconds, .05) * (Board.Timing.MasterClockHz / 4.0);
        int cycles = 0;
        while (credit >= 1)
        {
            long before = Board.MasterClocks;
            Board.Tick(); cycles++;
            // Credit is in nominal four-master-clock slots. Slow accesses consume 1.5 slots.
            credit -= (Board.MasterClocks - before) / 4.0;
        }
        return cycles;
    }
}
