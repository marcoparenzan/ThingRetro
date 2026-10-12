namespace Fake64.Mos;

// Porta parallela a 8 bit di un chip (PA/PB della 6526, P0-P5 della 6510).
// Ogni bit è un'uscita se il bit corrispondente di Direction è 1, altrimenti un ingresso.
// Le uscite NMOS hanno un pull-up debole: un dispositivo esterno può sempre tirare un pin a massa.
// Il livello di un pin è quindi l'uscita (o il pull-up, se è un ingresso) in AND con ciò che
// impone l'esterno (External, 0 = tirato a massa): così un joystick si legge anche su una porta in uscita
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

    // Linee tirate a massa dall'esterno (bit a 0); lo imposta la scheda che collega il chip
    public Func<byte> External { get; set; } = () => 0xFF;

    // Bit pilotati dal chip (le uscite), senza leggere i pin esterni
    public byte Output => (byte)(latch & direction);

    // Livello effettivo dei pin: uscite dal latch, ingressi a 1 (pull-up), il tutto in AND con l'esterno
    public byte Pins => (byte)(((latch & direction) | ~direction) & External());

    // Cambio di Latch o Direction: chi è collegato alle uscite può reagire (es. il banco del VIC-II)
    public event Action? Changed;

    public void Reset()
    {
        latch = 0;
        direction = 0;
        Changed?.Invoke();
    }
}
