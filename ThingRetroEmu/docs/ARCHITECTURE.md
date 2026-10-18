# Architecture

## The idea

The code mirrors the hardware. A **chip** is an object that knows nothing of the board it is mounted on; a **socket** is the
place on the board where a chip goes; a **cable** is an object that two connectors plug into. A computer (Thing64, Thing65) or
a drive (ThingCommodore) is a *board* that owns sockets, traces and connectors, and it lives in its own repository.

## Pinouts: the contract

(The chips and their pinouts are in ThingMOS; what stays here is the generic part: the buses, `ParallelPort`, the media and the floppy drive interface.)

Each chip implements an interface that lists its pins in the datasheet's words (`I6510Pinout`, `I6526Pinout`, `IVia6522Pinout`,
`I4510Pinout`, ...): the register file on the bus, the ports, the control lines, `/IRQ`, `/RES`, the clock tick. A board
connects its traces to those members. Any object that implements the interface fits the socket, so a chip can be replaced by a
faster one, a test double or a debugging wrapper without touching the board.

Rules:
1. A chip depends on nothing but `Pinouts.cs` and the other chips' interfaces. No screens, speakers, files or windows.
2. Time comes from the board: the chip advances one clock cycle per `Tick` and makes one bus access per cycle.
3. Lines are `Func<bool>` (true = high), or `OpenCollectorLine`/`IecLine` when several outputs share a trace.
4. Code run every cycle is compiled with optimization in Debug too (`<Optimize>true</Optimize>`).

## Sockets

`Socket<TPinout>` has `Insert`, `Remove`, `Chip` and `IsEmpty`. The board supplies two lambdas, *connect* and *disconnect*, which
wire and unwire the traces. Removing a chip leaves the traces floating, like an empty socket. A `Reset` after swapping a chip is
advisable, as after a power cycle.

## Buses and cables

The **IEC cable** is explicit: `IecBus` owns three `IecLine`s (ATN, CLK, DATA) and every device attaches its `IecPort` to it
(`IecBus.Attach`). A line is open collector with a pull-up: it is low as soon as one device pulls it down. A computer and a drive
never know each other: the C64 KERNAL talks to a 1541 through the cable exactly as on the bench, and the same `IecPort` fits
Thing64, Thing65 and any later device (printer, second drive).

The **audio and video cables** and the **control ports** (joystick, paddles, mouse) follow the same idea; the computers still
own the video and audio output cables today (see the roadmap).

## Video, audio and the PC side

The screen is defined by what it shows, not by a machine. A video source (`IVideoSource`, which a video chip implements) says what its picture is (`VideoMode`: width,
height, frames per second, and the shape of the dots, 2:1 for an Atari 2600) and renders it in BGRA; a monitor (`IMonitor`) hands the source a buffer of that mode at
the start of every frame. If the source changes its mode (a VIC-III switching resolution) the monitor makes the room for it and the next frame follows. So the same
`MonitorControl` (WinForms) or the same canvas (the web page) shows a VIC-II, a VIC-III, a TIA or an Apple II. Sound is the same: a machine feeds `AudioPort` one
level per cycle, the port resamples to the rate the `ISpeaker` declares.

Control ports, speakers, monitors, joysticks and the mouse as paddles are shared by every emulator that is built on these interfaces.

## Media

`D64Image`/`D81Image` hold the sectors; `GcrTrack` and `D64Writer` turn them into the bit stream a drive head reads and back.
The disk mechanics (stepper, motor, head) are not here: they are part of the drive (ThingCommodore).

## Repositories around it

| Repo | Role | Uses |
| --- | --- | --- |
| ThingRetroEmu | Sockets, buses, cables, media, ports, the chips that are not MOS | nothing |
| ThingMOS | The MOS Technology and CSG chips, and the assembler | ThingRetroEmu |
| Thing64 | C64 board, desktop app, web studio | ThingRetroEmu |
| Thing65 | C65 board, desktop app, web studio | ThingRetroEmu |
| ThingCommodore | Disk drives, cassette recorder and printer as boards; also the cross-project log (`docs/log`) | ThingRetroEmu |
