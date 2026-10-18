# Open points

1. **Class names.** The CPUs and the CSG chips keep the names they had (`MOS4510`, `MOS6502`...); the CSG ones are not "MOS" strictly. Rename (`CSG4510`)?
2. **`Pinouts.cs` is one big file.** One file per chip, or one per family?
3. **6507.** The Atari 2600's CPU is a 6502 with 13 address pins and no interrupt pins: a class that inherits from `MOS6502` (or a pinout without `IRQ`/`NMI`)?
4. **6532 RIOT** (RAM, I/O, timer): not made yet (Atari 2600, Apple-1 clones, KIM-1).
5. **6551 ACIA, 6545 CRTC, 6567 VIC (NTSC)**: the other MOS chips of the Commodore machines (PET, VIC-20, C128), not made yet. The 6569 is PAL only.
6. **8580 SID**: only the 6581 model.
7. **The 65C02 and 65816** are not here (the 4510 is a 65CE02).
8. **Tests that boot a ROM** are with the computers; the CPU test suites (Klaus Dormann, SingleStepTests) are run by Thing64 and Thing65 from their folders of external data.
9. **The old name.** ThingRetroEmu used to hold these chips until 2026-10-18 (evening): the history of the files is there.
