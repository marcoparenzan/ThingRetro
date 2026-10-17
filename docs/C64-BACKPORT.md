# Thing65 and the Thing64 code: what was touched

Thing65 started as a copy of Thing64. The C64 core (6502/6510, CIA, VIC-II, SID, board) is still here and will be
reused, so changes made to it, and problems noticed in it while building the C65, are recorded in this file:
anything listed here may deserve the same fix in Thing64.

## Changes to code that came from Thing64

| Date | Where | What | Needed in Thing64? |
| --- | --- | --- | --- |
| 2026-10-17 | `Pinouts.cs` | Added `IMappedBus` and `I4510Pinout` (new interfaces, nothing existing modified) | No |
| 2026-10-17 | `Thing65.Computer/AppSettings.cs` | Removed the one-time read of the old `Fake64` settings folder | No |
| 2026-10-17 | `Pinouts.cs` | Added `IVic3Memory`, `I4567Pinout`, `IF018Pinout`, `IF011Pinout`, `IDiskMedia`, `IMappedBus`, `I4510Pinout` | No |
| 2026-10-17 | `Keyboard.cs` | A ninth line for the C65's extra keys (`C65Key.NoScroll`...`Escape`, `GetExtendedKeyboardState`); the 8x8 matrix is unchanged | No |
| 2026-10-17 | `ControlPortSelector.cs`, `C65Monitor.cs` (WinForms) | The selector takes two `ControlPort`s; the monitor takes the frame size (default: the VIC-III's) | No |
| 2026-10-17 | `Thing65.Asm` | `Assemble(..., cpu)`, `!cpu`, a 4510 table; the 6510 path is unchanged (default) | No |

## Problems noticed in the C64 code

None so far. The C65 work found one bug of its own that is worth remembering when the C64 core is compared with it: `PHW`
pushes the low byte first (it was wrong in `MOS4510` until BASIC 10 failed to restore its text pointer); the 6502 core has no
such instruction.

## Things the C65 shares with the C64 core

- The C65 board runs the 6526 CIAs, the 6581 SIDs and the VIC-II compatibility on the same chip code as `Board`: a fix there
  helps both.
- `Thing65.WebComputer` and `Thing65.WebStudio` still use the C64 `Board` (see [ROADMAP.md](ROADMAP.md)).

(When something is found: file, what is wrong, how it shows, and whether it was fixed here.)

## Changes made for Thing1500 (the disk drives)

- `Pinouts.cs`: new `I6502Pinout` (the 6502 without the port); `MOS6502` implements it. No behavior change: the same
  change would do in Thing64 if the drives are to share its `Mos` library.
- `MOS6502`: `SoInput` pin (a falling edge sets V), also in `I6502Pinout`. The 1541 uses it for "byte ready".
