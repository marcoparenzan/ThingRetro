; VIC3: what the VIC-III adds, from the program. The ROM leaves the chip in VIC-III mode, so the new registers are there.
; - the palette: color 2 is redefined (it is red in the ROM: here it becomes orange-ish)
; - attributes: with ATTR set, bits 4-7 of the Color RAM of a character are blink, reverse, highlight and underline
; Row 12 of the 80-column screen gets "REVERSE" with the reverse attribute and "UNDERLINE" underlined.
!cpu 4510

SCREEN = $0800
ROW = 12 * 80
COLORS = $d800                 ; the first 1 KB of the Color RAM (the 2 KB with CRAM@DC00, see $d030)

* = $1800
        lda #$0f               ; red 15
        sta $d102
        lda #$07               ; green 7
        sta $d202
        lda #$00               ; blue 0
        sta $d302
        lda $d030
        ora #$04               ; PAL: the first 16 colors come from the palette RAM, not from the palette ROM
        sta $d030

        lda $d031
        ora #$20               ; ATTR: character attributes on
        sta $d031

        ldx #0
rev:    lda reverse,x
        beq under
        sta SCREEN + ROW + 10,x
        lda #$21               ; reverse + color 1 (white)
        sta COLORS + ROW + 10,x
        inx
        bra rev
under:  ldx #0
uln:    lda underline,x
        beq done
        sta SCREEN + ROW + 30,x
        lda #$81               ; underline + color 1
        sta COLORS + ROW + 30,x
        inx
        bra uln
done:   rts

reverse:   !scr "reverse", 0
underline: !scr "underline", 0
