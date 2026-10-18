# Roadmap

## Done

- 2026-10-18: extracted from Thing65 (sockets, control ports, `.d81`) and Thing1500 (VIA 6522, IEC bus, `.d64`).
- 2026-10-18 (evening): `IecListener`, `CassettePort`, `TapImage`, `Wd1770`; Thing64 and ThingCommodore built on it.
- 2026-10-18 (night): the MOS and CSG chips and the assembler moved to ThingMOS; Thing65 moved onto both libraries.
- 2026-10-19: the video and audio outputs (`VideoMode`, `IVideoSource`, `IMonitor`, `VideoPort`, `ISpeaker`, `AudioPort`) and the formats `.prg` and `.crt`
  moved here from Thing64 and Thing65; `ThingRetroEmu.WinForms` has the monitor, speakers, joysticks and mouse paddles for both.

## Next

1. **Move `Socket`, `OpenCollectorLine`, `Keyboard` and `Ram64KbChip`** of the C64 board into the library (the two boards have their own copies).
2. **The cartridges** (`Cartridge`, `NormalCartridge`, `OceanCartridge`, `EasyFlash`...) are Commodore expansion port devices: they could go to ThingCommodore.
3. **Pixel aspect of the C64**: the VIC-II frame is declared with square dots (as the monitors showed it until now); the real PAL dots are 0.936 wide.
4. **A script that packs** (`tools/Pack.ps1`): build, test, bump the version, pack into the local feed.
5. **The Disk II controller** (for an Apple II) and other makers' chips as they are needed.

## Then

- A web monitor (the canvas of the page) as a project of its own, shared by every emulator's web page.
- Joysticks, paddles, mice, light pens as stand-alone devices on a control port, shared between emulators.
- User port + ESP32 for home automation.
