namespace ThingRetroEmu.Sockets;

// Open-collector trace (/IRQ, /NMI): a resistor keeps it high and every connected output
// can only pull it down. It is low (active) if at least one device is pulling it down
public sealed class OpenCollectorLine
{
    readonly Func<bool>[] drivers;

    // drivers: the outputs connected to the trace; each one returns true when it pulls the line down
    public OpenCollectorLine(string name, params Func<bool>[] drivers)
    {
        Name = name;
        this.drivers = drivers;
    }

    public string Name { get; }

    public bool IsLow
    {
        get
        {
            foreach (var driver in drivers)
                if (driver()) return true;
            return false;
        }
    }
}
