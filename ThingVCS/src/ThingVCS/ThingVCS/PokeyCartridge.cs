using ThingRetroEmu.Sockets;
namespace ThingVCS;

public interface IAudioCartridgePinout : ICartridgePinout
{
    void TickAudio();
    double AudioLevel { get; }
}

public interface IMasterClockedAudioCartridge
{
    void TickMasterClock(int boardClockHz);
}

public sealed class PokeyCartridge : IAudioCartridgePinout, IMasterClockedAudioCartridge
{
    readonly ICartridgePinout rom;
    long oscillatorPhase;
    public int OscillatorHz { get; } // Nominal board-independent oscillator, configurable per cartridge.
    public long OscillatorTicks { get; private set; }
    public void TickMasterClock(int boardClockHz)
    {
        if (boardClockHz <= 0) throw new ArgumentOutOfRangeException(nameof(boardClockHz));
        oscillatorPhase += OscillatorHz;
        while (oscillatorPhase >= boardClockHz)
        {
            oscillatorPhase -= boardClockHz; OscillatorTicks++;
            PokeySocket.Chip?.Tick();
        }
    }
    public Socket<IPokeyPinout> PokeySocket { get; } = new("Cartridge POKEY ($4000)", _ => { }, _ => { });
    public PokeyCartridge(ReadOnlySpan<byte> bytes, bool superGame = false, int oscillatorHz = 1789773)
    {
        if (oscillatorHz is <= 0 or > 20000000) throw new ArgumentOutOfRangeException(nameof(oscillatorHz));
        OscillatorHz = oscillatorHz;
        if (!superGame && bytes.Length == 49152) throw new NotSupportedException("48 KiB ROM overlaps POKEY at $4000.");
        rom = superGame ? new SuperGameCartridge(bytes) : new LinearCartridge(bytes);
        PokeySocket.Insert(new Pokey());
    }
    public byte Read(ushort address) => address is >= 0x4000 and <= 0x7fff
        ? PokeySocket.Chip?.Read(address & 15) ?? (byte)0xff : rom.Read(address);
    public void Write(ushort address, byte value)
    {
        if (address is >= 0x4000 and <= 0x7fff) PokeySocket.Chip?.Write(address & 15, value);
        else rom.Write(address, value);
    }
    public void Reset() { rom.Reset(); PokeySocket.Chip?.Reset(); oscillatorPhase = 0; OscillatorTicks = 0; }
    public void TickAudio() => PokeySocket.Chip?.Tick();
    public double AudioLevel => PokeySocket.Chip?.AudioLevel ?? 0;
}
