# Status

Last update: 2026-10-18 (evening).

| | Thing64 | Thing65 | ThingCommodore | ThingRetroEmu |
| --- | --- | --- | --- | --- |
| What | C64 emulator (ex Fake64) with web studio and desktop app | C65 emulator, started as a copy of Thing64 | Peripherals: 1541, 1581, C2N, MPS-803 | Chips, sockets, buses, media, assembler |
| Branch / remote | `main`, GitHub (public) | `main`, GitHub (private) | `main`, GitHub (private) | `main`, GitHub (private) |
| State | C64 complete. Uses ThingRetroEmu (Thing64.Mos and Thing64.Asm are gone) and ThingCommodore. Cassette connector on the board. Web page: 1541, 1581, C2N, MPS-803 | C65 works (4510, VIC-III, DMAgic, F011, BASIC 10). Still has its own copies of Mos and Asm | 1541, 1581 (original DOS: list, load, save, format), C2N (save and load a `.tap`), MPS-803 (PDF) | 0.3: chips including 8520 and WD1770, `TapImage`, `IecListener`, `CassettePort`; 166 tests |
| Next | Push the history without ROMs (see below); desktop windows for the new devices | Move to ThingRetroEmu and ThingCommodore | Desktop windows; 1571; `.g64` | Migrate Thing65 |

## Pending actions

1. **Thing64 on GitHub still has the old history with the ROMs.** The local history is clean (ROMs removed on 2026-10-18, backup bundles in
   `D:\Dev\2026\__backup__\commodore-pre-rom-purge`). It needs a force push of `main`, or deleting and re-creating the repository, to clear the cached
   commits. Thing64 has local commits that are not pushed (the web page with the new devices, the cassette, the migration to ThingRetroEmu).
2. Migrate Thing65 to ThingRetroEmu and ThingCommodore ([SHARED-CODE.md](SHARED-CODE.md)).
3. The web page (Thing64.WebComputer) builds but the new controls have not been tried in a browser: the logic is tested in ThingCommodore.Tests only.
4. Delete Thing1500 when ThingCommodore has been used for a while.
5. Decide what `DemoNatale2026`, `XMasDev2026` and `xmasdev-c64-story` become ([CONFIGURATIONS.md](CONFIGURATIONS.md)).

## Other projects in the same folder

| Project | State |
| --- | --- |
| Daytona | MVP roadmap complete (12 tasks, GoatTracker music); GitHub `marcoparenzan/Daytona` |
| DemoNatale2026 | 8-step roadmap complete on 2026-08-28; talk content is to be finalized in another folder (XMasDev2026) |
| xmasdev-c64-story | Story documents, charpad notes, build scripts; no git |
