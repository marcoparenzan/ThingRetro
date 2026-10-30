using ThingRetroEmu.Ports;
namespace ThingVCS;

/// <summary>Atari socket adapter for a common optical device: trigger on pin 1, sensor on pin 6.
/// Functional active-low model; timing and wiring require external game/hardware validation.</summary>
public sealed class AtariLightGun(LightGun device) : IControlPortDevice
{
    public LightGun Device { get; } = device ?? throw new ArgumentNullException(nameof(device));
    public byte DigitalLines => (byte)(0xff & ~(Device.TriggerPressed ? 1 : 0) & ~(Device.LightDetected ? 0x10 : 0));
    public byte PotX => 0xff;
    public byte PotY => 0xff;
}
