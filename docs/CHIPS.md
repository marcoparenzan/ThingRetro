# Chips of other makers

The MOS and CSG chips are in [ThingMOS](https://github.com/marcoparenzan/ThingMOS) (see its docs/CHIPS.md). Here are the chips of the other makers and the
devices that are not chips:

| Part | Class | Used by | Missing / assumed |
| --- | --- | --- | --- |
| Western Digital WD1770 | `Wd1770` | ThingCommodore (1581) | sector level: no MFM bits, no CRC of the data; Read Address gives a valid CRC; no INTRQ/DRQ wiring on the 1581 |
| RAM expansion card | `RamExpansion` | Thing65 | |
| ROM cartridge | `RomCartridge` | Thing65 | |
| `ParallelPort` | | every chip with ports | the direction, the latch and the pins pulled from outside |
