# ThingOS64

A windowing operating system for the Commodore 64, in place of its system ROMs. No BASIC, no tape, no compatibility with existing
programs: a desktop in character mode, a pointer driven by a 1351 mouse or a joystick, programs and documents on disk. It is meant
to run on a real C64 (two 8 KB EPROMs in the BASIC and KERNAL sockets, a 4 KB character ROM) and is developed and tested on Thing64.

## Build and run

```powershell
dotnet run --project ThingOS64/src/ThingOS64              # writes ThingOS64/build/roms/{kernal,basic,chargen}
dotnet test ThingOS64/src/ThingOS64.Tests                 # boots the ROMs on the emulated board
Thing64/src/Thing64.Computer/bin/Debug/net10.0-windows/Thing64.Computer.exe --roms ThingOS64/build/roms
```

In the desktop app, right click on the screen: *Control port 1 > Mouse 1351* (click to capture the PC mouse, middle click or
Ctrl + right click to release it), or a joystick in *Control port 2*.

## The sources

| File | ROM | What |
| --- | --- | --- |
| [rom/thingos.asm](rom/thingos.asm) | both | the layout of the two ROMs, the includes |
| [rom/defs.asm](rom/defs.asm) | | chip registers, the memory map, key codes |
| [rom/reset.asm](rom/reset.asm) | lower | power on: CPU port, CIAs, SID, VIC-II, the pointer sprites |
| [rom/irq.asm](rom/irq.asm) | lower | the frame interrupt: 1351 mouse, joystick, buttons |
| [rom/keyboard.asm](rom/keyboard.asm) | lower | the matrix scan, ASCII codes, the key buffer, GETKEY |
| [rom/pointer.asm](rom/pointer.asm) | lower | the arrow: clamping, the sprites, its shape |
| [rom/jumptable.asm](rom/jumptable.asm) | lower | the entry points for applications, the CPU vectors |
| [rom/screen.asm](rom/screen.asm) | upper | characters, strings, numbers, rectangles; the clipped writing of the windows |
| [rom/wm.asm](rom/wm.asm) | upper | the window manager: slots, z order, ownership map, drawing, events |
| [rom/desktop.asm](rom/desktop.asm) | upper | the main loop (keys, clicks, drags), the menus, the About and demo windows |
| [rom/font.asm](rom/font.asm) | chargen | 128 characters, in the `;@ charset` format of Studio 64's charset editor |

`OsRoms.Build` ([src/ThingOS64/RomBuilder.cs](src/ThingOS64/RomBuilder.cs)) assembles `thingos.asm` once from `$A000` to `$FFFF`
and cuts the two sockets out of it; the CHARGEN is the font, its reverse (`$80-$FF`), and the same 2 KB again.

## Design

**Screen.** Text mode, 40x25, VIC-II bank 0, screen at `$0400`, characters straight from the CHARGEN ROM (the VIC-II sees it at
`$1000`). The character codes are ASCII for `$20-$7E`, so text needs no conversion; `$00-$1F` and `$7F` are the interface pieces
(frames, close box, arrows, shades, check boxes, file/program/disk icons); `$80-$FF` are the reverse of `$00-$7F`, which gives
highlighted text and solid blocks. White background, colors per cell from the color RAM.

**Pointer.** Two hires sprites (0 = black outline, 1 = white fill), hot spot at the tip, kept inside 0-319 x 0-199.

