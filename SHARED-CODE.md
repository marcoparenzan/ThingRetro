# Shared code

## Today

Thing65 started as a copy of Thing64, and Thing1500 uses the chips of Thing65. On 2026-10-18 the chip code of the two computers was
compared: `MOS6526`, `MOS6581`, `MOS6569` and `MOS6502.Instructions` are identical line for line (apart from the namespace).

| File | Difference between Thing64 and Thing65 |
| --- | --- |
| `MOS6502.cs` | Thing65 adds the `SO` pin (`SoInput`: a falling edge sets V, used by the 1541) and implements `I6502Pinout` |
| `Pinouts.cs` | Thing65 adds the interfaces of the C65 chips (`I4510Pinout`, `IVic3Memory`, `I4567Pinout`, `IF018Pinout`, `IF011Pinout`, `IDiskMedia`, `IMappedBus`) |
| `Keyboard.cs` | Thing65 has a ninth line for the C65's extra keys |
| `Thing65.Asm` | Thing65 takes a CPU parameter (`!cpu`) and has a 4510 table |
| `ControlPortSelector`, `C65Monitor` | Thing65 takes two control ports and a frame size |

The log of the changes made in Thing65 to code that came from Thing64 is [Thing65/docs/C64-BACKPORT.md](../Thing65/docs/C64-BACKPORT.md);
it is replaced by this file once the library exists.

## Target

```
ThingRetroEmu  (chips, sockets, buses, media, assembler)   <- package, D:\Dev\NuGetLocalFeed
    ^         ^          ^
 Thing64   Thing65   Thing1500   (boards, apps, studios, drive mechanics)
```

- ThingRetroEmu 0.1.0 has the Thing65 superset (the chips of both computers) plus the VIA 6522, the IEC bus and the `.d64` of Thing1500.
- Thing1500 still references `..\Thing65\src\Thing65.Mos` until it moves to the package.
- Order of migration: Thing65, Thing1500, Thing64.

## Rules while the migration is open

- A fix to a shared chip goes in **ThingRetroEmu** first; the two old copies are not touched any more.
- Anything noticed in a copy that is not in the library yet is added to the ThingRetroEmu OPEN-POINTS.
