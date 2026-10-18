; ----------------------------------------------------------------------------------------------------------
; ECM - extended background color mode: four background colors for the characters
;
; In ECM mode ($D011 bit 6) bits 6 and 7 of the screen code no longer select the character but the background
; color of the cell, among $D021, $D022, $D023 and $D024. The price: only 64 characters are left (codes 0-63),
; enough for letters, digits and punctuation.
;
; The program writes the same sentence four times, one per background color, and returns to BASIC: BASIC keeps
; working in ECM mode, because letters and digits have codes below 64. The cursor instead shows up as a small
; green square: to blink it reverses the character by setting bit 7, which in ECM selects background $D023.
; ----------------------------------------------------------------------------------------------------------

SCREEN      = $0400
VIC_CR1     = $D011         ; bit 6 = extended color mode
BACKGROUND0 = $D021
BACKGROUND1 = $D022
BACKGROUND2 = $D023
BACKGROUND3 = $D024
CLRSCR      = $E544
ROW2        = SCREEN+40*2   ; rows to write to
ROW4        = SCREEN+40*4
ROW6        = SCREEN+40*6
ROW8        = SCREEN+40*8

            * = $0801
            .basic

            JSR CLRSCR
            LDA #6
            STA BACKGROUND0             ; blue
            LDA #2
            STA BACKGROUND1             ; red
            LDA #5
            STA BACKGROUND2             ; green
            LDA #0
            STA BACKGROUND3             ; black
            LDA VIC_CR1
            ORA #%01000000
            STA VIC_CR1                 ; ECM on

            LDX #0
copy:       LDA text,X
            CMP #$FF
            BEQ done
            STA ROW2,X                  ; bits 7-6 = %00: background $D021
            ORA #%01000000
            STA ROW4,X                  ; %01: $D022
            EOR #%11000000
            STA ROW6,X                  ; %10: $D023
            ORA #%11000000
            STA ROW8,X                  ; %11: $D024
            INX
            BNE copy
done:       RTS

text:       .screen "EXTENDED COLOR MODE: 4 BACKGROUNDS"
            .byte $FF
