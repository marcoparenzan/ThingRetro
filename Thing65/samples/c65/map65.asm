; MAP65: the 4510's memory mapper. MAP takes the offsets and the blocks to map from A, X, Y and Z: A and X give the lower
; half ($0000-$7FFF, blocks of 8 KB) and Y and Z the upper one. Here block 1 ($2000-$3FFF) is shown at physical $12000
; (bank 1) instead of $02000: a byte is written through the window and read back. The ROM keeps the map it needs in
; $011C-$011F, and the program puts it back at the end. EOM ends the mapping sequence (interrupts are held until then).
!cpu 4510

* = $1800
        lda #$00               ; A: bits 8-15 of the lower offset
        ldx #$21               ; X: bits 4-7 = blocks 0-3 to map (block 1), bits 0-3 = bits 16-19 of the offset ($10000)
        ldy #$00
        ldz #$00               ; nothing mapped in the upper half
        map
        eom

        lda #'7'
        sta $2000              ; physical $12000
        lda $2000
        sta result

        lda $011c              ; the map BASIC runs with
        ldx $011d
        ldy $011e
        ldz $011f
        map
        eom

        lda result
        jsr $ffd2              ; prints the byte: 7
        lda #13
        jmp $ffd2

result: !byte 0
