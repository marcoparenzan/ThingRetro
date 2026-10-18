# Architecture

## Boards, not shortcuts

Every machine here is built like the real one: a board owns sockets, the chips are inserted in them, and the traces connect the pins.
What a machine needs from the world is a connector, and the other end of it is somebody else's business.

| Machine | CPU | Chips | Connector | What the other end sees |
| --- | --- | --- | --- | --- |
| 1541 | 6502 | 2 x VIA 6522, mechanics (`Mechanism1541`, GCR) | IEC | ATN, CLK, DATA |
| 1581 | 6502 | 8520, WD1770, mechanics (`Mechanism1581`) | IEC | ATN, CLK, DATA |
| C2N | none | motor, heads, keys (`Datasette`) | `CassettePort` | MOTOR, WRITE, SENSE, READ |
| MPS-803 | none (the protocol and the printer engine) | `IecListener` | IEC | ATN, CLK, DATA |

## Clocks

Nothing runs by itself: the host calls a method once per cycle of the computer's clock (`Board.AfterCycle`).

- A drive has its own clock, 1 MHz for the 1541 and 2 MHz for the 1581: `DriveClock(computerHz, drive.Tick, driveHz)` turns the cycles of the
  computer (985,248 Hz PAL) into cycles of the drive, so that the two machines stay in step to the cycle (the IEC protocol is timed in µs).
- The C2N and the printer are ticked with the computer's clock: the tape works in cycles of the computer (the `.tap` format counts them), and the
  printer's EOI timing is in µs.
- Several devices on one computer: the host puts the ticks in one delegate (`WebComputer.Rewire` in Thing64.WebComputer does it, and makes the IEC cable
  again with each device on it).

## The IEC cable

`IecBus` has three open collector lines. Each device calls `Plug(cable)` and drives or reads them; none knows the others. A 1541, a 1581 and a printer
share the cable, each with its device number. `Unplug()` takes a device off (the cable is made again by the host).

- The 1541 and the 1581 gate DATA with ATN in hardware: the 1541 with an XOR (PB4 = ATNA), the 1581 with a NAND (PB4 = 1 means DATA goes down with
  ATN; the DOS clears it when it has taken over).
- The printer is a listener at the level of the protocol (`IecListener`, in ThingRetroEmu): bytes, EOI, LISTEN/UNLISTEN, secondary addresses. It has no CPU.

## The 1581

- Memory: RAM at `$0000-$1FFF`, the 8520 at `$4000`, the WD1770 at `$6000`, the 32 KB DOS ROM at `$8000`.
- Port A: PA0 side select, PA1 /READY, PA2 /MOTOR, PA3-PA4 device number, PA5 power LED, PA6 activity LED, PA7 /DISK CHANGE. Port B as in the 1541
  except the device number: PB6 /WRITE PROTECT, PB5 fast serial direction. FLAG is ATN. INTRQ and DRQ are not wired: the DOS reads the status.
- The WD1770 works at sector level (`ThingRetroEmu.Chips.Wd1770`): the commands find a sector and move its 512 bytes at 32 µs each. Read Address gives the
  ID field with its CRC, because the DOS checks it in software.
- /DISK CHANGE: low when a disk was put in; a step of the head with a disk in clears it. This is how the DOS finds out that there is a disk.

## The C2N

`Datasette` has a tape (`TapImage`), five keys and the cassette connector. PLAY sends a falling edge at the end of each pulse of the tape; RECORD measures
the time between the rising edges of the WRITE line, because every pulse the KERNAL writes starts with one. The motor runs only when the computer
switches it on and a key is down: pressing PLAY at the BASIC prompt makes the tape run at once, as on a real C64.

## The MPS-803

`Mps803` receives bytes from `IecListener` and draws dots on `DotPaper`: A4 sheets with 240 dots per inch across (so that the 80 dpi of the character
cells and the 60 dpi of the bit images both fit) and 72 down. `PdfFile` writes the sheets as vector rectangles (neighbouring dots are merged), one
Flate stream per page. The font is the character generator of the computer (the ROM the user provides); the printer's own font is not available.

## Testing

Everything is tested against the real ROMs on a real Thing64 (`Board`): the 1541 and the 1581 are asked to list, load, save and format disks; the
C2N saves a program to a `.tap`, the tape goes through a file and the program is loaded back; the printer is asked to print from BASIC (`OPEN`,
`PRINT#`, `CMD`, `LIST`) and the dots are compared with the character generator. There are also tests of each part alone (the tape format, the
recording, the PDF).
