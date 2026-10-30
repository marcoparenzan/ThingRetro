namespace ThingVCS;

public interface ITiaAudioPinout
{
    double AudioLevel { get; }
    void TickAudio(); // One board master clock, independent of CPU HALT.
}

/// <summary>Functional two-channel TIA audio. Polynomial phases and analogue output are approximate.</summary>
public sealed class TiaAudio
{
    sealed class Channel
    {
        public int Control, Frequency, Volume, Counter, Divide31, Divide3;
        public int Poly4 = 15, Poly5 = 31, Poly9 = 511;
        public bool High;
        public void Reset()
        {
            Control = Frequency = Volume = Counter = Divide31 = Divide3 = 0;
            Poly4 = 15; Poly5 = 31; Poly9 = 511; High = false;
        }
        static int Shift(int value, int tap, int bits) =>
            (value >> 1) | (((value ^ (value >> tap)) & 1) << (bits - 1));
        public void Clock()
        {
            if (Control is 0 or 11) { High = true; return; }
            // Five-bit divider compares rather than truncates: lowering AUDF below
            // an in-flight count wraps at 31 before matching the new value.
            bool enabled = Counter == Frequency;
            Counter = enabled || Counter == 31 ? 0 : Counter + 1;
            if (!enabled) return;
            if (Control >= 12 && ++Divide3 < 3) return;
            Divide3 = 0;
            Poly5 = Shift(Poly5, 2, 5);
            Divide31 = (Divide31 + 1) % 31;
            bool gate = (Control & 2) == 0 || ((Control & 1) == 0
                ? Divide31 is 0 or 18 : (Poly5 & 1) != 0);
            if (!gate) return;
            if ((Control & 4) != 0) High = !High;
            else if ((Control & 8) != 0)
            {
                if (Control == 8) { Poly9 = Shift(Poly9, 4, 9); High = (Poly9 & 1) != 0; }
                else High = (Poly5 & 1) != 0;
            }
            else { Poly4 = Shift(Poly4, 1, 4); High = (Poly4 & 1) != 0; }
        }
    }
    readonly Channel[] channels = [new(), new()];
    int masterDivider;
    public long AudioClocks { get; private set; }
    // 114 TIA colour clocks at master/2 (~31.4 kHz NTSC); not locked to MARIA scanlines.
    // Uniform spacing approximates the internal two-phase audio clock.
    public const int MasterClocksPerAudioClock = 228;
    public double AudioLevel => channels.Sum(c => (c.Control is 0 or 11 || c.High) ? c.Volume / 30.0 : 0);
    public byte RegisterValue(int register) => register switch
    {
        0x15 or 0x16 => (byte)channels[register - 0x15].Control,
        0x17 or 0x18 => (byte)channels[register - 0x17].Frequency,
        0x19 or 0x1a => (byte)channels[register - 0x19].Volume,
        _ => throw new ArgumentOutOfRangeException(nameof(register))
    };
    public void WriteRegister(int register, byte value)
    {
        switch (register)
        {
            case 0x15: case 0x16: channels[register - 0x15].Control = value & 15; break;
            case 0x17: case 0x18: channels[register - 0x17].Frequency = value & 31; break;
            case 0x19: case 0x1a: channels[register - 0x19].Volume = value & 15; break;
        }
    }
    public void Tick()
    {
        if (++masterDivider < MasterClocksPerAudioClock) return;
        masterDivider = 0; AudioClocks++;
        foreach (var channel in channels) channel.Clock();
    }
    public void Reset() { masterDivider = 0; AudioClocks = 0; foreach (var channel in channels) channel.Reset(); }
}
