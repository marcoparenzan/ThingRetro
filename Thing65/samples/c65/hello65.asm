; HELLO65: the first C65 program of the project, for the 4510 (the 65CE02 of the C65).
; It prints a line with the KERNAL, then shows two things the 6502 cannot do: the base page register (B)
; and the word increment (INW). Assemble with "thing65 asm hello65.asm" and run with "thing65 run hello65.prg".
;
; Where to put machine code on a C65: in C65 mode the BASIC ROM is mapped over $2000-$7FFF (that is why BASIC reads its
; own program through the DMA), so a program at $2001 would find ROM instead of its bytes. The lower 8 KB are RAM
; and the KERNAL and the I/O stay visible: $1300-$1FFF is free ($0800-$0FFF is the 80-column screen, $1000-$12FF
; the system variables). The program is loaded with BLOAD and started with SYS 6144.
!cpu 4510

CHROUT = $ffd2                 ; the KERNAL routine that prints a character (the same address as on the C64)
COUNTER = $1500                ; a word in bank 0, reached through the base page register

* = $1800                      ; bank 0, the free RAM below the BASIC area

start:
        ldx #0
print:  lda text,x
        beq counted
        jsr CHROUT
        inx
        bra print              ; BRA: the 65CE02 has an unconditional branch

counted:
        lda #>COUNTER          ; B = $15: from now on "base page" addresses are in $1500-$15FF
        tab
        lda #0
        sta $00                ; $1500
        sta $01                ; $1501
        inw $00                ; INW: a 16-bit increment of the word at base page $00
        inw $00
        inw $00
        ldx $00                ; the low byte of the counter: 3
        lda #0
        tab                    ; B = 0: the base page is the zero page again

        txa
        clc
        adc #'0'
        jsr CHROUT             ; prints "3"
        lda #13
        jsr CHROUT
        rts

text:   !pet "hello from the 4510! inw counted ", 0   ; !pet: lower case in the source is upper case on the screen
