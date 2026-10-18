# Roadmap

## Done

- 2026-10-18: extracted from Thing65 (chips, assembler, sockets, control ports, `.d81`) and Thing1500 (VIA 6522, IEC bus,
  `.d64`). 126 tests of the chips and of the assembler pass on the library alone.

## Next

1. **Migrate the consumers.** Thing65 first (it is the superset), then Thing1500, then Thing64: delete their copies of `Mos`,
   `Asm`, `Socket`, `IecBus`, `D64`/`D81`, and reference the package from `D:\Dev\NuGetLocalFeed`.
2. **Bring Thing64 up to date** with what Thing65 added to the shared code: the `SO` pin and `I6502Pinout`, the 4510 table in
   the assembler.
3. **Move the generic cables and ports** (audio, video, keyboard, speakers): see OPEN-POINTS 4 and 5.
4. **CIA 8520 and WD1770** for the 1581 (Thing1500).

## Then: devices on their own

The point of the IEC cable is that nothing knows the machine on the other side. The same should be true of:

- **C2N cassette recorder**: a device with a cassette port (motor, read, write, sense) working on a `.tap`; a Thing64 or a
  Thing65 plugs into it, and so can anything else.
- **Printer**: on the IEC bus, with the same bus as the drives. If it shares the C2N mechanism, it follows the C2N (to be checked).
- **Joysticks, paddles, mice, light pens** as stand-alone devices on a control port.
- **User port + ESP32**: the user port as a real connector (8 data lines, handshake, serial) bridged to an ESP32 for home
  automation; the same bridge could serve a real C64.
- A generic **ThingElectroEmu** layer for discrete electronics (lines, gates, timers), only if the devices above need it; until
  then it is a folder of this package.

All of them are components of this package until they need a repository of their own.

## Docs and tooling

- `tools/Pack.ps1`: build, test, bump the version, pack into the local feed.
- A page per chip with the datasheet reference and the test that checks it.
