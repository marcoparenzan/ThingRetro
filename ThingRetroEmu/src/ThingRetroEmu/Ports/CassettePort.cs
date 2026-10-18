namespace ThingRetroEmu.Ports;

// The cassette connector of a computer (the datasette port of the C64): what the computer drives and what it senses.
// The computer fills the outputs from its chip (the CPU port bits for motor and write, and the CIA's FLAG for read); a
// recorder plugs into the other end and does not know which computer is on the other side
public sealed class CassettePort
{
    // What the computer drives
    public Func<bool> MotorOn { get; set; } = () => false;       // motor relay (P5 low on the C64)
    public Func<bool> WriteHigh { get; set; } = () => true;      // WRITE line (P3)

    // What the recorder drives; the computer reads them. Unconnected: no key pressed, line idle high
    public Func<bool> SenseLow { get; set; } = () => false;      // SENSE: low while a key (PLAY, RECORD, F.FWD, REWIND) is down
    public Func<bool> ReadHigh { get; set; } = () => true;       // READ: pulses to the CIA's FLAG, a falling edge for each pulse
}
