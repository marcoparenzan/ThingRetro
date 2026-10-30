namespace ThingVCS;

/// <summary>Partial TIA logical pin contract: INPT4/5, register bus, clock and reset only.</summary>
public interface ITiaInputPinout
{
    Func<bool> Fire1High { get; set; }
    Func<bool> Fire2High { get; set; }
    byte ReadRegister(int register, byte openBus);
    void WriteRegister(int register, byte value);
    void Tick();
    void Reset();
}

/// <summary>Single-button digital inputs and VBLANK D6 latch. Not a complete TIA chip.</summary>
public interface ITiaButtonInputPinout
{
    Func<int, bool> ButtonInputHigh { get; set; } // INPT0-3 voltage comparator inputs.
}

public sealed class TiaInputs : ITiaInputPinout, ITiaAudioPinout, ITiaButtonInputPinout
{
    public Func<int, bool> ButtonInputHigh { get; set; } = _ => false;
    public TiaAudio Audio { get; } = new();
    public double AudioLevel => Audio.AudioLevel;
    public void TickAudio() => Audio.Tick();
    bool latchEnabled, firstLow, secondLow;
    public Func<bool> Fire1High { get; set; } = () => true;
    public Func<bool> Fire2High { get; set; } = () => true;
    public void Tick()
    {
        if (!latchEnabled) { firstLow = secondLow = false; return; }
        firstLow |= !Fire1High(); secondLow |= !Fire2High();
    }
    public byte ReadRegister(int register, byte openBus)
    {
        if (register is >= 8 and <= 11)
            return (byte)((openBus & 0x7f) | (ButtonInputHigh(register - 8) ? 0x80 : 0));
        if (register is not (12 or 13)) return openBus;
        Tick();
        bool high = register == 12 ? Fire1High() && !firstLow : Fire2High() && !secondLow;
        // Only D7 is driven by these input registers; D0-D6 retain the bus level.
        return (byte)((openBus & 0x7f) | (high ? 0x80 : 0));
    }
    public void WriteRegister(int register, byte value)
    {
        Audio.WriteRegister(register, value);
        if (register != 1) return; // VBLANK and audio; video writes remain unimplemented.
        latchEnabled = (value & 0x40) != 0;
        Tick();
    }
    public void Reset() { latchEnabled = firstLow = secondLow = false; Audio.Reset(); }
}
