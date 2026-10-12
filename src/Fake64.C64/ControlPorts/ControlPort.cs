namespace Fake64.C64;

// Ciò che un dispositivo presenta sui pin della porta di controllo (presa DE-9, standard "Atari"):
//   1 JOY0 (su)   2 JOY1 (giù)   3 JOY2 (sinistra)   4 JOY3 (destra)   6 fuoco (e penna ottica sulla porta 1)
//   5 POT Y       9 POT X        7 +5V             8 massa
// I contatti digitali chiusi tirano la linea a massa; le linee POT portano una resistenza che il SID misura
public interface IControlPortDevice
{
    // Pin 1, 2, 3, 4, 6 nei bit 0-4 (0 = chiuso a massa); i bit 5-7 non sono collegati e valgono 1
    byte DigitalLines { get; }

    // Pin 9 e 5 come li misura il SID (0-255); $FF = nessuna resistenza collegata
    byte PotX { get; }
    byte PotY { get; }
}

// Presa della porta di controllo sulla scheda: un dispositivo alla volta (joystick, paddle, mouse...).
// Senza nulla collegato i contatti sono aperti e le linee POT libere
public sealed class ControlPort
{
    volatile IControlPortDevice? device;

    internal ControlPort(string name)
    {
        Name = name;
    }

    public string Name { get; }
    public IControlPortDevice? Device => device;
    public bool IsEmpty => device is null;

    public void Plug(IControlPortDevice newDevice)
    {
        ArgumentNullException.ThrowIfNull(newDevice);
        if (device is not null)
            throw new InvalidOperationException($"Alla {Name} è già collegato un dispositivo: scollegarlo prima");
        device = newDevice;
    }

    public IControlPortDevice? Unplug()
    {
        var unplugged = device;
        device = null;
        return unplugged;
    }

    // Livelli sui pin visti dalla scheda
    internal byte DigitalLines => device?.DigitalLines ?? 0xFF;
    internal byte PotX => device?.PotX ?? 0xFF;
    internal byte PotY => device?.PotY ?? 0xFF;

    public override string ToString() => $"{Name}: {(device is null ? "vuota" : device.GetType().Name)}";
}
