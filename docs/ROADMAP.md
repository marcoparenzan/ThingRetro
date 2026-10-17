# Thing65 roadmap and deferred work

## Current focus (decided 2026-10-17)

Command line and WinForms desktop app only. In order: VIC-III, FAST mode, C65 keyboard, `thing65 run` and the
desktop window on `BoardC65`, 4510 support in the assembler.

## Deferred

- **Web** (`Thing65.WebComputer`, `Thing65.WebStudio`): still the Thing64 C64 board, not touched. When it is wanted:
  `WebComputer` must host `BoardC65` instead of `Board`, the canvas size follows the VIC-III output size, the web
  keyboard layout needs the C65 keys, the studio templates and the Monaco 6502 syntax file need the 4510 opcodes.
- 40/80 column text editors, sprite and charset editors of the studio: made for the VIC-II modes.
- Samples (`samples/`): all C64 programs; C65 samples will be added with the assembler support.
- F011 free-format track commands (RDT, WTT), DMAgic MIX/SWAP/XY_MOD and interrupt yielding.
- RAM expansion controller (`$D0A0`), UART (`$D600`), cartridge port ($40000-$7FFFF).
