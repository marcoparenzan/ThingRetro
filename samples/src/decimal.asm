; ----------------------------------------------------------------------------------------------------------
; DECIMAL - the 6510's decimal (BCD) mode: a 4-digit counter
;
; With the D flag set (SED) ADC and SBC work in BCD: each byte holds two decimal digits, one per nibble
; ($09 + $01 = $10, $99 + $01 = $00 with carry). So a number can be shown on screen without divisions:
; each nibble already is a digit.
;
; Careful: an interrupt does not clear the D flag, and the KERNAL handler does not either. That is why the
; decimal part is enclosed between SEI and CLI: if an IRQ arrived with D set, the KERNAL's calculations
; (the TI clock, for example) would go wrong.
;
; The counter goes up by one at every frame (50 times per second). The program does not return to BASIC.
; ----------------------------------------------------------------------------------------------------------

SCREEN      = $0400
RASTER      = $D012         ; current raster line (low 8 bits)
CLRSCR      = $E544         ; KERNAL routine that clears the screen
LOW         = $FB           ; tens and units digits (BCD)
HIGH        = $FC           ; thousands and hundreds digits (BCD)
ZERO        = $30           ; screen code of the digit 0
TEXT_COLUMN = 6

            * = $0801
            .basic

            JSR CLRSCR
            LDX #0
title:      LDA text,X                  ; description next to the digits
            CMP #$FF
            BEQ start
            STA SCREEN+TEXT_COLUMN,X
            INX
            BNE title

start:      LDA #0
            STA LOW
            STA HIGH

loop:       LDA RASTER                  ; wait for line 255: once per frame
            CMP #255
            BNE loop

            SEI
            SED                         ; from here ADC works in BCD
            CLC
            LDA LOW
            ADC #1
            STA LOW
            LDA HIGH
            ADC #0                      ; the carry (from 99 to 00) goes to the hundreds
            STA HIGH
            CLD                         ; back to binary before enabling interrupts again
            CLI

            LDX #0                      ; column on the screen
            LDA HIGH
            JSR digits
            LDA LOW
            JSR digits

wait:       LDA RASTER                  ; wait until line 255 has gone by, so as not to count twice
            CMP #255
            BEQ wait
            JMP loop

; Writes the two BCD digits of A at SCREEN+X and advances X by 2
digits:     PHA
            LSR                         ; high nibble: left digit
            LSR
            LSR
            LSR
            ORA #ZERO
            STA SCREEN,X
            INX
            PLA
            AND #%00001111              ; low nibble: right digit
            ORA #ZERO
            STA SCREEN,X
            INX
            RTS

text:       .screen "<- BCD COUNTER, +1 PER FRAME"
            .byte $FF                   ; end of the text (0 is the screen code of @)
