namespace Fake64.Mos;

// Porta parallela a 8 bit di un chip (PA/PB della 6526, P0-P5 della 6510).
// Ogni bit è un'uscita se il bit corrispondente di Direction è 1, altrimenti un ingresso.
// I bit in uscita valgono quanto scritto in Latch; quelli in ingresso leggono ciò che c'è sul filo,
// fornito da chi è collegato al pin (External), con i pull-up a 1 se non c'è nulla
public sealed class ParallelPort
{
    byte latch;
    byte direction;

    public byte Latch
    {
        get => latch;
        set { latch = value; Changed?.Invoke(); }
    }

    public byte Direction
    {
        get => direction;
        set { direction = value; Changed?.Invoke(); }
    }

    // Stato dei pin quando il chip non li pilota; lo imposta la scheda che collega il chip
    public Func<byte> External { get; set; } = () => 0xFF;

    // Bit pilotati dal chip (le uscite), senza leggere i pin esterni
    public byte Output => (byte)(latch & direction);

    // Livello effettivo dei pin: uscite dal latch, ingressi dall'esterno
    public byte Pins => (byte)((latch & direction) | (External() & ~direction));

    // Cambio di Latch o Direction: chi è collegato alle uscite può reagire (es. il banco del VIC-II)
    public event Action? Changed;

    public void Reset()
    {
        latch = 0;
        direction = 0;
        Changed?.Invoke();
    }
}
