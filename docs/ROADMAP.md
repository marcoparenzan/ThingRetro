# Roadmap

## Done

- 2026-10-18: the MOS and CSG chips and the assembler moved here from ThingRetroEmu, after being brought together from Thing64, Thing65 and Thing1500.
  Thing64, Thing65 and ThingCommodore use them; 140 tests of the chips pass on this repository alone.

## Next

1. **6507 and 6532 RIOT**, for an Atari 2600 (with the TIA, which is Atari's and goes in a repository of its own).
2. **The Apple II**: a 6502, 4-48 KB of RAM, the ROM, the keyboard, the speaker, the video from TTL logic and the Disk II. A board in a new repository, with chips from here
   and the Disk II controller from ThingRetroEmu.
3. **6551 ACIA, 6545 CRTC, 6567 (NTSC)** and the **8580 SID**, for the PET, the VIC-20 and the NTSC machines.
4. **One file per pinout**, and a page per chip with the datasheet reference and the test that checks it.

## Then

- 65C02, 65816 (a CPU with its table in the assembler).
- A 6502 that runs the SingleStepTests from this repository too, without the computers.
