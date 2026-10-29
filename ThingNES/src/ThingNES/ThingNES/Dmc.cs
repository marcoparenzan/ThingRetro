namespace ThingNES;

// NTSC DMC output and memory-reader state. The board services the requested DMA.
public sealed class Dmc
{
    static readonly int[] Periods = [428,380,340,320,286,254,226,214,190,160,142,128,106,84,72,54];
    int period = 428, timer, bitsRemaining = 8;
    byte shift, sampleBuffer;
    bool bufferEmpty = true, silent = true, loop, irqEnabled;
    ushort sampleAddress = 0xc000, currentAddress;
    int sampleLength = 1, remaining;
    public int Output { get; private set; }
    public bool Irq { get; private set; }
    public bool Active => remaining > 0;
    public ushort? DmaAddress => bufferEmpty && remaining > 0 ? currentAddress : null;

    public void Write(int register, byte value)
    {
        switch (register)
        {
            case 0: irqEnabled = (value & 128) != 0; loop = (value & 64) != 0; period = Periods[value & 15]; if (!irqEnabled) Irq = false; break;
            case 1: Output = value & 127; break;
            case 2: sampleAddress = (ushort)(0xc000 + value * 64); break;
            case 3: sampleLength = value * 16 + 1; break;
            default: throw new ArgumentOutOfRangeException(nameof(register));
        }
    }
    public void SetEnabled(bool enabled)
    {
        Irq = false;
        if (!enabled) remaining = 0;
        else if (remaining == 0) Restart();
    }
    void Restart() { currentAddress = sampleAddress; remaining = sampleLength; }
    public void SupplySample(byte value)
    {
        if (DmaAddress is null) throw new InvalidOperationException("No DMC fetch is pending.");
        sampleBuffer = value; bufferEmpty = false;
        currentAddress = currentAddress == 0xffff ? (ushort)0x8000 : (ushort)(currentAddress + 1);
        if (--remaining == 0)
        {
            if (loop) Restart();
            else if (irqEnabled) Irq = true;
        }
    }
    public void Tick()
    {
        if (timer > 0) { timer--; return; }
        timer = period - 1;
        if (!silent)
        {
            if ((shift & 1) != 0) { if (Output <= 125) Output += 2; }
            else if (Output >= 2) Output -= 2;
        }
        shift >>= 1;
        if (--bitsRemaining == 0)
        {
            bitsRemaining = 8; silent = bufferEmpty;
            if (!bufferEmpty) { shift = sampleBuffer; bufferEmpty = true; }
        }
    }
}
