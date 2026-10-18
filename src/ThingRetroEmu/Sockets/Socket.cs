namespace ThingRetroEmu.Sockets;

// A motherboard socket. The contract is the pinout (TPinout): any object that implements it fits the
// socket. Inserting a chip, the board connects its pins to the traces (bus, interrupt lines, ports);
// removing it leaves the traces unconnected, like an empty socket.
// After changing a chip a Board.Reset() is advisable, like switching the machine off and on
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
            throw new InvalidOperationException($"The {Name} socket is already taken: remove the chip in it first");
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

    public override string ToString() => $"{Name}: {(Chip is null ? "empty" : Chip.GetType().Name)}";
}
