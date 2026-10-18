# ThingMOS

The chips of **MOS Technology** (and of its successor **CSG**, which made the C65's custom chips) as C# components, written to be read: each chip
has a **pinout** (the interface that is the contract of its socket) and does not know which computer it is mounted in. With them, the **assembler** of
their instruction sets.

It is the chip library under [Thing64](https://github.com/marcoparenzan/Thing64) (C64), Thing65 (C65) and ThingCommodore (the drives). The boards, the
sockets, the buses and the chips of other makers (the Western Digital WD1770, for example) are in
[ThingRetroEmu](https://github.com/marcoparenzan/ThingRetroEmu).

| Chip | Class | Pinout | What |
| --- | --- | --- | --- |
| MOS 6502 | `MOS6502` | `I6502Pinout` | CPU of the drives; cycle-stepped, all 256 opcodes (undocumented ones too), `SO` pin |
| MOS 6510 | `MOS6510` | `I6510Pinout` | the C64's CPU: a 6502 with an I/O port |
| CSG 4510 | `MOS4510` | `I4510Pinout` | the C65's CPU: a 65CE02 with a memory mapper that reaches 1 MB |
| MOS 6526 | `MOS6526` | `I6526Pinout` | CIA: two ports, two timers, serial register, time of day, FLAG |
| MOS 8520 | `MOS8520` | `I6526Pinout` | the Amiga's and the 1581's CIA (event counter instead of the time of day) |
| MOS 6522 | `Via6522` | `IVia6522Pinout` | VIA: two ports, two timers, handshake lines (the 1541) |
| MOS 6581 | `MOS6581` | `I6581Pinout` | SID: three voices, filter |
| MOS 6569 | `MOS6569` | `I6569Pinout` | VIC-II (PAL): all graphics modes, sprites, raster, bus DMA |
| CSG 4567 | `CSG4567` | `I4567Pinout` | VIC-III: VIC-II compatible plus 80 columns, 256 colors, bitplanes |
| CSG F018 | `F018` | `IF018Pinout` | DMAgic: the C65's DMA controller |
| CSG F011 | `F011` | `IF011Pinout` | the C65's floppy controller |
| (CSG) | `C65Uart` | `IUartPinout` | the C65's UART |

`Asm`: the assembler, with ACME-compatible syntax: 6502, 6510 and the whole 4510 set (`!cpu`), and the Monaco syntax file for the editors.

## Build and test

```powershell
dotnet test tests/ThingMOS.Tests
dotnet pack src/ThingMOS -c Release -o D:\Dev\NuGetLocalFeed
```

The tests are those of the chips that need no motherboard (140 at the time of writing: the 6502 cycle by cycle, the CIA and its timers, the 6522, the VIC-III,
the DMAgic, the floppy tracks, both assemblers). The ones that boot a ROM stay with the computers, which also run Klaus Dormann's 6502 test and the
SingleStepTests. Consumers reference the project (`ProjectReference`) or the package from the local feed.

## Documents

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md): the rules of a chip and how a board uses one.
- [docs/OPEN-POINTS.md](docs/OPEN-POINTS.md): what is undecided or known to be wrong.
- [docs/ROADMAP.md](docs/ROADMAP.md): what comes next, including the machines a 6502 can drive besides the Commodores.
