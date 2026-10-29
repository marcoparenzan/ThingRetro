namespace ThingNES;

// Functional NTSC APU. Analog filtering and exact frame-register write delays are deferred.
public sealed class Apu
{
    public const int CpuFrequency = 1_789_773;
    public const int SampleRate = 44_100;
    static readonly int[] Lengths = [10,254,20,2,40,4,80,6,160,8,60,10,14,12,26,14,12,16,24,18,48,20,96,22,192,24,72,26,16,28,32,30];
    static readonly int[] NoisePeriods = [4,8,16,32,64,96,128,160,202,254,380,508,762,1016,2034,4068];
    readonly Pulse[] pulses = [new(true), new(false)];
    readonly Envelope noiseEnvelope = new();
    int noiseLength, noiseTimer, noisePeriod = 4, noiseShift = 1;
    bool noiseEnabled, noiseShort;
    int triangleLength, triangleTimer, trianglePeriod, trianglePhase, triangleLinear, triangleReloadValue;
    bool triangleEnabled, triangleControl, triangleReload;
    int frameCycle, sampleAccumulator;
    long cycles;
    bool fiveStep, inhibitIrq;
    bool frameIrq;
    public Dmc Dmc { get; } = new();
    public bool Irq => frameIrq || Dmc.Irq;
    // Delivered synchronously on the emulation thread. No unbounded internal audio buffer.
    public event Action<short>? SampleProduced;

