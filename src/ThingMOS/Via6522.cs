
namespace ThingMOS;

// MOS 6522 VIA, DIP-40: two parallel ports, two timers, interrupt flags. The drives use it for the IEC bus, the motor, the
// stepper and the read/write head.
// Registers: 0 ORB, 1 ORA, 2 DDRB, 3 DDRA, 4/5 T1 counter low/high, 6/7 T1 latch low/high, 8/9 T2 low/high, 10 SR,
// 11 ACR, 12 PCR, 13 IFR, 14 IER, 15 ORA without handshake
// Not modelled: the shift register, the port latching (ACR bits 0-1), the PB7 output of timer 1 and timer 2 pulse counting
// Reference: "R6522 VIA Data Sheet" (Rockwell / WDC)
public class Via6522 : IVia6522Pinout
{
    // Interrupt flags
    const int CA2 = 0x01, CA1 = 0x02, SHIFT = 0x04, CB2 = 0x08, CB1 = 0x10, T2 = 0x20, T1 = 0x40, ANY = 0x80;

    public ParallelPort PortA { get; } = new();
    public ParallelPort PortB { get; } = new();

    // Control lines as inputs: true = high. CA1/CB1 start the interrupt with the edge PCR selects
    public Func<bool> Ca1Input { get; set; } = () => true;
    public Func<bool> Cb1Input { get; set; } = () => true;
    public Func<bool> Ca2Input { get; set; } = () => true;
    public Func<bool> Cb2Input { get; set; } = () => true;

    byte ifr, ier, acr, pcr, sr;
    byte t1Latch0, t1Latch1, t2Latch0;
    ushort t1, t2;
    bool t1Armed, t2Armed, t1Reload;
    bool ca1, cb1, ca2, cb2;

    // CA2 and CB2 as outputs (PCR bits 3-1 / 7-5 = 11x manual: 110 low, 111 high). Other modes read as high
    public bool Ca2Output => (pcr & 0x0E) != 0x0C;
    public bool Cb2Output => (pcr & 0xE0) != 0xC0;

    // /IRQ is low while an enabled flag is set
    public bool Irq => (ifr & ier & 0x7F) != 0;

    public void Reset()
    {
        PortA.Reset();
        PortB.Reset();
        ifr = ier = acr = pcr = sr = 0;
        t1Latch0 = t1Latch1 = t2Latch0 = 0;
        t1 = t2 = 0xFFFF;
        t1Armed = t2Armed = t1Reload = false;
        ca1 = Ca1Input();
        cb1 = Cb1Input();
        ca2 = Ca2Input();
        cb2 = Cb2Input();
    }

    // One φ2 cycle: the timers count and the control lines are looked at for edges
    public void Tick()
    {
        if (t1Reload)
        {
            t1 = (ushort)(t1Latch1 << 8 | t1Latch0); // after an underflow the latch is loaded in a cycle of its own
            t1Reload = false;
        }
        else if (--t1 == 0xFFFF)
        {
            if (t1Armed) ifr |= T1;
            if ((acr & 0x40) != 0) t1Reload = true; // free run: again and again
            else t1Armed = false;                   // one shot: the counter goes on, silently
        }

        if (--t2 == 0xFFFF && t2Armed)
        {
            ifr |= T2;
            t2Armed = false;
        }

        bool level = Ca1Input();
        if (level != ca1 && level == ((pcr & 0x01) != 0)) ifr |= CA1;
        ca1 = level;
        level = Cb1Input();
        if (level != cb1 && level == ((pcr & 0x10) != 0)) ifr |= CB1;
        cb1 = level;
        level = Ca2Input();
        if (level != ca2 && (pcr & 0x08) == 0 && level == ((pcr & 0x04) != 0)) ifr |= CA2; // CA2 as an input
        ca2 = level;
        level = Cb2Input();
        if (level != cb2 && (pcr & 0x80) == 0 && level == ((pcr & 0x40) != 0)) ifr |= CB2;
        cb2 = level;
    }

    public byte Read(int register)
    {
        switch (register & 0x0F)
        {
            case 0:
                ifr &= unchecked((byte)~(CB1 | ((pcr & 0xA0) == 0x20 ? 0 : CB2))); // independent CB2 interrupt mode keeps its flag
                return PortB.Pins;
            case 1:
                ifr &= unchecked((byte)~(CA1 | ((pcr & 0x0A) == 0x02 ? 0 : CA2)));
                return PortA.Pins;
            case 2: return PortB.Direction;
            case 3: return PortA.Direction;
            case 4:
                ifr &= unchecked((byte)~T1);
                return (byte)t1;
            case 5: return (byte)(t1 >> 8);
            case 6: return t1Latch0;
            case 7: return t1Latch1;
            case 8:
                ifr &= unchecked((byte)~T2);
                return (byte)t2;
            case 9: return (byte)(t2 >> 8);
            case 10: return sr;
            case 11: return acr;
            case 12: return pcr;
            case 13: return (byte)((ifr & 0x7F) | (Irq ? ANY : 0));
            case 14: return (byte)(ier | 0x80);
            default: return PortA.Pins;
        }
    }

    public void Write(int register, byte value)
    {
        switch (register & 0x0F)
        {
            case 0:
                PortB.Latch = value;
                ifr &= unchecked((byte)~(CB1 | ((pcr & 0xA0) == 0x20 ? 0 : CB2)));
                break;
            case 1:
                PortA.Latch = value;
                ifr &= unchecked((byte)~(CA1 | ((pcr & 0x0A) == 0x02 ? 0 : CA2)));
                break;
            case 2: PortB.Direction = value; break;
            case 3: PortA.Direction = value; break;
            case 4:
            case 6:
                t1Latch0 = value;
                break;
            case 5:                          // starts timer 1: the latch is loaded in the counter
                t1Latch1 = value;
                t1 = (ushort)(value << 8 | t1Latch0);
                ifr &= unchecked((byte)~T1);
                t1Armed = true;
                t1Reload = false;
                break;
            case 7:
                t1Latch1 = value;
                ifr &= unchecked((byte)~T1);
                break;
            case 8: t2Latch0 = value; break;
            case 9:                          // starts timer 2
                t2 = (ushort)(value << 8 | t2Latch0);
                ifr &= unchecked((byte)~T2);
                t2Armed = true;
                break;
            case 10: sr = value; break;
            case 11: acr = value; break;
            case 12: pcr = value; break;
            case 13: ifr &= (byte)~(value & 0x7F); break;                        // writing a 1 clears the flag
            case 14: ier = (byte)((value & 0x80) != 0 ? ier | value & 0x7F : ier & ~value & 0x7F); break; // bit 7: set or clear
            case 15: PortA.Latch = value; break;
        }
    }
}