**Input, once per frame** (raster interrupt at line 251):
- 1351 mouse in port 1, proportional mode: the SID's POT X/Y are read while CIA 1 PA6 selects port 1, and the difference from the
  previous reading moves the pointer (the algorithm of Commodore's own 1351 driver). Left button = fire line, right button = up line.
- Joystick in port 2: 1 pixel per frame, 3 after a third of a second; fire is the left button.
- Keyboard: the full 8x8 matrix, keys that went down since the previous frame become ASCII (SHIFT, CTRL + letter = control code,
  cursor and function keys `$81-$8F`) in a 16-key ring. The lines a device holds in port 1 are masked out; a joystick held in port 2
  skips the scan (it would select columns by itself).

**Windows.** Up to 8, each in a slot (one array per field at `$C400`); `z_order` lists them from the bottom to the top. The
ownership map (`$C000`, a byte per cell: 0 = desktop, slot + 1 = the window on top there) is rebuilt when a window opens, closes,
moves or comes to the front, then the screen is drawn again: the desktop on its cells, every window only on its own cells, so
nothing is drawn twice and nothing flickers. The same map answers "what is under the pointer".

The OS keeps no copy of a window's content: a window has a *draw* routine, called with the drawing port on its content and the
cursor at 0,0, and an *event* routine (`EV_KEY` when it is on top, `EV_CLICK` with the cell of its content, `EV_CLOSE`). A window
writes with `WPUTCHAR`/`WPUTSTRING`, which go through the port: characters outside the content, or on cells another window covers,
are dropped. A window can be redrawn at any time (`WIN_INVALIDATE`), even when it is below others.

```
 [x] Title          <- title bar: close box, title; blue on top, grey below. Drag it to move the window
 |content         |
 +----------------+
```

The main loop sends the keys to the window on top and follows the left button: the menu bar (hold the button, release on an item),
a window (brought to the front; then close box, title bar to drag, or content), the desktop.

**Memory map.**

| Address | Use |
| --- | --- |
| `$02-$3F` | OS zero page (the IRQ has its own temporaries at `$30-$33`) |
| `$40-$FF` | application zero page |
| `$0300-$033F` | vectors (IRQ, NMI), keyboard matrix, key buffer |
| `$0340-$03BF` | pointer sprites |
| `$0400-$07FF` | screen, sprite pointers |
| `$0800-$9FFF` | applications |
| `$A000-$BFFF` | upper ROM (BASIC socket) |
| `$C000-$C3E7` | ownership map |
| `$C400-$C4FF` | window table, z order, window manager and menu state |
| `$C500-$CFFF` | OS RAM, free |
| `$E000-$FFFF` | lower ROM (KERNAL socket); jump table at `$FF80` |

**Jump table** (`$FF80`, entries never move):

| Address | Routine |
| --- | --- |
| `$FF80` | `GETKEY`: the next key in A, 0 if none (changes X) |
| `$FF83` | `WIN_OPEN`: A/X = descriptor (x, y, w, h, title, draw, event: 10 bytes). A = slot; carry set when 8 are open |
| `$FF86` | `WIN_CLOSE`: A = slot (its event routine gets `EV_CLOSE` first) |
| `$FF89` | `WIN_FRONT`: A = slot |
| `$FF8C` | `WIN_INVALIDATE`: A = slot, its content is drawn again |
| `$FF8F` | `WPUTCHAR`: A = character at the cursor (`cur_col`, `cur_row`) of the port, in `text_color` |
| `$FF92` | `WPUTSTRING`: A/X = string ending with 0 |
| `$FF95` | `WPUTNUMBER`: A/X = 0-999, three digits |

A draw routine gets X = slot; an event routine A = event, X/Y = its data (the key, or the cell clicked), `cur_win` = slot; both have
the port on the content. OS calls may change the zero page `$02-$3F`; applications keep theirs in `$40-$FF`.

## Milestones

1. **Boot and input** (done): reset, hardware set up, font, desktop and menu bar, pointer from 1351 or joystick, keyboard to ASCII.
2. **Window manager** (done): overlapping windows with a title bar and a close box, an ownership map (one byte per cell: which window is
   on top there) for clipping and hit testing, dragging, focus, the menu bar's menus, events (click, key) for the window on top. Menus: *ThingOS > About ThingOS*, *File > New window*
   (a demo window that shows the keys and marks the clicks), *File > Close window*, *Disk > Directory* (milestone 3).
3. **Disk**: the serial bus driven bit by bit from CIA 2 (ATN, CLK, DATA with the 1541's timing, EOI), LISTEN/TALK/OPEN/CLOSE,
   reading the directory, reading and writing files, the error channel. Tested against Thing64's 1541 with its real DOS.
4. **File manager**: a window per drive with the directory, double click to start a program.
5. **Applications**: a program format with a header, loading at `$0800`, the OS API in the jump table (windows, drawing, events,
   files), a first application (a text viewer/editor).

## Not done yet

Key repeat; RESTORE; double click; resizing windows; a whole redraw for every cell a window is dragged (about 100,000 cycles, 5 frames, with three windows: dragging runs at about 10 steps a second; an outline or redrawing only the exposed cells would be faster); PAL/NTSC detection (the raster line works on both, nothing depends on the frame rate yet); the second 2 KB of
the CHARGEN (a copy of the first, free for a second font).
