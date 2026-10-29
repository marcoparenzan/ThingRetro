; ----------------------------------------------------------------------------------------------------------
; THINGOS - a windowing operating system for the Commodore 64
;
; It takes the place of the two system ROMs: what the C64 keeps in the BASIC socket ($A000-$BFFF, the upper
; ROM here) and in the KERNAL socket ($E000-$FFFF, the lower ROM). There is no BASIC and no tape: the screen
; is a desktop in character mode, driven by a 1351 mouse (port 1) or a joystick (port 2), and the programs
; come from the disk.
;
;   lower ROM ($E000)   reset, hardware set up, IRQ, mouse/joystick/keyboard, the jump table, the vectors
;   upper ROM ($A000)   drawing on the screen, the desktop
;
; The CPU port stays at $37 (both ROMs and the I/O visible). The ROM builder (ThingOS64) assembles this file
; once and cuts the two 8 KB images out of it; font.asm becomes the CHARGEN ROM.
; ----------------------------------------------------------------------------------------------------------

            !cpu 6510
            !initmem $FF                ; an erased EPROM

            !source "defs.asm"

; ---------------------------------------------------------------------------------------------- upper ROM
            * = $A000
            !source "screen.asm"
            !source "wm.asm"
            !source "desktop.asm"
upper_end:
            !if upper_end > $C000 { !error "the upper ROM is over 8 KB" }

; ---------------------------------------------------------------------------------------------- lower ROM
            * = $E000
            !source "reset.asm"
            !source "irq.asm"
            !source "keyboard.asm"
            !source "pointer.asm"
lower_end:
            !if lower_end > JUMP_TABLE { !error "the lower ROM runs into the jump table" }

            !source "jumptable.asm"
