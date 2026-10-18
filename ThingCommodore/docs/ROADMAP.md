# Roadmap

## Done

- 2026-10-18: the drives moved here from Thing1500 (1541: DOS, GCR mechanics, `.d64` read and write); they use ThingRetroEmu.
- 2026-10-18: **C2N** cassette recorder with `.tap` tapes; Thing64 gets a cassette connector (motor, write, sense, and the CIA's FLAG for read).
- 2026-10-18: **MPS-803** printer on the IEC cable, writing a PDF.
- 2026-10-18: **1581** with the original DOS: directory, load, save, format; WD1770 and 8520 in ThingRetroEmu.
- 2026-10-18: Thing64 web page: 1541, 1581, C2N and printer on one clock and one IEC cable.

## Next

1. **Use the devices in the desktop apps** (Thing64.Computer, Thing65.Computer): windows for the 1581, the recorder and the printer, as the 1541 has.
2. **1541-II, 1571** (a 1571 is a 1541 with two heads and a WD1770 for the MFM side) and the **1570**.
3. **`.g64` images** for the 1541 (the real GCR bits, copy protections).
4. **Fast serial** for the 1581 and the 1571 with a C128.

## Then

- **Other printers**: MPS-801, 1520 plotter (it has pens, not dots), MPS-1200 with ESC/P.
- **Joysticks, paddles, mice, light pens** as devices of their own (ThingRetroEmu has the control ports).
- **User port + ESP32** for home automation: the user port as a real connector, bridged to an ESP32 over serial.
- A second recorder type (the 1531, the Plus/4 Datassette with its different connector).
