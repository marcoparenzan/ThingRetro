namespace ThingVCS;

/// <summary>Logical console contacts, not a numbered physical connector.</summary>
public sealed class ConsoleSwitches
{
    public bool ResetPressed { get; set; }
    public bool SelectPressed { get; set; }
    public bool PausePressed { get; set; }
    public bool LeftDifficultyA { get; set; }
    public bool RightDifficultyA { get; set; }

    public void ReleaseButtons() => ResetPressed = SelectPressed = PausePressed = false;

    /// <summary>7800 SWCHB: active-low buttons on PB0/1/3; difficulty A drives PB6/7 high.
    /// Unconnected contacts remain high. PB2 is not a 2600 color switch here.</summary>
    public byte Read7800Contacts()
    {
        int value = 0x3f;
        if (ResetPressed) value &= ~1;
        if (SelectPressed) value &= ~2;
        if (PausePressed) value &= ~8;
        if (LeftDifficultyA) value |= 0x40;
        if (RightDifficultyA) value |= 0x80;
        return (byte)value;
    }
}
