# Open points

1. **Names of the chips.** `Chips` holds every chip, MOS or CSG; `MOS4510`, `MOS6502`... keep their historical names. Rename?
2. **`Pinouts.cs` is one big file.** One file per chip, or one per family?
3. **Board-level pieces.** `Socket<TPinout>` is here; the boards (`Board`, `BoardC65`, `Board1541`) are in the computer repos and
   each wires its own sockets. Is there a common base class?
4. **Audio, video and keyboard cables** (`AudioCable`, `MonitorCable`, `Keyboard`, `ISpeaker`, `IScreen`) are still in the C64
   and C65 projects. They are generic: move them to `Ports`?
5. **`Keyboard`**: Thing65's has a ninth line for the C65's extra keys, Thing64's has 8. One class with the extension?
6. **`Thing64.Basic`** (the BASIC tokenizer) is a second language tool and could move next to `Asm`.
7. **IEC is a standard.** The same bus serves drives, printers and the plotter; the cassette and the user port are different
   connectors. Define the family of port and cable interfaces once, before the C2N.
8. **Package or project reference.** The package is cut by hand into `D:\Dev\NuGetLocalFeed`; a script (`tools/Pack.ps1`) that
   bumps the version and refreshes the consumers is still to do.
9. **One package or several?** `Chips`, `Buses` and `Asm` are separable if the package becomes too large (decided on
   2026-10-18: one).
10. **Tests that boot a ROM** stay with the computers; only chip-level and assembler tests live here. The helpers they share
    (`TestHelpers`) are tied to `Board`.
