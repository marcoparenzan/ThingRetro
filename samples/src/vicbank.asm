; ----------------------------------------------------------------------------------------------------------
; VICBANK - moving the screen and the characters to another VIC-II bank
;
; The VIC-II sees only 16 KB at a time. The bank is selected by bits 0-1 of CIA 2 port A, inverted:
;   %11 = bank 0 ($0000-$3FFF)   %10 = bank 1 ($4000-$7FFF)   %01 = bank 2   %00 = bank 3
; In banks 0 and 2 the VIC-II sees the character ROM at $1000-$1FFF; in banks 1 and 3 it does not: the
; characters must be copied into RAM. The CPU sees the character ROM at $D000 only with CHAREN = 0 (bit 2 of
; $01): at that moment the I/O is not at $D000, so the copy is done with interrupts disabled.
;
; The program copies the character ROM to $6000, inverts the letter A to show that the VIC-II now reads RAM,
; switches to bank 1 with the screen at $4400 and tells the KERNAL ($0288) where to write. Then it returns to
; BASIC, which keeps working in bank 1: every A shows up reversed.
; ----------------------------------------------------------------------------------------------------------

CPU_PORT    = $01           ; bit 2 = CHAREN: 1 = I/O at $D000, 0 = character ROM
CHAR_ROM    = $D000
CHARSET     = $6000         ; bank 1 + $2000
SCREEN      = $4400         ; bank 1 + $0400
CIA2_PRA    = $DD00         ; bits 0-1: VIC-II bank (inverted)
VIC_MEMORY  = $D018
HIBASE      = $0288         ; screen page for the KERNAL editor
CLRSCR      = $E544
CHROUT      = $FFD2
SOURCE      = $FB
TARGET      = $FD

            * = $0801
            .basic

            SEI                         ; with CHAREN = 0 the I/O disappears: no interrupts during the copy
            LDA CPU_PORT
            AND #%11111011
            STA CPU_PORT
            LDA #<CHAR_ROM
            STA SOURCE
            LDA #>CHAR_ROM
            STA SOURCE+1
            LDA #<CHARSET
            STA TARGET
            LDA #>CHARSET
            STA TARGET+1
            LDX #8                      ; 8 pages = 2 KB: the upper case/graphics set
            LDY #0
copy:       LDA (SOURCE),Y
            STA (TARGET),Y
            INY
            BNE copy
            INC SOURCE+1
            INC TARGET+1
            DEX
            BNE copy
            LDA CPU_PORT
            ORA #%00000100              ; I/O at $D000 again
            STA CPU_PORT
            CLI

            LDX #7                      ; the letter A (screen code 1) reversed
invert:     LDA CHARSET+8,X
            EOR #%11111111
            STA CHARSET+8,X
            DEX
            BPL invert

            LDA CIA2_PRA
            AND #%11111100
            ORA #%00000010              ; %10 = bank 1
            STA CIA2_PRA
            LDA #%00011000              ; screen at bank + $0400 (1 KB x 1), characters at bank + $2000 (2 KB x 4)
            STA VIC_MEMORY
            LDA #>SCREEN
            STA HIBASE                  ; the KERNAL will write at $4400
            JSR CLRSCR

            LDX #0
print:      LDA message,X
            BEQ done
            JSR CHROUT
            INX
            BNE print
done:       RTS

message:    .text "SCREEN AT $4400, CHARACTERS AT $6000 (BANK 1)"
            .byte 13
            .text "THE A IS COPIED INTO RAM AND REVERSED"
            .byte 13, 0
