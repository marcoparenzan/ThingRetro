namespace Fake64.Mos;

// MOS 6510: un 6502 con una porta di I/O integrata (P0-P5) ai registri $0000 (direzione) e $0001 (dati).
// Sul C64 P0-P2 decidono la mappa di memoria (LORAM, HIRAM, CHAREN) e P3-P5 vanno al registratore
public class MOS6510 : MOS6502, I6510Pinout
{
    public ParallelPort Port { get; } = new();

    // Al reset la porta è tutta in ingresso: i pin valgono quanto imposto dall'esterno (pull-up).
    // Il Changed della porta permette alla scheda di aggiornare la mappa di memoria prima del vettore
    protected override void OnReset() => Port.Reset();

    // Registri della porta come li vede la CPU: $00 = direzione, $01 = livello dei pin
    public byte ReadPort(ushort addr) => addr == 0 ? Port.Direction : Port.Pins;

    public void WritePort(ushort addr, byte value)
    {
        if (addr == 0) Port.Direction = value;
        else Port.Latch = value;
    }

    // L'indirizzo va comunque sul bus: in lettura il dato del bus viene ignorato, in scrittura arriva
    // anche al bus (sul C64, nella RAM sottostante)
    protected override byte ReadByte(ushort addr)
    {
        byte value = base.ReadByte(addr);
        return addr <= 0x0001 ? ReadPort(addr) : value;
    }

    protected override void WriteByte(ushort addr, byte val)
    {
        if (addr <= 0x0001) WritePort(addr, val);
        base.WriteByte(addr, val);
    }
}
