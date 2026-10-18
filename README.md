# ThingCommodore

The cross-project log of the Commodore family of repositories. It holds no code: it says where everything is, what state it is
in and what was decided, so that each repository can keep its own independent files.

| Repository | What | Local folder |
| --- | --- | --- |
| [ThingRetroEmu](../ThingRetroEmu) | Chips, sockets, buses, media and assembler (package) | `D:\Dev\2026\Commodore64\ThingRetroEmu` |
| [Thing64](https://github.com/marcoparenzan/Thing64) | C64 emulator, desktop app, web studio | `D:\Dev\2026\Commodore64\Thing64` |
| Thing65 | C65 emulator, desktop app, web studio | `D:\Dev\2026\Commodore64\Thing65` |
| Thing1500 | Disk drives as stand-alone machines on the IEC cable | `D:\Dev\2026\Commodore64\Thing1500` |
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
