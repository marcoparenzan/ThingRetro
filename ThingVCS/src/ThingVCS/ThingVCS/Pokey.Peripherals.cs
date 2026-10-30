namespace ThingVCS;

public interface IPokeyPeripheralPinout
{
    Func<int, int> PotThreshold { get; set; } // Comparator crossing in 0..228 ticks, not resistance in ohms.
    Func<int, bool> KeyContact { get; set; } // 6-bit matrix scan code, true = closed.
    Func<bool> ShiftKey { get; set; }
    Func<bool> ControlKey { get; set; }
    Func<bool> BreakKey { get; set; }
    bool SerialOutputHigh { get; }
    void ClockSerialInput(bool high); // External bit-clock edge; idle high, 8N1 functional framing.
    void ClockSerialOutput();
}

public sealed partial class Pokey
{
    public Func<int, int> PotThreshold { get; set; } = _ => 228;
    public Func<int, bool> KeyContact { get; set; } = _ => false;
    public Func<bool> ShiftKey { get; set; } = () => false;
    public Func<bool> ControlKey { get; set; } = () => false;
    public Func<bool> BreakKey { get; set; } = () => false;
    readonly byte[] pots = Enumerable.Repeat((byte)228, 8).ToArray();
    readonly int[] thresholds = new int[8];
    byte pendingPots = 255, keyCode = 9, serialIn = 255, errors;
    int potDivider, potCount, keyDivider, scanCode, candidate = -1, heldKey = -1;
    bool converting, previousBreak, serialInputHigh = true;
    int receiveBit = -1, receiveByte, transmitFrame, transmitBits;
    byte? transmitPending;
    bool serialOutput = true;
    // Exposed for diagnostics: two-tone/asynchronous/internal serial clock modes are not implemented.
    public byte UnsupportedSkctlModes => (byte)(registers[15] & 0x78);
    public bool SerialOutputHigh => (registers[15] & 0x80) == 0 && serialOutput;
    byte ReadPot(int input) => converting && (pendingPots & (1 << input)) != 0 ? (byte)potCount : pots[input];
    byte ReadSerialStatus()
    {
        int flags = errors | (ShiftKey() ? 8 : 0) | (heldKey >= 0 ? 4 : 0) |
            (!serialInputHigh ? 16 : 0) | (receiveBit >= 0 ? 2 : 0);
        return (byte)~flags;
    }
    void PeripheralWrite(int register, byte value)
    {
        if (register == 10) errors = 0; // SKREST clears framing/overrun errors, not IRQ flags.
        if (register == 11 && (registers[15] & 3) != 0)
        {
            pendingPots = 255; potCount = potDivider = 0; converting = true;
            for (int i = 0; i < 8; i++)
            {
                thresholds[i] = Math.Clamp(PotThreshold(i), 0, 228);
                if (thresholds[i] == 0) { pots[i] = 0; pendingPots &= (byte)~(1 << i); }
            }
        }
        if (register == 13 && (registers[15] & 3) != 0)
        {
            transmitPending = value; timerFlags &= unchecked((byte)~8);
        }
        if (register == 15)
        {
            if ((value & 3) == 0) ResetPeripherals();
            if ((value & 2) == 0) { candidate = heldKey = -1; scanCode = keyDivider = 0; }
        }
    }
    void TickPeripherals()
    {
        if ((registers[15] & 3) == 0) return;
        if (converting && ++potDivider >= ((registers[15] & 4) != 0 ? 1 : 114))
        {
            potDivider = 0; potCount++;
            for (int i = 0; i < 8; i++)
                if ((pendingPots & (1 << i)) != 0 && potCount >= thresholds[i])
                { pots[i] = (byte)thresholds[i]; pendingPots &= (byte)~(1 << i); }
            if (pendingPots == 0 || potCount >= 228) converting = false;
        }
        bool pressedBreak = BreakKey();
        if (pressedBreak && !previousBreak) timerFlags |= (byte)(registers[14] & 0x80);
        previousBreak = pressedBreak;
        if ((registers[15] & 2) != 0 && ++keyDivider >= 114)
        {
            keyDivider = 0;
            if (heldKey >= 0 && !KeyContact(heldKey)) { heldKey = candidate = -1; }
            if (heldKey < 0 && KeyContact(scanCode))
            {
                if ((registers[15] & 1) == 0 || candidate == scanCode)
                {
                    if ((timerFlags & 0x40) != 0) errors |= 0x40;
                    heldKey = scanCode; keyCode = (byte)(scanCode | (ShiftKey() ? 0x40 : 0) | (ControlKey() ? 0x80 : 0));
                    timerFlags |= (byte)(registers[14] & 0x40);
                }
                else candidate = scanCode;
            }
            else if (candidate == scanCode && !KeyContact(scanCode)) candidate = -1;
            scanCode = (scanCode + 1) & 63;
        }
    }
    public void ClockSerialInput(bool high)
    {
        serialInputHigh = high;
        if ((registers[15] & 3) == 0) return;
        if (receiveBit < 0) { if (!high) { receiveBit = 0; receiveByte = 0; } return; }
        if (receiveBit < 8) { if (high) receiveByte |= 1 << receiveBit; receiveBit++; return; }
        if (!high) errors |= 0x80;
        if ((timerFlags & 0x20) != 0) errors |= 0x20;
        serialIn = (byte)receiveByte; timerFlags |= (byte)(registers[14] & 0x20); receiveBit = -1;
    }
    public void ClockSerialOutput()
    {
        if ((registers[15] & 3) == 0) return;
        if (transmitBits == 0)
        {
            if (transmitPending is not byte data) { serialOutput = true; return; }
            transmitFrame = (1 << 9) | (data << 1); transmitBits = 10; transmitPending = null;
            timerFlags |= (byte)(registers[14] & 0x10);
        }
        serialOutput = (transmitFrame & 1) != 0; transmitFrame >>= 1; transmitBits--;
        if (transmitBits == 0) timerFlags |= 8;
    }
    void ResetPeripherals()
    {
        Array.Fill(pots, (byte)228); pendingPots = 255; keyCode = 9; serialIn = 255; errors = 0;
        converting = false; potDivider = potCount = keyDivider = scanCode = 0; candidate = heldKey = -1;
        previousBreak = false; receiveBit = -1; transmitBits = 0; transmitPending = null;
        serialInputHigh = serialOutput = true;
    }
}
