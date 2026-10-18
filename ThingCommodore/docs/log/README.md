# Log of the Commodore family

The cross-project log of the repositories next to this one. It holds no code: it says where everything is, what state it is in and what was decided, so
that each repository can keep its own independent files. (The code of this repository, the peripherals, is described in [../../README.md](../../README.md).)

| Repository | What | Local folder |
| --- | --- | --- |
| [ThingMOS](https://github.com/marcoparenzan/ThingMOS) | The MOS Technology and CSG chips, and the assembler | `D:\Dev\2026\Commodore64\ThingMOS` |
| [ThingRetroEmu](https://github.com/marcoparenzan/ThingRetroEmu) | Sockets, buses, media, video and audio outputs, control ports, WinForms controls | `D:\Dev\2026\Commodore64\ThingRetroEmu` |
| [Thing64](https://github.com/marcoparenzan/Thing64) | C64 emulator, desktop app, web studio | `D:\Dev\2026\Commodore64\Thing64` |
| Thing65 | C65 emulator, desktop app, web studio | `D:\Dev\2026\Commodore64\Thing65` |
| ThingCommodore | The peripherals: 1541, 1581, C2N, MPS-803 (this repository) | `D:\Dev\2026\Commodore64\ThingCommodore` |
| Daytona | C64 racing game | `D:\Dev\2026\Commodore64\Daytona` |
| DemoNatale2026 / XMasDev2026 | XMasDev 2026 demo | see [CONFIGURATIONS.md](CONFIGURATIONS.md) |
| xmasdev-c64-story | Story for the XMasDev talk | see [CONFIGURATIONS.md](CONFIGURATIONS.md) |

## Files

- [STATUS.md](STATUS.md): where each project is. Updated at the end of every working session.
- [SHARED-CODE.md](SHARED-CODE.md): which code is shared, who depends on whom, and what still has to move.
- [CONFIGURATIONS.md](CONFIGURATIONS.md): map of `D:\Configurations\Thing64` and `Thing65` (not versioned; their content is never copied here).
- [DECISIONS.md](DECISIONS.md): decisions with date and reason.

## Conventions

- Code, comments, UI and docs of the repositories are in English; the conversation with Claude is in Italian.
- Every project has README, ARCHITECTURE, ROADMAP/OPEN-POINTS and `tools/*.ps1` scripts.
- ROMs are never versioned: each repository has `tools/Install-Roms.ps1` that copies them from C64 Forever.
- No `Co-Authored-By` trailers in commits.
- Development ports: 5064 Studio 64, 5164 Web 64, 5065 Studio 65, 5165 Web 65 (the `launchSettings.json` of each project).
