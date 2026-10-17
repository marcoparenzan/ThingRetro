namespace Thing65.Mos;

// The serial (IEC) connector of a computer: what it pulls down on the bus and what it senses. The computer fills the
// outputs from its chip (the CIA's port), a cable connects the other end to the bus and the drives; the computer does not
// know what is on the other side
public sealed class IecPort
{
    // The lines the computer pulls down (outputs, after the inverting drivers)
    public Func<bool> AtnLow { get; set; } = () => false;
    public Func<bool> ClkLow { get; set; } = () => false;
    public Func<bool> DataLow { get; set; } = () => false;

    // The state of the lines on the cable, own drivers included; unconnected the lines are high
    public Func<bool> ClkInputLow { get; set; } = () => false;
    public Func<bool> DataInputLow { get; set; } = () => false;
}
