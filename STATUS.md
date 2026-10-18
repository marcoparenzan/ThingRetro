# Status

Last update: 2026-10-18.

| | Thing64 | Thing65 | Thing1500 | ThingRetroEmu |
| --- | --- | --- | --- | --- |
| What | C64 emulator (ex Fake64) with web studio and desktop app | C65 emulator, started as a copy of Thing64 | The Commodore disk drives, on an IEC cable | Chips, sockets, buses, media, assembler |
| Branch / remote | `main`, GitHub (public) | `main`, GitHub (private) | `master`, GitHub (private) | `main`, local only |
| State | C64 complete; 1541 on the IEC cable (desktop and web); screen capture, charset editor, memory viewer | C65 works (4510, VIC-III, DMAgic, F011, BASIC 10); web runs the C65, web studio still has C64 texts | 1541 works: DOS, GCR mechanics, writing to `.d64`, `LOAD`/`SAVE` from BASIC with Thing64 and Thing65 | 0.1: extracted from Thing65 and Thing1500; 126 tests pass |
| Next | Push the history without ROMs (see below); web drive | Editor and 40/80 column editors for the 4510; use ThingRetroEmu | 1581 (CIA 8520, WD1770), `.g64`, 1541-II, 1571 | Migrate the three consumers |

## Pending actions

1. **Thing64 on GitHub still has the old history with the ROMs.** The local history is clean (ROMs removed on 2026-10-18,
   backup bundles in `D:\Dev\2026\__backup__\commodore-pre-rom-purge`). It needs a force push of `main`, or deleting and
   re-creating the repository, to clear the cached commits.
2. Migrate Thing65, Thing1500 and Thing64 to the ThingRetroEmu package ([SHARED-CODE.md](SHARED-CODE.md)).
3. Decide what `DemoNatale2026`, `XMasDev2026` and `xmasdev-c64-story` become ([CONFIGURATIONS.md](CONFIGURATIONS.md)).

## Other projects in the same folder

| Project | State |
| --- | --- |
| Daytona | MVP roadmap complete (12 tasks, GoatTracker music); GitHub `marcoparenzan/Daytona` |
| DemoNatale2026 | 8-step roadmap complete on 2026-08-28; talk content is to be finalized in another folder (XMasDev2026) |
| xmasdev-c64-story | Story documents, charpad notes, build scripts; no git |
