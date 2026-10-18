; DMA65: the DMAgic fills 80 bytes of the 80-column screen with a '*' and copies them one line down.
; The DMAgic is at $D700 (in VIC-III mode, which the ROM has already set): write the bank, the high and then the low byte
; of the address of a list; the last write starts the job and stops the CPU until it is done.
; The list is 12 bytes: command, count, source (address, bank), destination (address, bank), sub-command, modulo.
; Run it like hello65: BLOAD "DMA65",B0 and SYS 6144.
!cpu 4510

SCREEN = $0800                 ; the text screen of the C65 mode: 80 x 25
FIRST = SCREEN + 8 * 80        ; line 8
SECOND = SCREEN + 9 * 80       ; line 9

* = $1800
        lda #<fill
        ldx #>fill
        jsr dma
        lda #<copy
        ldx #>copy
        ; falls into dma

dma:    ldy #$00
        sty $d702              ; the list is in bank 0
        stx $d701              ; high byte of its address
        sta $d700              ; low byte: starts the job
        rts

fill:   !byte $03              ; command 3 = FILL
        !word 80               ; count
        !word '*'              ; the fill byte is the source's low byte ($2a: the screen code of "*")
        !byte 0
        !word FIRST            ; destination
        !byte 0
        !byte 0                ; sub-command: ascending, no hold, no modulo
        !word 0

copy:   !byte $00              ; command 0 = COPY
        !word 80
        !word FIRST            ; source
        !byte 0
        !word SECOND           ; destination
        !byte 0
        !byte 0
        !word 0
