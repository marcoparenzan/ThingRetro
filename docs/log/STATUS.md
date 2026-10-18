# Status

Last update: 2026-10-19.

| | Thing64 | Thing65 | ThingCommodore | ThingMOS | ThingRetroEmu |
| --- | --- | --- | --- | --- | --- |
| What | C64 emulator (ex Fake64) with web studio and desktop app | C65 emulator, started as a copy of Thing64 | Peripherals: 1541, 1581, C2N, MPS-803 | MOS and CSG chips, assembler | Sockets, buses, media, video and audio outputs, control ports, WinForms controls |
| Remote | `main`, GitHub (public) | `main`, GitHub (private) | `main`, GitHub (private) | local only | `main`, GitHub (private) |
| State | C64 complete. No chips, assembler, monitor, speakers or joysticks of its own: they come from ThingMOS and ThingRetroEmu. Web page: 1541, 1581, C2N, MPS-803 | C65 works (4510, VIC-III, DMAgic, F011, BASIC 10). Moved onto ThingMOS and ThingRetroEmu like Thing64 | 1541, 1581 (original DOS: list, load, save, format), C2N (save and load a `.tap`), MPS-803 (PDF) | 0.1: 12 chips and the assembler; 140 tests | 0.5: 37 tests (WD1770, disk and tape images, IEC bus, `.prg`/`.crt`, video, audio) |
| Next | Push the history without ROMs (see below); desktop windows for the new devices | Nothing planned for the new peripherals (the user does not need them in Thing65) | Desktop windows; 1571; `.g64` | 6507, 6532 RIOT (Atari 2600); 65C02 | Move the C64 board's `Socket`, `Keyboard`, cartridges; Pack.ps1 |

## Pending actions

1. **Thing64 on GitHub still has the old history with the ROMs.** The local history is clean (ROMs removed on 2026-10-18, backup bundles in
   `D:\Dev\2026\__backup__\commodore-pre-rom-purge`). It needs a force push of `main`, or deleting and re-creating the repository, to clear the cached
   commits. Thing64 has many local commits that are not pushed.
2. **ThingMOS has no GitHub repository yet** (create it and push); Thing65, ThingRetroEmu and ThingCommodore have changes that are not committed.
3. The web pages (Thing64.WebComputer, Thing65.WebComputer) build but the new controls and the canvas that follows the size of the picture have not been tried in a browser.
4. Decide what `DemoNatale2026`, `XMasDev2026` and `xmasdev-c64-story` become ([CONFIGURATIONS.md](CONFIGURATIONS.md)).

## Other projects in the same folder

| Project | State |
| --- | --- |
| Daytona | MVP roadmap complete (12 tasks, GoatTracker music); GitHub `marcoparenzan/Daytona` |
| DemoNatale2026 | 8-step roadmap complete on 2026-08-28; talk content is to be finalized in another folder (XMasDev2026) |
| xmasdev-c64-story | Story documents, charpad notes, build scripts; no git |
