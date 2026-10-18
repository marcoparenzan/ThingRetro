# Architecture

## What a chip is

A chip is an object that knows nothing of the board it is in. It implements a **pinout** (`Pinouts.cs`), an interface that lists its pins in the
datasheet's words, and the board connects its traces to those members. Any object that implements the pinout fits the socket (`Socket<TPinout>` in
ThingRetroEmu): a faster chip, a test double, a probe that records the traffic.

Rules:
1. A chip depends on nothing but the pinouts and ThingRetroEmu's generic pieces (`IBus`, `ParallelPort`, `IDiskMedia`). No screens, speakers, files or windows.
2. Time comes from the board: one `Tick` is one cycle of the clock, and the chip makes one bus access per cycle (the 6502 and the 4510 in the order of the real
   chip: dummy reads, the double write of a read-modify-write instruction).
3. Lines are `Func<bool>` (true = high) and `ParallelPort` (a port with direction, latch and the pins pulled from outside), or `OpenCollectorLine` when
   several outputs share a trace.
4. Active-low lines are properties with "true = active" (`Irq`, `Nmi`, `Reset`).
5. Code run every cycle is compiled with optimization in Debug too (`<Optimize>true</Optimize>`).

## Where the line is drawn

ThingMOS holds the chips of MOS Technology and of CSG, the company that continued it and made the C65's chips. Everything that is not a MOS chip is
in ThingRetroEmu: the sockets, the buses and cables, the media, and the chips of other makers (the WD1770 of the 1570, 1571 and 1581). A machine that is
built with a 6502 and other makers' chips takes its CPU from here and the rest from there: the Apple II (a 6502 and TTL logic), the Atari 2600 (a 6507, which
is a 6502 with fewer address pins, a 6532 RIOT, which is a MOS chip, and the TIA, which is Atari's).

## The assembler

One assembler, one table of instructions per CPU (`InstructionSet`): ACME-compatible syntax, `!cpu 6510|4510`, forward references in two passes, `.prg` output.
It lives here because an instruction set belongs to the chip that executes it: a new CPU in this library brings its table.

## Dependencies

```text
ThingMOS  -->  ThingRetroEmu  (IBus, ParallelPort, IDiskMedia, ...)
```

Boards and devices (Thing64, Thing65, ThingCommodore) reference both.
