namespace Fake64.C64;

// Zoccolo della scheda madre. Il contratto è la piedinatura (TPinout): qualunque oggetto che la
// implementa entra nello zoccolo. Inserendo un chip la scheda ne collega i pin alle piste
// (bus, linee di interrupt, porte); togliendolo le piste restano scollegate, come uno zoccolo vuoto.
// Dopo aver cambiato un chip conviene un Board.Reset(), come riaccendere la macchina
public sealed class Socket<TPinout> where TPinout : class
{
    readonly Action<TPinout> connect;
    readonly Action<TPinout> disconnect;

    internal Socket(string name, Action<TPinout> connect, Action<TPinout> disconnect)
    {
        Name = name;
        this.connect = connect;
        this.disconnect = disconnect;
    }

    public string Name { get; }
    public TPinout? Chip { get; private set; }
    public bool IsEmpty => Chip is null;

    public void Insert(TPinout chip)
    {
        ArgumentNullException.ThrowIfNull(chip);
        if (Chip is not null)
            throw new InvalidOperationException($"Lo zoccolo {Name} è già occupato: togliere prima il chip presente");
        Chip = chip;
        connect(chip);
    }

    public TPinout? Remove()
    {
        var chip = Chip;
        if (chip is null) return null;
        disconnect(chip);
        Chip = null;
        return chip;
    }

    public override string ToString() => $"{Name}: {(Chip is null ? "vuoto" : Chip.GetType().Name)}";
}
