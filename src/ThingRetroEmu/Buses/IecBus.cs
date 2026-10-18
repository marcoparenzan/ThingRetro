
namespace ThingRetroEmu.Buses;

// One line of the IEC bus: open collector with a pull-up, so it is low as soon as one of the connected devices pulls it down
public sealed class IecLine
{
    readonly List<Func<bool>> drivers = [];

    public IecLine(string name) => Name = name;

    public string Name { get; }

    // Connects an output to the line; it returns true while the device pulls the line down
    public void Connect(Func<bool> driver) => drivers.Add(driver);

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

// The serial bus of the Commodore computers and drives: ATN, CLK and DATA, shared by all the devices (plus /SRQIN,
// not used, and /RESET, which the computer drives). It is the explicit cable: a computer and each drive connect
// their pins to it; none of them knows the others
public sealed class IecBus
{
    public IecLine Atn { get; } = new("ATN");
    public IecLine Clk { get; } = new("CLK");
    public IecLine Data { get; } = new("DATA");

    // The cable end of a computer: what it pulls down goes on the lines, what is on the lines goes back to its inputs
    public void Attach(IecPort computer) => Attach(
        () => computer.AtnLow(), () => computer.ClkLow(), () => computer.DataLow(),
        input => computer.ClkInputLow = input, input => computer.DataInputLow = input);

    // The same for any computer with a serial connector: its three outputs (true = pulls the line down) and the two places
    // where it takes the state of CLK and DATA from (they are given a function that tells whether the line is down)
    public void Attach(Func<bool> atnLow, Func<bool> clkLow, Func<bool> dataLow,
        Action<Func<bool>> clkInput, Action<Func<bool>> dataInput)
    {
        Atn.Connect(atnLow);
        Clk.Connect(clkLow);
        Data.Connect(dataLow);
        clkInput(() => Clk.IsLow);
        dataInput(() => Data.IsLow);
    }
}
