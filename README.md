# ThingRetroEmu

The retro hardware as C# components, written to be read: **chips** with their pinouts, the **sockets** they are inserted
in, the **buses and cables** between machines, the **disk media** and the **assembler**. It is the library under
Thing64 (C64), Thing65 (C65) and ThingCommodore (the disk drives, the cassette recorder, the printer); none of its parts knows which computer or drive it is
mounted in.

> **Status: 0.3.** The code was extracted from Thing65 and Thing1500 on 2026-10-18 (the chips shared byte for byte with
> Thing64). Thing64 and ThingCommodore (the drives, the recorder and the printer) use it; Thing65 still carries its own copies: moving it
> is the first item of the [roadmap](docs/ROADMAP.md).

## What is in it

| Folder / namespace | Content |
| --- | --- |
| `Chips` | MOS 6502 and 6510, CSG 4510 (65CE02), CIA 6526 and 8520, SID 6581, VIC-II 6569, VIC-III (CSG 4567), VIA 6522, WD1770, DMAgic F018, floppy controller F011, C65 UART, RAM expansion, ROM cartridge, `ParallelPort`, and `Pinouts.cs`: the interface each chip implements, which is the contract of its socket |
| `Sockets` | `Socket<TPinout>`: a socket accepts any object that implements the pinout; `OpenCollectorLine` |
| `Buses` | `IecPort` (the connector of a computer or drive), `IecLine` and `IecBus` (the serial cable: ATN, CLK and DATA, open collector), `IecListener` (a device that only listens on the cable, at the level of the protocol: the base of a printer) |
| `Media` | `.d64` (`D64Image`, `D64Writer`, GCR tracks), `.d81` (`D81Image`, `D81Writer`) and `.tap` (`TapImage`) |
| `Ports` | Control ports: `ControlPort`, `Joystick`, `Paddles`, `Mouse1351`; the cassette connector `CassettePort` |
| `Asm` | The assembler: ACME-compatible syntax, 6502, 6510 and the whole 4510 set (`!cpu`), plus the Monaco syntax file |

## Build and test

```powershell
dotnet test tests/ThingRetroEmu.Tests
dotnet pack src/ThingRetroEmu -c Release -o D:\Dev\NuGetLocalFeed
```

The tests are those of the chips and of the assembler that need no motherboard (166 at the time of writing). The ones that boot
a ROM stay with the computers. Consumers reference the package from the local feed `D:\Dev\NuGetLocalFeed` (or, while a chip is
being developed, the project with a `ProjectReference`).

## Documents

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md): how chips, sockets, buses and cables fit together, and the rules of the library.
- [docs/OPEN-POINTS.md](docs/OPEN-POINTS.md): what is undecided or known to be wrong.
- [docs/ROADMAP.md](docs/ROADMAP.md): what comes next, from migration to the C2N, the printer and the ESP32 user port.
- [docs/CHIPS.md](docs/CHIPS.md): one line per chip: who uses it, what is missing.

The system ROMs are copyrighted and are not part of this repository; the computers' repositories explain how to install them.
