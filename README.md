# ThingCommodore

The Commodore peripherals as stand-alone machines, written to be read: each one is a **board** with its chips in sockets, its own
ROM and mechanics, and it is connected to a computer (Thing64, Thing65) only through a **cable** or a **port**. A drive does not
know which computer is on the other end of the IEC cable. The chips, sockets, buses and media they are made of are in
[ThingRetroEmu](https://github.com/marcoparenzan/ThingRetroEmu).

| Folder | Machine | State |
| --- | --- | --- |
| `Drives` | **1541** (6502, two VIAs, GCR mechanics) | the original DOS loads, saves, scratches and formats `.d64` disks |
| `Drives` | **1581** (6502 at 2 MHz, 8520, WD1770) | the original DOS (318045-02) lists, loads, saves and formats `.d81` disks, with the KERNAL of a Thing64 |
| `Tape` | **C2N** cassette recorder (`Datasette`) | `.tap` tapes: SAVE and LOAD through the KERNAL, PLAY, RECORD, rewind, fast forward |
| `Printers` | **MPS-803** dot matrix printer | listens on the IEC cable (device 4 or 5), writes dots on paper and gives a PDF |

> The code of the drives comes from Thing1500, which is left in place until this repository has been used for a while.

## The 1581 in a few lines

```csharp
var drive = new Board1581(File.ReadAllBytes("roms/c-drive-1581.rom")) { DeviceNumber = 8, Disk = d81 };
var cable = new IecBus();
cable.Attach(() => board.Iec.AtnLow(), () => board.Iec.ClkLow(), () => board.Iec.DataLow(),
             input => board.Iec.ClkInputLow = input, input => board.Iec.DataInputLow = input);
drive.Plug(cable);
board.AfterCycle = new DriveClock(985_248, drive.Tick, Board1581.CLOCK_HZ).OnComputerCycle;
```

`LOAD"$",8`, `LOAD"NAME",8`, `SAVE"NAME",8` and `OPEN15,8,15,"N:NAME,ID"` (format) work as on the real machine.

## Build and test

```powershell
./tools/Install-Roms.ps1                 # the ROMs from C64 Forever (not versioned: copyrighted)
dotnet test tests/ThingCommodore.Tests
```

The tests boot a Thing64 (KERNAL, BASIC) and the real drive ROMs: they need the ROMs and `../Thing64`, `../Thing65` and `../ThingRetroEmu`
next to this repository. Without the ROMs they are *Inconclusive*. A full run takes a couple of minutes: formatting a disk alone is 160
tracks written and verified.

## Documents

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md): the boards, the clocks and how a device is plugged in.
- [docs/OPEN-POINTS.md](docs/OPEN-POINTS.md): what is undecided or known to be wrong.
- [docs/ROADMAP.md](docs/ROADMAP.md): what comes next.
- [docs/log/](docs/log/README.md): the log of the whole Commodore family (status, shared code, configuration folders, decisions).
