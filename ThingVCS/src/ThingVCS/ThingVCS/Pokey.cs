namespace ThingVCS;

public interface IPokeyPinout
{
    byte Read(int register);
    void Write(int register, byte value);
    void Tick();
    void Reset();
    double AudioLevel { get; }
}
public interface IPokeyInterruptPinout { bool Irq { get; } }

/// <summary>Functional POKEY audio/noise/filters/timers. Serial, keyboard, POT conversion and exact phases are pending.</summary>
public sealed partial class Pokey : IPokeyPinout, IPokeyInterruptPinout, IPokeyPeripheralPinout
{
    readonly byte[] registers = new byte[16];
    readonly int[] counters = new int[4];
    readonly bool[] high = new bool[4], filter = new bool[2];
    int poly4, poly5, poly9 = 511, poly17 = 131071;
    byte timerFlags;
    int timerResetDelay;
    public bool Irq => (timerFlags & registers[14]) != 0;
    public long Clocks { get; private set; }
    public double AudioLevel
    {
        get
        {
            double sum = 0;
            for (int channel = 0; channel < 4; channel++)
            {
                byte control = registers[channel * 2 + 1];
                bool volumeOnly = (control & 0x10) != 0;
                bool joinedLow = channel == 0 && (registers[8] & 0x10) != 0 || channel == 2 && (registers[8] & 8) != 0;
                bool filtered = high[channel];
                if (channel < 2 && (registers[8] & (4 >> channel)) != 0) filtered ^= filter[channel];
                if (volumeOnly || (!joinedLow && filtered)) sum += (control & 15) / 60.0;
            }
            return sum;
        }
    }
    public int ChannelPeriod(int channel)
    {
        if (channel is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(channel));
        int audctl = registers[8], baseClock = (audctl & 1) != 0 ? 114 : 28;
        int frequency = registers[channel * 2];
        bool fast = (audctl & (channel < 2 ? 0x40 : 0x20)) != 0;
        bool joined = (audctl & (channel < 2 ? 0x10 : 8)) != 0;
        if ((channel & 1) != 0 && joined)
        {
            int word = (frequency << 8) | registers[(channel - 1) * 2];
            return fast ? word + 7 : (word + 1) * baseClock;
        }
        return (channel & 1) == 0 && fast ? frequency + 4 : (frequency + 1) * baseClock;
    }
    public byte Read(int register) => (register & 15) switch
    {
        10 => (registers[15] & 3) == 0 ? (byte)0xff : (byte)((registers[8] & 0x80) != 0 ? poly9 : poly17 >> 8),
        14 => (byte)~timerFlags,
        <= 7 => ReadPot(register & 7),
        8 => pendingPots,
        9 => keyCode,
        13 => serialIn,
        15 => ReadSerialStatus(),
        _ => 0xff
    };
    public void Write(int register, byte value)
    {
        int index = register & 15;
        registers[index] = value;
        if (index == 9) timerResetDelay = 4; // STIMER settles four chip clocks later; does not acknowledge IRQ.
        if (index == 14) timerFlags &= (byte)(value | 8); // Serial-complete status survives masking.
        PeripheralWrite(index, value);
        if (index == 15 && (value & 3) == 0) { ResetPolynomials(); registers[14] = 0; timerFlags &= 8; }
    }
    static int ShiftSmall(int state, int bits) => ((state << 1) | (~((state >> 2) ^ (state >> (bits - 1))) & 1)) & ((1 << bits) - 1);
    void ResetPolynomials() { poly4 = poly5 = 0; poly9 = 511; poly17 = 131071; }
    public void Tick()
    {
        Clocks++;
        TickPeripherals();
        if (timerResetDelay > 0 && --timerResetDelay == 0)
        {
            Array.Clear(counters); Array.Clear(high); Array.Clear(filter);
            return;
        }
        if ((registers[15] & 3) == 0) return;
        poly4 = ShiftSmall(poly4, 4); poly5 = ShiftSmall(poly5, 5);
        poly9 = (poly9 >> 1) | (((poly9 ^ (poly9 >> 5)) & 1) << 8);
        int bit7 = ((poly17 >> 8) ^ (poly17 >> 13)) & 1, bit16 = poly17 & 1;
        poly17 = ((poly17 >> 1) & ~0x80) | (bit7 << 7) | (bit16 << 16);
        for (int channel = 0; channel < 4; channel++)
        {
            if (++counters[channel] < ChannelPeriod(channel)) continue;
            counters[channel] = 0;
            int irqBit = channel switch { 0 => 1, 1 => 2, 3 => 4, _ => 0 };
            timerFlags |= (byte)(irqBit & registers[14]);
            byte control = registers[channel * 2 + 1];
            if ((control & 0x80) != 0 || (poly5 & 1) != 0)
            {
                if ((control & 0x20) != 0) high[channel] = !high[channel];
                else high[channel] = ((control & 0x40) != 0 ? poly4 : (registers[8] & 0x80) != 0 ? poly9 : poly17) % 2 != 0;
            }
            if (channel == 2 && (registers[8] & 4) != 0) filter[0] = high[0];
            if (channel == 3 && (registers[8] & 2) != 0) filter[1] = high[1];
        }
    }
    public void Reset()
    {
        Array.Clear(registers); Array.Clear(counters); Array.Clear(high); Array.Clear(filter);
        ResetPolynomials(); ResetPeripherals(); timerResetDelay = 0; timerFlags = 0; Clocks = 0;
    }
}
