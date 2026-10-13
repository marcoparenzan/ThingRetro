; ----------------------------------------------------------------------------------------------------------
; SYS - a machine language program at $C000, started with SYS 49152
;
; $C000-$CFFF are 4 KB of RAM that BASIC does not use: the classic place for machine language routines.
; A .prg file that starts at $C000 is not a BASIC program: after loading it you do not type RUN but
; SYS 49152 (49152 = $C000). SYS jumps to the address and, when the routine executes RTS, returns to BASIC.
;
; The routine changes the colors and writes a message with CHROUT, the KERNAL routine that prints a character
; at the cursor position (like PRINT).
; ----------------------------------------------------------------------------------------------------------

BORDER      = $D020
BACKGROUND  = $D021
CHROUT      = $FFD2
RETURN      = 13            ; PETSCII: new line

            * = $C000

start:      LDA #0
            STA BORDER                  ; black
            LDA #11
            STA BACKGROUND              ; dark grey
            LDX #0
print:      LDA message,X
            BEQ done
            JSR CHROUT
            INX
            BNE print
done:       RTS                         ; back to BASIC, which prints READY.

message:    .byte RETURN
            .text "HELLO FROM $C000! (SYS 49152)"
            .byte RETURN, 0
