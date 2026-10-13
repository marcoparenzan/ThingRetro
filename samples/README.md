# Samples

Programs to open in Fake64 (from the studio's explorer, by dragging them onto a C64 or from the command line),
each devoted to a Commodore 64 topic. The sources are in [src](src): BASIC (`.bas`) and commented 6502 assembly
(`.asm`), meant to be read; the `.asm` ones open in the studio's editor, where F5 assembles and runs them.
BASIC programs and those with the `10 SYS2061` stub start by themselves with `RUN`.

## BASIC

| File | Source | What it shows |
| --- | --- | --- |
| `hello.prg` | [hello.bas](src/hello.bas) | Colored text: `POKE 646` is the cursor color |
| `math.prg` | [math.bas](src/math.bas) | Floating point expressions and functions |
| `sprite.prg` | [sprite.bas](src/sprite.bas) | A bouncing expanded sprite |
| `keys.prg` | [keys.bas](src/keys.bas) | Function keys and cursor keys with `GET` (F1-F8 = codes 133-140). `Q` to quit |
| `joystick.prg` | [joystick.bas](src/joystick.bas) | Joystick in port 2 (`PEEK(56320)`): the smiley moves, fire changes its color |
| `paddle.prg` | [paddle.bas](src/paddle.bas) | Paddles in port 1 (in the app: "Paddles: mouse"): the smiley follows the knob (`PEEK(54297)`) |
| `sound.prg` | [sound.bas](src/sound.bas) | A scale with voice 1 of the SID: frequency, envelope, GATE |

## Machine language: CPU and interrupts

| File | Source | What it shows |
| --- | --- | --- |
| `sys.prg` | [sys.asm](src/sys.asm) | A routine at `$C000`: started with `SYS 49152` (not with `RUN`) |
| `rasterbars.prg` | [rasterbars.asm](src/rasterbars.asm) | VIC-II raster interrupt: a still red bar in the border, while BASIC works |
| `irq.prg` | [irq.asm](src/irq.asm) | Hooking into the CIA 1 timer IRQ (vector `$0314`) without stopping the KERNAL |
| `nmi.prg` | [nmi.asm](src/nmi.asm) | NMI from the CIA 2 timer (vector `$0318`): falling edge and interrupt acknowledge |
| `decimal.prg` | [decimal.asm](src/decimal.asm) | Decimal mode (BCD): a 4-digit counter without divisions. Does not return to BASIC |

## Machine language: SID

| File | Source | What it shows |
| --- | --- | --- |
| `music.prg` | [music.asm](src/music.asm) | Three-voice music played by the IRQ: melody with PWM, bass through the resonant low-pass filter, noise. Returns to BASIC and keeps playing |

## Machine language: VIC-II

| File | Source | What it shows |
| --- | --- | --- |
| `multicolor.prg` | [multicolor.asm](src/multicolor.asm) | Multicolor text with a character set defined in RAM |
| `ecm.prg` | [ecm.asm](src/ecm.asm) | Extended color mode: 4 background colors selected by bits 6-7 of the screen code |
| `bitmap.prg` | [bitmap.asm](src/bitmap.asm) | 320 x 200 bitmap: memory organized in 8 x 8 cells |
| `mcbitmap.prg` | [mcbitmap.asm](src/mcbitmap.asm) | 160 x 200 multicolor bitmap: the 4 color sources of each cell |
| `sprites.prg` | [sprites.asm](src/sprites.asm) | Eight sprites: expansion, multicolor, priority over the graphics, collisions (`$D01E`) |
| `scroll.prg` | [scroll.asm](src/scroll.asm) | Fine scrolling of one line with XSCROLL, changed with the raster. Does not return to BASIC |
| `vicbank.prg` | [vicbank.asm](src/vicbank.asm) | VIC-II bank from CIA 2, characters copied from ROM into RAM and modified |
| `borders.prg` | [borders.asm](src/borders.asm) | Top and bottom borders opened by changing the number of rows at line 248, with four sprites in the bottom border |

The programs that "do not return to BASIC" stay in a loop: a reset is needed to get out (right click on the
monitor).

## Cartridges

| File | Source | What it shows |
| --- | --- | --- |
| `hello.crt` | [cartridge.asm](src/cartridge.asm) | 8K cartridge with autostart (`CBM80` signature) |
| `ultimax.crt` | [ultimax.asm](src/ultimax.asm) | Ultimax mode: the C64 without KERNAL, starting from the cartridge's reset vector |
| `banks.crt` | [banks0.asm](src/banks0.asm), [banks1.asm](src/banks1.asm) | Magic Desk with two banks: the bank switching code must run in RAM |

## The assembler

The `.asm` sources use the project's assembler (`src/Fake64.Asm`), small but complete:

```asm
; comment
* = $0801               ; assembly address
.basic                  ; BASIC line "10 SYS2061": the code starts right after it
BORDER = $D020          ; constant (CONSTANTS in upper case, labels in lower case: they are distinct)
loop:   LDA #<text      ; label; <value and >value are the low and the high byte
        STA SCREEN+40*12,X
        BNE loop
text:   .text "HELLO"   ; PETSCII, for CHROUT
        .screen "HELLO" ; screen codes, to be written straight into video memory
        .byte 13, $FF, %1010, 'A'
        .word loop
        .fill 8, 0
```

All the 6502 addressing modes with the standard syntax (zero page is chosen automatically), the undocumented
opcodes included, expressions with `+ - * /`, parentheses and `*` for the current address.

## Regenerating

The outputs and their sources are listed in [samples.json](samples.json), a build manifest:

```powershell
fake64 build samples/samples.json            # writes the .prg and .crt files into samples/
```

(or `dotnet run --project src/Fake64 -- build samples/samples.json` without the global tool.)

The tests in `src/Fake64.Tests/TestSamples.cs` run every sample in the emulator and check that the files of this
folder match the ones built from the manifest.
