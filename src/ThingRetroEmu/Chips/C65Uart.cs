namespace ThingRetroEmu.Chips;

// The C65's UART. Characters leave through Transmit and arrive through Receive; the transmitter is as fast as the CPU
// (a character is gone as soon as it is written) and the baud rate, word length and parity registers are kept but
// do not change anything. The interrupt flags follow the mask: transmitter empty and receiver full, as IRQ or as NMI.
// Registers: 0 data, 1 status, 2 control, 3-4 baud rate, 5 interrupt mask, 6 interrupt flags, 9 fast serial control
// Reference: "C65 Specifications", chapter 2.3.6
public class C65Uart : IUartPinout
{
    // Status bits
    const int FULL = 0x01, OVERRUN = 0x02, XMIT_EMPTY = 0x40, XMIT_DONE = 0x80;
    // Control bits
    const int XMIT_ON = 0x80, RCVR_ON = 0x40;
    // Mask and flag bits
    const int XMIT_IRQ = 0x80, RCVR_IRQ = 0x40, XMIT_NMI = 0x20, RCVR_NMI = 0x10;

    byte data, status = XMIT_DONE | XMIT_EMPTY, control, baudLow, baudHigh, mask, fastSerial;

    public Action<byte>? Transmit { get; set; }

    byte Flags => (byte)(((mask & XMIT_IRQ) != 0 && XmitReady ? XMIT_IRQ : 0) | ((mask & RCVR_IRQ) != 0 && (status & FULL) != 0 ? RCVR_IRQ : 0) |
                         ((mask & XMIT_NMI) != 0 && XmitReady ? XMIT_NMI : 0) | ((mask & RCVR_NMI) != 0 && (status & FULL) != 0 ? RCVR_NMI : 0));

    bool XmitReady => (control & XMIT_ON) != 0;

    public bool Irq => (Flags & (XMIT_IRQ | RCVR_IRQ)) != 0;
    public bool Nmi => (Flags & (XMIT_NMI | RCVR_NMI)) != 0;

    public void Reset()
    {
        data = control = baudLow = baudHigh = mask = fastSerial = 0;
        status = XMIT_DONE | XMIT_EMPTY;
    }

    public byte Read(int register)
    {
        switch (register & 0x0F)
        {
            case 0:
                byte value = data;
                status = (byte)(status & ~(FULL | OVERRUN)); // reading the data register clears the receiver's flags
                return value;
            case 1: return status;
            case 2: return control;
            case 3: return baudLow;
            case 4: return baudHigh;
            case 5: return mask;
            case 6: return Flags;
            case 9: return fastSerial;
            default: return 0xFF;
        }
    }

    public void Write(int register, byte value)
    {
        switch (register & 0x0F)
        {
            case 0:
                if ((control & XMIT_ON) != 0) Transmit?.Invoke(value);
                break;
            case 1: status = (byte)((status & ~0x30) | (value & 0x30)); break; // ENDT and IDLE are writable
            case 2: control = value; break;
            case 3: baudLow = value; break;
            case 4: baudHigh = value; break;
            case 5: mask = value; break;
            case 9: fastSerial = value; break;
        }
    }

    public void Receive(byte value)
    {
        if ((control & RCVR_ON) == 0) return;
        if ((status & FULL) != 0)
        {
            status |= OVERRUN; // the last character is kept, the new one is lost
            return;
        }
        data = value;
        status |= FULL;
    }
}
