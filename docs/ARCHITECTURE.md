# Architecture

## The idea

The code mirrors the hardware. A **chip** is an object that knows nothing of the board it is mounted on; a **socket** is the
place on the board where a chip goes; a **cable** is an object that two connectors plug into. A computer (Thing64, Thing65) or
a drive (Thing1500) is a *board* that owns sockets, traces and connectors, and it lives in its own repository.

## Pinouts: the contract

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

## Media

`D64Image`/`D81Image` hold the sectors; `GcrTrack` and `D64Writer` turn them into the bit stream a drive head reads and back.
The disk mechanics (stepper, motor, head) are not here: they are part of the drive (Thing1500).

## Assembler

One assembler, one table of instructions per CPU (`InstructionSet`): ACME-compatible syntax, `!cpu 6510|4510`, forward
references in two passes, `.prg` output. The editor's Monaco syntax file travels with the package.

## Repositories around it

| Repo | Role | Uses |
| --- | --- | --- |
| ThingRetroEmu | Components | nothing |
| Thing64 | C64 board, desktop app, web studio | ThingRetroEmu |
| Thing65 | C65 board, desktop app, web studio | ThingRetroEmu |
| Thing1500 | Disk drives as boards | ThingRetroEmu |
| ThingCommodore | Cross-project log: status, decisions, map of the configuration folders | documents only |
