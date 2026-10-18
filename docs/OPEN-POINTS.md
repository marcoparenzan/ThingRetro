# Open points

1. **1581 timing.** The WD1770 takes its time from guessed constants: step rates of 6, 12, 2 and 3 ms, 32 µs a byte, no rotational latency (the first
   byte comes about 30 byte times after the command). The DOS does not mind, but the speed of a LOAD is not the real one.
2. **1581 fast serial** (the C128 burst mode on SP and CNT) is not done: a C128 would fall back to the slow bus.
3. **1581 write protect** polarity of PB6 is taken from the DOS (it checks bit 6 of `$4001` before writing); it works with unprotected disks, protected
   ones are untested on the 1581.
4. **`.g64`, `.d71`, `.d82`**: not read. The 1571 and the 1541-II need their boards.
5. **C2N speed.** Rewind and fast forward are quick, not at the real tape speed; a tape position counter in seconds is shown only while playing.
6. **MPS-803 codes.** (It is on the IEC bus, as the real one is.) ESC sequences, dot tabs, and the printer's own status channel are not done; CR cancels graphics mode, double width and reverse (to be
   checked against the manual). The 803 prints the Commodore font; here the font is the computer's character generator at 8 x 8.
7. **No IEC device number conflicts check** in the host except in the web page (a 1541 and a 1581 on the same number).
8. **Printer paper in the web page** is kept in memory; a long job can fill it. There is no limit on the number of sheets.