    public byte ReadStatus()
    {
        byte result = (byte)((pulses[0].Length > 0 ? 1 : 0) | (pulses[1].Length > 0 ? 2 : 0) |
            (triangleLength > 0 ? 4 : 0) | (noiseLength > 0 ? 8 : 0) | (Dmc.Active ? 0x10 : 0) | (frameIrq ? 0x40 : 0) | (Dmc.Irq ? 0x80 : 0));
        frameIrq = false;
        return result;
    }
    public void Write(ushort address, byte value)
    {
        if (address is >= 0x4000 and <= 0x4007)
        { pulses[(address - 0x4000) / 4].Write(address & 3, value); return; }
        if (address is >= 0x4010 and <= 0x4013) { Dmc.Write(address - 0x4010, value); return; }
        switch (address)
        {
            case 0x4008: triangleControl = (value & 0x80) != 0; triangleReloadValue = value & 127; break;
            case 0x400a: trianglePeriod = (trianglePeriod & 0x700) | value; break;
            case 0x400b:
                trianglePeriod = (trianglePeriod & 255) | ((value & 7) << 8);
                if (triangleEnabled) triangleLength = Lengths[value >> 3];
                triangleReload = true; break;
            case 0x400c: noiseEnvelope.Write(value); break;
            case 0x400e: noisePeriod = NoisePeriods[value & 15]; noiseShort = (value & 0x80) != 0; break;
            case 0x400f: if (noiseEnabled) noiseLength = Lengths[value >> 3]; noiseEnvelope.Start = true; break;
            case 0x4015:
                pulses[0].SetEnabled((value & 1) != 0); pulses[1].SetEnabled((value & 2) != 0);
                triangleEnabled = (value & 4) != 0; if (!triangleEnabled) triangleLength = 0;
                noiseEnabled = (value & 8) != 0; if (!noiseEnabled) noiseLength = 0;
                Dmc.SetEnabled((value & 16) != 0);
                break;
            case 0x4017:
                fiveStep = (value & 0x80) != 0; inhibitIrq = (value & 0x40) != 0;
                if (inhibitIrq) frameIrq = false;
                frameCycle = 0;
                if (fiveStep) { QuarterFrame(); HalfFrame(); }
                break;
        }
    }
    public void Tick()
    {
        cycles++;
        Dmc.Tick();
        if ((cycles & 1) == 0) { pulses[0].Tick(); pulses[1].Tick(); }
        if (triangleTimer == 0)
        {
            triangleTimer = trianglePeriod;
            if (triangleLength > 0 && triangleLinear > 0 && trianglePeriod > 1) trianglePhase = (trianglePhase + 1) & 31;
        }
        else triangleTimer--;
        if (noiseTimer == 0)
        {
            noiseTimer = noisePeriod - 1;
            int feedback = (noiseShift ^ (noiseShift >> (noiseShort ? 6 : 1))) & 1;
            noiseShift = (noiseShift >> 1) | (feedback << 14);
        }
        else noiseTimer--;
        frameCycle++;
        if (frameCycle is 7457 or 22371) QuarterFrame();
        if (frameCycle == 14913) { QuarterFrame(); HalfFrame(); }
        if (!fiveStep && frameCycle == 29829)
        { QuarterFrame(); HalfFrame(); if (!inhibitIrq) frameIrq = true; }
        if (fiveStep && frameCycle == 37281) { QuarterFrame(); HalfFrame(); }
        if (frameCycle >= (fiveStep ? 37282 : 29830)) frameCycle = 0;
        sampleAccumulator += SampleRate;
        if (sampleAccumulator >= CpuFrequency)
        { sampleAccumulator -= CpuFrequency; SampleProduced?.Invoke(Mix()); }
    }
    void QuarterFrame()
    {
        foreach (var pulse in pulses) pulse.Envelope.Clock();
        noiseEnvelope.Clock();
        if (triangleReload) triangleLinear = triangleReloadValue;
        else if (triangleLinear > 0) triangleLinear--;
        if (!triangleControl) triangleReload = false;
    }
    void HalfFrame()
    {
        foreach (var pulse in pulses) pulse.HalfFrame();
        if (!triangleControl && triangleLength > 0) triangleLength--;
        if (!noiseEnvelope.Loop && noiseLength > 0) noiseLength--;
    }
    short Mix()
    {
        int p = pulses[0].Output + pulses[1].Output;
        int t = triangleLength > 0 && triangleLinear > 0 && trianglePeriod > 1 ? (trianglePhase < 16 ? 15 - trianglePhase : trianglePhase - 16) : 0;
        int n = noiseLength > 0 && (noiseShift & 1) == 0 ? noiseEnvelope.Output : 0;
        double pulse = p == 0 ? 0 : 95.88 / (8128.0 / p + 100);
        double input = t / 8227.0 + n / 12241.0 + Dmc.Output / 22638.0;
        double tnd = input == 0 ? 0 : 159.79 / (1 / input + 100);
        return (short)Math.Clamp((pulse + tnd) * short.MaxValue, 0, short.MaxValue);
    }
    sealed class Envelope
    {
        int volume, divider, decay;
        bool constant;
        public bool Loop { get; private set; }
        public bool Start { get; set; }
        public int Output => constant ? volume : decay;
        public void Write(byte v) { volume = v & 15; constant = (v & 16) != 0; Loop = (v & 32) != 0; }
        public void Clock()
        {
            if (Start) { Start = false; decay = 15; divider = volume; }
            else if (divider > 0) divider--;
            else { divider = volume; if (decay > 0) decay--; else if (Loop) decay = 15; }
        }
    }
    sealed class Pulse(bool first)
    {
        static readonly byte[] Duties = [0x02, 0x06, 0x1e, 0xf9];
        int period, timer, phase, duty;
        bool enabled, sweepEnabled, sweepNegate, sweepReload;
        int sweepPeriod, sweepShift, sweepDivider;
        public int Length { get; private set; }
        public Envelope Envelope { get; } = new();
        int Target => period + (sweepNegate ? -(period >> sweepShift) - (first ? 1 : 0) : period >> sweepShift);
        public int Output => Length == 0 || period < 8 || (!sweepNegate && Target > 0x7ff) || ((Duties[duty] >> phase) & 1) == 0 ? 0 : Envelope.Output;
        public void SetEnabled(bool value) { enabled = value; if (!value) Length = 0; }
        public void Write(int register, byte value)
        {
            switch (register)
            {
                case 0: duty = value >> 6; Envelope.Write(value); break;
                case 1: sweepEnabled = (value & 0x80) != 0; sweepPeriod = (value >> 4) & 7; sweepNegate = (value & 8) != 0; sweepShift = value & 7; sweepReload = true; break;
                case 2: period = (period & 0x700) | value; break;
                case 3: period = (period & 255) | ((value & 7) << 8); if (enabled) Length = Lengths[value >> 3]; phase = 0; Envelope.Start = true; break;
            }
        }
        public void Tick() { if (timer == 0) { timer = period; phase = (phase + 1) & 7; } else timer--; }
        public void HalfFrame()
        {
            if (!Envelope.Loop && Length > 0) Length--;
            if (sweepDivider == 0 && sweepEnabled && sweepShift > 0 && period >= 8 && Target is >= 0 and <= 0x7ff) period = Target;
            if (sweepDivider == 0 || sweepReload) { sweepDivider = sweepPeriod; sweepReload = false; }
            else sweepDivider--;
        }
    }
}
