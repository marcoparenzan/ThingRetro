namespace ThingMOS;

// MOS 8520, the 6526 of the Amiga and of the 1581: the same ports, timers, serial register and interrupts, but an event counter
// in place of the time of day: three binary bytes ($08-$0A, $0B is unused) that count the edges on the TOD pin. The 1581 holds
// that pin, so here the counter does not count: it keeps what the DOS writes
public class MOS8520 : MOS6526
{
    protected override bool BinaryTod => true;
    protected override bool TodClocked => false;
}
