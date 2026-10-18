namespace ThingMOS;

public partial class MOS6526 : I6526Pinout
{
    // The 8520 (Amiga, 1581) has an event counter in place of the time of day: 24 bits in binary, clocked from its TOD pin
    // (not at all in the 1581). Its subclass says so
    protected virtual bool BinaryTod => false;
    protected virtual bool TodClocked => true;

    // PA0-PA7 and PB0-PB7: registers 0/1 (data) and 2/3 (direction)
    public ParallelPort PortA { get; } = new();
    public ParallelPort PortB { get; } = new();

    // /IRQ: active until the ICR is read
    public bool Irq => (interruptStatus & 0x80) != 0;

    // FLAG: a falling edge sets bit 4 of the ICR (and the /IRQ, if enabled). Unconnected it is high
    public Func<bool> FlagInput { get; set; } = () => true;
    bool flagWasHigh = true;

    // Registers
    private ushort timerA; // $DC04-$DC05/DD04-DD05
    private ushort timerB; // $DC06-$DC07/DD06-DD07
    private byte tod10thsec; // $DC08/DD08
    private byte todSec; // $DC09/DD09
    private byte todMin; // $DC0A/DD0A
    private byte todHr; // $DC0B/DD0B
    private byte serialShift; // $DC0C/DD0C
    private byte interruptControl; // $DC0D/DD0D (ICR)
    private byte controlRegA; // $DC0E/DD0E (CRA)
    private byte controlRegB; // $DC0F/DD0F (CRB)

    // Timer latches
    private ushort timerALatch;
    private ushort timerBLatch;

    // TOD latches
    private byte todAlarm10thsec;
    private byte todAlarmSec;
    private byte todAlarmMin;
    private byte todAlarmHr;

    // Status flags
    private bool timerARunning;
    private bool timerBRunning;
    private bool todRunning;
    private bool serialRunning;
    private byte interruptStatus; // Internal interrupt status

    // Cycle-exact signal tracking
    private bool pendingTimerAUnderflow;
    private bool pendingTimerBUnderflow;
    private bool pendingTodAlarm;
    private byte pendingInterrupts;
    private int todCycleDivider;
    private const int TOD_CYCLE_INTERVAL = 19656; // PAL cycles for 1/10 second (50Hz system)

    public MOS6526()
    {
        Reset();
    }

    // /RES: all registers cleared, ports as inputs, timers stopped
    public void Reset()
    {
        // Clear all registers
        PortA.Reset();
        PortB.Reset();
        timerA = timerB = 0;
        timerALatch = timerBLatch = 0;
        tod10thsec = todSec = todMin = todHr = 0;
        todAlarm10thsec = todAlarmSec = todAlarmMin = todAlarmHr = 0;
        serialShift = 0;
        interruptControl = 0;
        controlRegA = controlRegB = 0;
        interruptStatus = 0;

        // Reset status flags
        timerARunning = timerBRunning = false;
        todRunning = TodClocked; // TOD clock starts running
        serialRunning = false;

        // Reset cycle-exact tracking
        pendingTimerAUnderflow = false;
        pendingTimerBUnderflow = false;
        pendingTodAlarm = false;
        pendingInterrupts = 0;
        todCycleDivider = 0;
    }

    // φ2: one cycle of timers, TOD and interrupts
    public void Tick()
    {
        ProcessSignals();
        Clock();
        bool flagHigh = FlagInput();
        if (flagWasHigh && !flagHigh)
        {
            interruptStatus |= 0x10;
            CheckInterrupts();
        }
        flagWasHigh = flagHigh;
    }

