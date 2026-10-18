; ----------------------------------------------------------------------------------------------------------
; MCBITMAP - multicolor bitmap: 160 x 200 pixels, 4 colors per cell
;
; Like the high-resolution bitmap, but with multicolor on ($D016 bit 4): each pair of bits is a double-width
; pixel and selects one of 4 colors, the first three different for each 8 x 8 cell:
;   %00 = background ($D021)   %01 = video memory, high nibble   %10 = video memory, low nibble
;   %11 = color RAM
;
; The program splits each cell into four horizontal stripes, one per color source. The color RAM changes
; from cell to cell, so the bottom stripe has different colors. The program does not return to BASIC.
; ----------------------------------------------------------------------------------------------------------

VIC_CR1     = $D011
VIC_CR2     = $D016
VIC_MEMORY  = $D018
BACKGROUND  = $D021
SCREEN      = $0400
COLOR_RAM   = $D800
BITMAP      = $2000
POINTER     = $FB

            * = $0801
            .basic

            LDA #$3B
            STA VIC_CR1                 ; bitmap, display on, 25 rows
            LDA VIC_CR2
            ORA #%00010000
            STA VIC_CR2                 ; multicolor
            LDA #$18
            STA VIC_MEMORY              ; video memory at $0400, bitmap at $2000
            LDA #0
            STA BACKGROUND              ; %00 black

            LDX #0
colors:     LDA #$27                    ; %01 red (2), %10 yellow (7)
            STA SCREEN,X
            STA SCREEN+$100,X
            STA SCREEN+$200,X
            STA SCREEN+$300,X
            TXA
            AND #%00001111              ; %11: a different color for each cell (repeating every 16)
            STA COLOR_RAM,X
            STA COLOR_RAM+$100,X
            STA COLOR_RAM+$200,X
            STA COLOR_RAM+$300,X
            INX
            BNE colors

            LDA #<BITMAP
            STA POINTER
            LDA #>BITMAP
            STA POINTER+1
            LDY #0
fill:       TYA
            AND #%00000111              ; pixel row within the cell (0-7)
            LSR                         ; two rows per stripe: 0-3
            TAX
            LDA stripes,X
            STA (POINTER),Y
            INY
            BNE fill
            INC POINTER+1
            LDA POINTER+1
            CMP #>BITMAP+$2000
            BNE fill

done:       JMP done

stripes:    .byte %00000000             ; rows 0-1: background
            .byte %01010101             ; rows 2-3: high nibble of video memory
            .byte %10101010             ; rows 4-5: low nibble of video memory
            .byte %11111111             ; rows 6-7: color RAM
