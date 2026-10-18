# Roadmap

## Done

- 2026-10-18: extracted from Thing65 (chips, assembler, sockets, control ports, `.d81`) and Thing1500 (VIA 6522, IEC bus, `.d64`).
  126 tests of the chips and of the assembler passed on the library alone.
- 2026-10-18 (evening): `IecListener`, `CassettePort`, `TapImage`, the CIA's FLAG input, `MOS8520` and `Wd1770` with their tests (166 in all).
  Thing64 moved onto the library: `Thing64.Mos` and `Thing64.Asm` are gone and 556 of its tests pass on this code.
  ThingCommodore (the drives, the C2N and the MPS-803 printer) is built on it.

## Next

1. **Migrate Thing65**: delete its copies of `Mos`, `Asm`, `Socket`, `IecBus` and `D81`, and reference the library, as Thing64 and ThingCommodore do.
2. **Move the generic cables and ports** (audio, video, keyboard, speakers): see OPEN-POINTS 4 and 5.
3. **Move `Socket`, `OpenCollectorLine`, `Keyboard` and the cables of the C64 board** into the library (the board has its own copies).
4. **A script that packs** (`tools/Pack.ps1`): build, test, bump the version, pack into the local feed.

## Then

The point of a cable is that nothing knows the machine on the other side. Devices that are still to be made (in ThingCommodore):

- Other printers (MPS-801, the 1520 plotter), the 1571 and the 1541-II.
- **Joysticks, paddles, mice, light pens** as stand-alone devices on a control port.
- **User port + ESP32**: the user port as a real connector (8 data lines, handshake, serial) bridged to an ESP32 for home
  automation; the same bridge could serve a real C64.
- A generic **ThingElectroEmu** layer for discrete electronics (lines, gates, timers), only if the devices above need it; until
  then it is a folder of this package.

## Docs and tooling

- A page per chip with the datasheet reference and the test that checks it.
