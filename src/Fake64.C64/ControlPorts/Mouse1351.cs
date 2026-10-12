namespace Fake64.C64;

// Mouse Commodore 1351 in modo proporzionale (quello usato da GEOS). Il mouse conta il movimento e lo
// presenta sulle linee POT: POT X e POT Y valgono (posizione mod 64) * 2, il bit 0 è un bit di rumore
// che i driver ignorano. Il pulsante sinistro chiude il pin del fuoco, il destro quello di "su".
// Il driver legge la differenza fra due letture successive, quindi conta solo il movimento.
// Non modellati: il modo joystick (pulsante destro premuto all'accensione) e la sincronizzazione
// del mouse con la misura del SID
public sealed class Mouse1351 : IControlPortDevice
{
    const int RightButtonBit = 0x01, LeftButtonBit = 0x10;

    int x, y;
    int buttons;

    // Movimento in "mickey": x verso destra, y verso l'alto (come i contatori del 1351)
    public void Move(int dx, int dy)
    {
        Interlocked.Add(ref x, dx);
        Interlocked.Add(ref y, dy);
    }

    public bool LeftButton { get => (Volatile.Read(ref buttons) & LeftButtonBit) != 0; set => SetButton(LeftButtonBit, value); }
    public bool RightButton { get => (Volatile.Read(ref buttons) & RightButtonBit) != 0; set => SetButton(RightButtonBit, value); }

    public byte DigitalLines => (byte)~Volatile.Read(ref buttons);
    public byte PotX => (byte)((Volatile.Read(ref x) & 0x3F) << 1);
    public byte PotY => (byte)((Volatile.Read(ref y) & 0x3F) << 1);

    void SetButton(int bit, bool pressed)
    {
        if (pressed) Interlocked.Or(ref buttons, bit);
        else Interlocked.And(ref buttons, ~bit);
    }
}
