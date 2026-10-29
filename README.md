# ThingRetro

The Thing emulators in one repository: chips, sockets and buses as components, the machines built with them, the peripherals, and (to come) the studio. Written to be
read: the code mirrors the hardware, with the chips in their sockets, the traces that connect them and the cables to the monitor, the speakers and the keyboard.

| Folder | What |
| --- | --- |
| [ThingMOS](ThingMOS) | The MOS Technology and CSG chips (6502, 6510, 4510, 6526, 8520, 6522, 6581, 6569, VIC-III, DMAgic, F011) and the assembler |
| [ThingRetroEmu](ThingRetroEmu) | Everything generic: sockets, buses (IEC), media (`.d64` `.d81` `.tap` `.prg` `.crt`), video (RGB and composite) and audio outputs, control ports, the WD1770, and the WinForms controls |
| [ThingCommodore](ThingCommodore) | The Commodore peripherals (1541, 1581, C2N, MPS-803), the cartridges, the loaders, the BASIC tokenizer, the graphics and build tools; and the log of the family in `docs/log` |
| [Thing64](Thing64) | The Commodore 64: the board, the desktop app, the command line, the web computer and the web studio |
| [Thing65](Thing65) | The Commodore 65: the board, the desktop app, the command line, the web computer and the web studio |
| [ThingNES](src/ThingNES) | NES NTSC foundation (NROM/UxROM/CNROM): headless CPU runner, functional background/sprite renderer, PPM frame export, controllers, OAM DMA and functional pulse/triangle/noise/DMC audio with WAV export; Windows desktop frontend with keyboard controller and pause/frame-step; opt-in live Windows audio with bounded buffering |
| [ThingOS64](ThingOS64) | ThingOS, a windowing operating system for the C64 in place of its KERNAL, BASIC and character ROMs: desktop in character mode, 1351 mouse or joystick, disk only (no BASIC, no tape) |

The projects reference each other by relative paths (`..\..\..\ThingMOS\src\...`), which is why they live side by side. `ThingMOS` depends on `ThingRetroEmu`; the
machines and `ThingCommodore` depend on both.

## Build, test, run

```powershell
./tools/Install-Roms.ps1                    # the ROMs from C64 Forever Free Express Edition (not versioned: copyrighted)
dotnet build ThingRetro.slnx
dotnet test ThingMOS/tests/ThingMOS.Tests
dotnet test ThingRetroEmu/tests/ThingRetroEmu.Tests
dotnet test ThingCommodore/tests/ThingCommodore.Tests
dotnet test Thing64/src/Thing64.Tests
dotnet test Thing65/src/Thing65.Tests
dotnet test ThingOS64/src/ThingOS64.Tests
```

Development ports: 5064 Studio 64, 5164 Web 64, 5065 Studio 65, 5165 Web 65.

## Where to read next

- Each folder has its own README, ARCHITECTURE and ROADMAP or OPEN-POINTS.
- [ThingCommodore/docs/log](ThingCommodore/docs/log/README.md): status of every project, what is shared, the map of the configuration folders and the decisions.

## Conventions

- Code, comments, UI and docs are in English.
- The ROMs are never versioned. `D:\Configurations\...` (the studios' projects, which may hold secrets) is outside the repository.
- No `Co-Authored-By` trailers in commits.
