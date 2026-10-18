namespace ThingRetroEmu.Buses;

/*
    A device on the IEC cable that only listens (a printer, a plotter), done at the level of the bus protocol instead of with a CPU
    and a ROM. It is clocked once per cycle of the computer's clock (Tick) and watches ATN, CLK and DATA like the real device:

      ATN down      every device holds DATA down ("I am here") and receives the command bytes: LISTEN (0x20 + device), UNLISTEN (0x3F),
                    TALK (0x40 + device), UNTALK (0x5F), then the secondary address (0x60 + channel, 0xE0 close, 0xF0 open)
      a byte        the talker releases CLK ("ready to send"), the listener releases DATA ("ready for data"), the talker pulls CLK down
                    and puts the eight bits on DATA, least significant first, each one valid while CLK is released; the listener
                    pulls DATA down after the eighth bit (the frame is acknowledged)
      EOI           if the talker leaves CLK released for 200 µs after DATA is ready, this is the last byte: the listener pulls DATA
                    down for about 60 µs and releases it

    Only the listening side is done: a device addressed with TALK stays silent. The subclass says what to do with the bytes.
    Reference: "Commodore 64 Programmer's Reference Guide", chapter 5, "Inside Commodore DOS" (the serial bus)
 */
public abstract class IecListener
{
    const int EOI_WAIT_CYCLES = 200;    // the talker took more than this to start the byte: it is the last one
    const int EOI_ACK_CYCLES = 70;      // how long DATA is held down to say that the EOI was seen

    enum State { Idle, WaitReady, WaitStart, Bits, EoiAck }

    IecBus? bus;
    State state = State.Idle;
    bool dataLow;
    bool lastAtn;
    bool lastClk;
    bool clkWasLow;
    int ticks;
    int bit;
    byte value;
    bool eoi;
    bool listening;
    bool addressedTalker;
    int channel = -1;

    protected IecListener(int deviceNumber) => DeviceNumber = deviceNumber;

    // The device number: the printers answer to 4 or 5
    public int DeviceNumber { get; set; }

    public bool Listening => listening;
    public bool Plugged => bus is not null;

    // The cable: the device pulls DATA down, and reads the three lines
    public void Plug(IecBus cable)
    {
        if (bus is not null) throw new InvalidOperationException("The device is already plugged in");
        bus = cable;
        cable.Data.Connect(() => dataLow);
    }

    // The computer asked this device to listen, on this channel (the secondary address 0-15; -1 when it sent none)
    protected virtual void OnListen(int channel) { }

    // A data byte arrived; eoi says it is the last one of the transmission
    protected abstract void OnData(byte value, bool eoi);

    // The computer sent UNLISTEN (the transmission ended)
    protected virtual void OnUnlisten() { }

    // The channel was opened (0xF0 + n) or closed (0xE0 + n)
    protected virtual void OnChannel(int channel, bool open) { }

    // One cycle of the computer's clock
    public void Tick()
    {
        if (bus is null) return;
        bool atn = bus.Atn.IsLow;
        bool clk = bus.Clk.IsLow;

        if (atn && !lastAtn)
        {
            // ATN down: every device says that it is there, and listens to the command
            dataLow = true;
            state = State.WaitReady;
            clkWasLow = clk;
            addressedTalker = false;
        }
        else if (!atn && lastAtn)
        {
            if (listening || addressedTalker)
            {
                dataLow = true;                 // addressed: holds DATA until the next byte is ready
                state = State.WaitReady;
                clkWasLow = clk;
            }
            else
            {
                dataLow = false;
                state = State.Idle;
            }
        }
        lastAtn = atn;

        switch (state)
        {
            case State.WaitReady:
                if (clk) clkWasLow = true;
                else if (clkWasLow)
                {
                    // The talker released CLK: ready to send. Ready for data
                    if (!addressedTalker)
                    {
                        dataLow = false;
                        state = State.WaitStart;
                        ticks = 0;
                    }
                }
                break;

            case State.WaitStart:
                if (clk)
                {
                    state = State.Bits;
                    bit = 0;
                    value = 0;
                }
                else if (++ticks >= EOI_WAIT_CYCLES && !eoi)
                {
                    eoi = true;
                    dataLow = true;
                    ticks = 0;
                    state = State.EoiAck;
                }
                break;

            case State.EoiAck:
                if (++ticks >= EOI_ACK_CYCLES)
                {
                    dataLow = false;
                    state = State.WaitStart;
                    ticks = EOI_WAIT_CYCLES;   // the talker takes it from here
                }
                break;

            case State.Bits:
                if (!clk && lastClk && bit < 8)
                {
                    // CLK released: the bit on DATA is valid (a released DATA is a 1)
                    if (!bus.Data.IsLow) value |= (byte)(1 << bit);
                    bit++;
                }
                else if (clk && !lastClk && bit == 8)
                {
                    // CLK down again after the eighth bit: the byte is complete; acknowledge the frame
                    dataLow = true;
                    state = State.WaitReady;
                    clkWasLow = true;
                    Byte(value, atn);
                    eoi = false;
                }
                break;
        }
        lastClk = clk;
    }

    void Byte(byte b, bool command)
    {
        if (!command)
        {
            if (listening) OnData(b, eoi);
            return;
        }

        if (b == 0x3F)                                  // UNLISTEN
        {
            if (listening) OnUnlisten();
            listening = false;
            channel = -1;
        }
        else if (b == 0x5F)                             // UNTALK
        {
            addressedTalker = false;
        }
        else if ((b & 0xE0) == 0x20)                    // LISTEN device
        {
            listening = (b & 0x1F) == DeviceNumber;
            channel = -1;
            if (listening) OnListen(-1);
        }
        else if ((b & 0xE0) == 0x40)                    // TALK device
        {
            addressedTalker = (b & 0x1F) == DeviceNumber;
            listening = false;
        }
        else if (listening && (b & 0xF0) is 0x60 or 0xE0 or 0xF0)
        {
            channel = b & 0x0F;
            if ((b & 0xF0) == 0x60) OnListen(channel);
            else OnChannel(channel, open: (b & 0xF0) == 0xF0);
        }
    }
}