    // RS0-RS3: the 16 registers repeat across the whole area selected by /CS
    public byte Read(int register)
    {
        switch (register & 0x0F)
        {
            case 0x00:
                // Output bits return the value written, input bits the state of the pin
                return PortA.Pins;
            case 0x01:
                return PortB.Pins;
            case 0x02: return PortA.Direction; // DDRA
            case 0x03: return PortB.Direction; // DDRB
            case 0x04: return (byte)(timerA & 0xFF); // Timer A low
            case 0x05: return (byte)(timerA >> 8); // Timer A high
            case 0x06: return (byte)(timerB & 0xFF); // Timer B low
            case 0x07: return (byte)(timerB >> 8); // Timer B high
            case 0x08: return tod10thsec; // TOD 10ths
            case 0x09: return todSec; // TOD seconds
            case 0x0A: return todMin; // TOD minutes
            case 0x0B: return todHr; // TOD hours
            case 0x0C: return serialShift; // Serial port
            case 0x0D: // Interrupt Control Register (ICR)
                byte temp = interruptStatus;
                interruptStatus = 0; // Reading clears all the flags and releases the IRQ line
                return temp;
            case 0x0E: return controlRegA; // Control register A
            case 0x0F: return controlRegB; // Control register B
            default: return 0xFF;
        }
    }

    public void Write(int register, byte value)
    {
        switch (register & 0x0F)
        {
            case 0x00: // Port A
                PortA.Latch = value;
                break;
            case 0x01: // Port B
                PortB.Latch = value;
                break;
            case 0x02: // DDRA
                PortA.Direction = value;
                break;
            case 0x03: // DDRB
                PortB.Direction = value;
                break;
            case 0x04: // Timer A low
                timerALatch = (ushort)((timerALatch & 0xFF00) | value);
                break;
            case 0x05: // Timer A high
                timerALatch = (ushort)((value << 8) | (timerALatch & 0x00FF));
                if ((controlRegA & 0x01) == 0) // Timer stopped
                {
                    timerA = timerALatch;
                }
                break;
            case 0x06: // Timer B low
                timerBLatch = (ushort)((timerBLatch & 0xFF00) | value);
                break;
            case 0x07: // Timer B high
                timerBLatch = (ushort)((value << 8) | (timerBLatch & 0x00FF));
                if ((controlRegB & 0x01) == 0) // Timer stopped
                {
                    timerB = timerBLatch;
                }
                break;
            case 0x08: // TOD 10ths or Alarm 10ths
                if ((controlRegB & 0x80) != 0)
                    todAlarm10thsec = (byte)(value & (BinaryTod ? 0xFF : 0x0F));
                else
                    tod10thsec = (byte)(value & (BinaryTod ? 0xFF : 0x0F));
                break;
            case 0x09: // TOD seconds or Alarm seconds
                if ((controlRegB & 0x80) != 0)
                    todAlarmSec = (byte)(value & (BinaryTod ? 0xFF : 0x7F));
                else
                    todSec = (byte)(value & (BinaryTod ? 0xFF : 0x7F));
                break;
            case 0x0A: // TOD minutes or Alarm minutes
                if ((controlRegB & 0x80) != 0)
                    todAlarmMin = (byte)(value & (BinaryTod ? 0xFF : 0x7F));
                else
                    todMin = (byte)(value & (BinaryTod ? 0xFF : 0x7F));
                break;
            case 0x0B: // TOD hours or Alarm hours
                if ((controlRegB & 0x80) != 0)
                    todAlarmHr = (byte)(value & 0x9F);
                else
                {
                    todHr = (byte)(value & 0x9F);
                    todRunning = TodClocked;
                }
                break;
            case 0x0C: // Serial port
                serialShift = value;
                break;
            case 0x0D: // Interrupt Control Register
                if ((value & 0x80) != 0)
                {
                    // Set bits
                    interruptControl |= (byte)(value & 0x7F);
                }
                else
                {
                    // Clear bits
                    interruptControl &= (byte)~value;
                }
                this.CheckInterrupts();
                break;
            case 0x0E: // Control register A
                controlRegA = (byte)(value & ~0x10); // Bit 4 (force load) is a strobe, it is not stored
                timerARunning = (value & 0x01) != 0; // Start bit
                if ((value & 0x10) != 0) // Force load: loads the latch even while the timer is stopped
                {
                    timerA = timerALatch;
                }
                break;
            case 0x0F: // Control register B
                controlRegB = (byte)(value & ~0x10); // Bit 4 (force load) is a strobe, it is not stored
                timerBRunning = (value & 0x01) != 0; // Start bit
                if ((value & 0x10) != 0) // Force load: loads the latch even while the timer is stopped
                {
                    timerB = timerBLatch;
                }
                break;
        }
    }
}