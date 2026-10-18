# Shared code

## Today (2026-10-18, evening)

```text
ThingRetroEmu  (chips, sockets, buses, media, assembler)   <- ProjectReference, package 0.3.0 in D:\Dev\NuGetLocalFeed
    ^         ^              ^
 Thing64   Thing65   ThingCommodore  (1541, 1581, C2N, MPS-803)
    |                      ^
    +----------------------+  Thing64.Computer and Thing64.WebComputer reference ThingCommodore (so does Thing65.Computer)
```

- **Thing64** has no chips of its own any more: `Thing64.Mos` and `Thing64.Asm` were removed and the board uses `ThingRetroEmu.Chips`.
  The only things the board adds are its own `Socket`, `OpenCollectorLine`, `Keyboard`, `ControlPort`s and the cable classes (to be moved, see OPEN-POINTS of ThingRetroEmu).
- **Thing65** still has `Thing65.Mos` and `Thing65.Asm` (the superset the library was made from). It uses ThingCommodore for the 1541 window.
  Moving it to the library is the next migration.
- **ThingCommodore** depends on ThingRetroEmu only; its tests use Thing64 and Thing65 as computers.
- Thing1500 is no longer referenced by anyone.

## Rules while the migration is open

- A fix to a shared chip goes in **ThingRetroEmu** first; `Thing65.Mos` and `Thing65.Asm` are not touched any more.
- Anything noticed in a copy that is not in the library yet is added to the OPEN-POINTS of ThingRetroEmu.

## History

On 2026-10-18 the chip code of Thing64 and Thing65 was compared: `MOS6526`, `MOS6581`, `MOS6569` and `MOS6502.Instructions` were identical line for line.
Thing65 had the `SO` pin on the 6502 (a falling edge sets V, used by the 1541), the interfaces of the C65 chips in `Pinouts.cs`, a ninth keyboard line
and the CPU parameter of the assembler. All of it went into ThingRetroEmu.
