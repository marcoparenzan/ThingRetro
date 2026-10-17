; ----------------------------------------------------------------------------------------------------------
; MULTICOLOR - multicolor text with a character set defined in RAM
;
; In multicolor mode ($D016 bit 4) the characters whose color has bit 3 set are drawn in pairs of bits:
; each pair is a double-width pixel, with 4 possible colors:
;   %00 = background ($D021)   %01 = $D022   %10 = $D023   %11 = color RAM (bits 0-2)
; Characters whose color has bit 3 clear stay normal (8 pixels, 2 colors).
;
; The program defines 4 characters, each filled with one of the 4 pairs, in a character set at $3000
; ($D018 tells the VIC-II where to read it), and fills the screen with diagonal stripes. The color RAM color
; changes at every row, so the fourth stripe changes color going down. The program does not return to BASIC.
; ----------------------------------------------------------------------------------------------------------

SCREEN      = $0400
COLOR_RAM   = $D800
CHARSET     = $3000
VIC_CR2     = $D016         ; bit 4 = multicolor
VIC_MEMORY  = $D018         ; bits 4-7: screen / 1 KB, bits 1-3: characters / 2 KB
BACKGROUND  = $D021
COLOR1      = $D022
COLOR2      = $D023
SCREEN_PTR  = $FB           ; pointer to the current screen row
COLOR_PTR   = $FD           ; pointer to the current color RAM row
ROW         = $02           ; current row (0-24)

            * = $0801
            .basic

            LDX #4*8-1                  ; 4 characters of 8 bytes
copy:       LDA chars,X
            STA CHARSET,X
            DEX
            BPL copy

            LDA #%00011100              ; screen at $0400 (1 x 1 KB), characters at $3000 (6 x 2 KB)
            STA VIC_MEMORY
            LDA VIC_CR2
            ORA #%00010000
            STA VIC_CR2                 ; multicolor on
            LDA #6
            STA BACKGROUND              ; blue
            LDA #2
            STA COLOR1                  ; red
            LDA #7
            STA COLOR2                  ; yellow

            LDA #<SCREEN
            STA SCREEN_PTR
            LDA #>SCREEN
            STA SCREEN_PTR+1
            LDA #<COLOR_RAM
            STA COLOR_PTR
            LDA #>COLOR_RAM
            STA COLOR_PTR+1
            LDX #0
row:        STX ROW
            LDY #0
column:     TYA
            CLC
            ADC ROW
            AND #%00000011              ; (column + row) mod 4: diagonal stripes
            STA (SCREEN_PTR),Y
            TXA
            AND #%00000111
            ORA #%00001000              ; bit 3 = multicolor character; bits 0-2 = color of %11
            STA (COLOR_PTR),Y
            INY
            CPY #40
            BNE column

            CLC                         ; next row: +40 to both pointers
            LDA SCREEN_PTR
            ADC #40
            STA SCREEN_PTR
            BCC screen_ok
            INC SCREEN_PTR+1
screen_ok:  CLC
            LDA COLOR_PTR
            ADC #40
            STA COLOR_PTR
            BCC color_ok
            INC COLOR_PTR+1
color_ok:   INX
            CPX #25
            BNE row

done:       JMP done

chars:      .fill 8, %00000000          ; character 0: all %00 (background)
            .fill 8, %01010101          ; character 1: all %01 ($D022)
            .fill 8, %10101010          ; character 2: all %10 ($D023)
            .fill 8, %11111111          ; character 3: all %11 (color RAM)
