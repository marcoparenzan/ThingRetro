# Roadmap

## Done

- 2026-10-18: extracted from Thing65 (sockets, control ports, `.d81`) and Thing1500 (VIA 6522, IEC bus, `.d64`).
- 2026-10-18 (evening): `IecListener`, `CassettePort`, `TapImage`, `Wd1770`; Thing64 and ThingCommodore built on it.
- 2026-10-18 (night): the MOS and CSG chips and the assembler moved to ThingMOS; Thing65 moved onto both libraries.
- 2026-10-19: composite video (`VideoStandard`, `CompositeFrame`, encoder, decoder, tuner) next to the RGB connector, with 14 tests; the video and audio outputs (`VideoMode`, `IVideoSource`, `IMonitor`, `VideoPort`, `ISpeaker`, `AudioPort`) and the formats `.prg` and `.crt`
  moved here from Thing64 and Thing65; `ThingRetroEmu.WinForms` has the monitor, speakers, joysticks and mouse paddles for both.

## Next

1. **Composite from the chips**: the VIC-II and VIC-III are encoded from their RGB picture (the palette goes through RGB to Y, U, V and back); a native signal that builds the chroma from the palette's phases would give the real artifacts of the C64. A `TIA` and an Apple II video circuit would implement `ICompositeSource` directly.
2. **A switch in the apps**: RGB or composite (PAL or NTSC) in the context menu of the desktop apps and in the web page. The monitor controls need no change: `new CompositeTuner(monitor)`.
3. **S-Video and RGB signals** as connectors of their own (Y and C on two wires; red, green and blue), and an interlaced field.
4. **Pixel aspect of the C64**: the VIC-II frame is declared with square dots (as the monitors showed it until now); the real PAL dots are 0.936 wide.
5. **A script that packs** (`tools/Pack.ps1`): build, test, bump the version, pack into the local feed.
6. **The Disk II controller** (for an Apple II) and other makers' chips as they are needed.

## Then

- A web monitor (the canvas of the page) as a project of its own, shared by every emulator's web page.
- Joysticks, paddles, mice, light pens as stand-alone devices on a control port, shared between emulators.
- User port + ESP32 for home automation.
