# Shared code

## Today (2026-10-19)

```text
ThingMOS        the MOS Technology and CSG chips, and the assembler
   |
   v
ThingRetroEmu   sockets, buses, media (.d64 .d81 .tap .prg .crt), video and audio outputs, control ports, the WD1770
   ^                         (+ ThingRetroEmu.WinForms: monitor, speakers, joysticks, mouse paddles)
   |
Thing64    Thing65    ThingCommodore (1541, 1581, C2N, MPS-803)      <- boards and devices: they use both libraries
```

- **Thing64 and Thing65** have no chips, no assembler, no monitor, no speakers, no joysticks, no cartridges, no `.prg` loader, no BASIC tokenizer, no graphics conversion and
  no build tool of their own any more (the projects `Mos`, `Asm`, `Basic`, `Build`, `Crt`, `Graphics` and `Prg` are gone, and the board's copies of `Socket`, `OpenCollectorLine`,
  `Ram64KbChip` and `Cartridge`). What they keep is what is machine specific: the board and its wiring, the keyboard matrix and its PC mapping (`Keyboard`, `PcKeyboard`), the ROM set,
  the apps, the web pages and the studios. The boards of Thing64 and Thing65 stay separate on purpose: a C65 is not a C64, and the C65 project keeps its own C64-mode board next to `BoardC65`.
- **ThingCommodore** depends on both libraries; it also holds what the Commodore machines share besides devices: `Cartridges`, `Software` (`.prg` into RAM, keyboard buffer) and
  the second project `ThingCommodore.Tools` (BASIC tokenizer, graphics conversion, build manifests). Its tests use Thing64 and Thing65 as computers.
- Thing1500 (the old home of the drives) was deleted: everything it had is in ThingCommodore and the libraries.

## Rules

- A fix to a chip goes in **ThingMOS**; a fix to a socket, bus, cable, medium, monitor or speaker goes in **ThingRetroEmu**.
- A new emulator takes its CPU and chips from ThingMOS (a 6502 machine: Apple II, Atari 2600...), its screen, sound, joysticks and file formats from ThingRetroEmu, and writes its board.
- Chips of other makers (the WD1770, the TIA...) go in ThingRetroEmu or in a library of their own, not in ThingMOS.

## History

On 2026-10-18 the chip code of Thing64 and Thing65 was compared: `MOS6526`, `MOS6581`, `MOS6569` and `MOS6502.Instructions` were identical line for line. Thing65 had the `SO` pin on the 6502,
the interfaces of the C65 chips, a ninth keyboard line and the CPU parameter of the assembler. All of it went into one library, and that evening the MOS part was separated as ThingMOS.
On 2026-10-19 the monitor, the audio output, the control ports and the file formats followed.
