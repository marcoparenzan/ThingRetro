# Configuration folders

`D:\Configurations\Thing64` and `D:\Configurations\Thing65` are **not versioned**: the studios load their projects from there and
the folders may contain connection strings and other secrets. Their content is never copied to a repository. Only the map is here.

## D:\Configurations\Thing64

| Item | What | Git | Where it should end up |
| --- | --- | --- | --- |
| `Studio\Projects\Daytona` | Copy of the Daytona game | own `.git` | `D:\Dev\2026\Commodore64\Daytona` (repo `marcoparenzan/Daytona`): decide which copy is the master |
| `Studio\Projects\XMasDev2026` | `.asm` sources of the XMasDev 2026 demo (last change 2026-10-13) | no (`.ralfai` folder) | its own repo `XMasDev2026` (renamed from DemoNatale2026) |
| `Studio\Projects\xmasdev-c64-story` | The talk's story | no | its own repo |
| `Studio\Projects\FanMission` | Studio project with `thing64.json`, `PLATFORM.md`, `_structure.md` (2026-10-15) | no (`.ralfai` folder) | undecided |
| `2026.10.14 FanMission.zip` | Snapshot of FanMission | | archive |

## D:\Configurations\Thing65

| Item | What |
| --- | --- |
| `Studio\Projects\Hello World` | The only project |

## In D:\Dev\2026\Commodore64

| Folder | Git | Note |
| --- | --- | --- |
| `Daytona` | yes, GitHub | |
| `DemoNatale2026` | no | Same demo as `XMasDev2026` before the rename; `src` modified 2026-10-12, one day before the copy in Configurations: the two have diverged |
| `xmasdev-c64-story` | no | |

## To decide

1. Which copy of the demo is the master (`DemoNatale2026` or `Configurations\...\XMasDev2026`).
2. Whether the studio should load projects from `D:\Dev` repositories (a `projects` setting) instead of `D:\Configurations`.
3. Whether Thing65's studio gets its own projects folder or shares Thing64's.
