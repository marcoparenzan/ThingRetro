# Chips

| Chip | Class | Used by | Missing / assumed |
| --- | --- | --- | --- |
| MOS 6502 | `MOS6502` | ThingCommodore (1541, 1581) | `SO` pin: a falling edge sets V (the 1541's byte ready); no port |
| MOS 6510 | `MOS6510` | Thing64 | |
| CSG 4510 | `MOS4510` | Thing65 | memory mapper `MAP`/`EOM`, 1 MB address space |
| CIA 6526 | `MOS6526` | Thing64, Thing65 | FLAG input (cassette read, ATN); no CNT, SP pins |
| CIA 8520 | `MOS8520` | ThingCommodore (1581) | the event counter does not count (the 1581 holds the TOD pin) |
| SID 6581 | `MOS6581` | Thing64, Thing65 | 8580 model |
| VIC-II 6569 | `MOS6569` | Thing64 | PAL only |
| VIC-III | `CSG4567` | Thing65 | 1280-pixel mode shows only the early pixel; no external sync |
| VIA 6522 | `Via6522` | ThingCommodore (1541) | no shift register, no port latching |
| DMAgic F018 | `F018` | Thing65 | XY_MOD misc byte assumed |
| F011 | `F011` | Thing65 | one revolution of 6250 bytes for free-format tracks |
| UART | `C65Uart` | Thing65 | baud rate and parity only stored |
| WD1770 | `Wd1770` | ThingCommodore (1581) | sector level: no MFM bits, no CRC of the data; Read Address gives a valid CRC; no INTRQ/DRQ wiring on the 1581 |
