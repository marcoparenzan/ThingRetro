# Thing65 roadmap and deferred work

## Done (2026-10-17)

CSG 4510, DMAgic, VIC-III, F011 and `.d81`, FAST mode, C65 keyboard, `BoardC65` booting the 910828 ROM to BASIC 10, the desktop
window and `thing65 run`/`asm` on it, the 4510 assembler, the C64 mode, four samples in `samples/c65`. Decided then: command line and WinForms only; the web is deferred.

## Next, in no particular order

- An editor/hover table for the 4510 opcodes (the studio is deferred with the web).
- The C64 mode (`GO64`) boots and runs BASIC V2 (tested); only its details are untested (`FRE(0)` printed nothing).
- UART, RAM expansion, CAPS LOCK; the 80-column bad-line cycles; HPOS/VPOS.
- Retire the old C64 `Board` once nothing needs it (its tests move to `BoardC65` in C64 mode).

## Deferred

- **Web** (`Thing65.WebComputer`, `Thing65.WebStudio`): still the Thing64 C64 board, not touched. When it is wanted:
  `WebComputer` must host `BoardC65` instead of `Board`, the canvas size follows the VIC-III output size, the web
  keyboard layout needs the C65 keys, the studio templates and the Monaco 6502 syntax file need the 4510 opcodes.
- 40/80 column text editors, sprite and charset editors of the studio: made for the VIC-II modes.
- Samples (`samples/`): all C64 programs; C65 samples will be added with the assembler support.
- F011 free-format track commands (RDT, WTT), DMAgic MIX/SWAP/XY_MOD and interrupt yielding.
- RAM expansion controller (`$D0A0`), UART (`$D600`), cartridge port ($40000-$7FFFF).
