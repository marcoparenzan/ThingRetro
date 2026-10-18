# ThingRetroEmu

The retro hardware as C# components, written to be read: the **sockets** the chips are inserted in, the **buses and cables** between
machines, the **disk and tape media**, the control ports and the chips that are not MOS (the chips themselves are in ThingMOS). It is the library under
Thing64 (C64), Thing65 (C65) and ThingCommodore (the disk drives, the cassette recorder, the printer); none of its parts knows which computer or drive it is
mounted in.

> **Status: 0.4.** The code was extracted from Thing65 and Thing1500 on 2026-10-18. On the same day the MOS and CSG chips and the assembler went to
> [ThingMOS](https://github.com/marcoparenzan/ThingMOS). Thing64, Thing65 and ThingCommodore use both.

## What is in it

| Folder / namespace | Content |
| --- | --- |
| `Chips` | The pieces the chips of every maker are made of: `ParallelPort`, the generic pinouts (`IBus`, `IMappedBus`, `IDiskMedia`, `IFloppyDrive`...) in `Pinouts.cs`, the chips of other makers (the WD1770) and the devices that are not chips (`RamExpansion`, `RomCartridge`). **The MOS and CSG chips are in [ThingMOS](https://github.com/marcoparenzan/ThingMOS)** |
| `Sockets` | `Socket<TPinout>`: a socket accepts any object that implements the pinout; `OpenCollectorLine` |
| `Buses` | `IecPort` (the connector of a computer or drive), `IecLine` and `IecBus` (the serial cable: ATN, CLK and DATA, open collector), `IecListener` (a device that only listens on the cable, at the level of the protocol: the base of a printer) |
| `Video` | The monitor socket. Two connectors, like a real machine: **RGB** (`VideoMode`, `IVideoSource`, `IMonitor`: the picture as dots, and the monitor follows its size) and **composite** (`VideoStandard` PAL or NTSC, `CompositeFrame`: the voltage of the wire, sample after sample, with sync, burst and blanking; `CompositeEncoder`, `CompositeDecoder`, `CompositeTuner`, `ICompositeSource`, `ICompositeMonitor`). `VideoPort`, `MonitorCable` |
| `Audio` | The speaker socket: `ISpeaker`, `AudioPort` (resamples to the speaker's rate, removes the DC), `AudioCable` |
| `Media` | `.d64` (`D64Image`, `D64Writer`, GCR tracks), `.d81` (`D81Image`, `D81Writer`), `.tap` (`TapImage`), `.prg` (`PrgFile`) and `.crt` (`CrtImage`): the formats, not what a machine does with them |
| `Ports` | Control ports: `ControlPort`, `Joystick`, `Paddles`, `Mouse1351`; the cassette connector `CassettePort` |
| `ThingRetroEmu.WinForms` | A second project, for Windows: `MonitorControl` (one screen control for every machine), `PcSpeakers`, `KeyboardJoystick`, `UsbJoystick`, `MousePaddles` and `ControlPortSelector` |

## Build and test

```powershell
dotnet test tests/ThingRetroEmu.Tests
dotnet pack src/ThingRetroEmu -c Release -o D:\Dev\NuGetLocalFeed
```

The tests are those of the parts that need no motherboard (26 at the time of writing: the WD1770, the disk images, the IEC bus). Consumers reference the package from the local feed `D:\Dev\NuGetLocalFeed` (or, while a chip is
being developed, the project with a `ProjectReference`).

## Documents

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md): how chips, sockets, buses and cables fit together, and the rules of the library.
- [docs/OPEN-POINTS.md](docs/OPEN-POINTS.md): what is undecided or known to be wrong.
- [docs/ROADMAP.md](docs/ROADMAP.md): what comes next, from migration to the C2N, the printer and the ESP32 user port.
- [docs/CHIPS.md](docs/CHIPS.md): one line per chip: who uses it, what is missing.

The system ROMs are copyrighted and are not part of this repository; the computers' repositories explain how to install them.
