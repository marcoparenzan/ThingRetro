# Thing65 and the Thing64 code: what was touched

Thing65 started as a copy of Thing64. The C64 core (6502/6510, CIA, VIC-II, SID, board) is still here and will be
reused, so changes made to it, and problems noticed in it while building the C65, are recorded in this file:
anything listed here may deserve the same fix in Thing64.

## Changes to code that came from Thing64

| Date | Where | What | Needed in Thing64? |
| --- | --- | --- | --- |
| 2026-10-17 | `Pinouts.cs` | Added `IMappedBus` and `I4510Pinout` (new interfaces, nothing existing modified) | No |
| 2026-10-17 | `Thing65.Computer/AppSettings.cs` | Removed the one-time read of the old `Fake64` settings folder | No |

## Problems noticed in the C64 code

None so far.

(When something is found: file, what is wrong, how it shows, and whether it was fixed here.)
