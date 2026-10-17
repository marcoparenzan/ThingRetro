; ----------------------------------------------------------------------------------------------------------
; BITMAP - the high-resolution bitmap mode (320 x 200 pixels, 2 colors per 8 x 8 cell)
;
; In bitmap mode each pixel is a bit in memory (8000 bytes). The memory is NOT ordered by screen lines but by
; 8 x 8 pixel cells, like characters: the first 8 bytes are the top left cell (one pixel row per byte), the
; next 8 the cell to its right, and so on for 40 cells; then the second row of cells. For each cell the two
; colors come from video memory ($0400): high nibble = color of the 1 bits, low nibble = color of the 0 bits.
;
; The program fills each cell with a diagonal and flips its direction at every column of cells: a zigzag.
; It stays in bitmap mode: a reset is needed to get back to BASIC.
; ----------------------------------------------------------------------------------------------------------

VIC_CR1     = $D011         ; bit 5 = bitmap mode, bit 4 = display on, bit 3 = 25 rows
VIC_MEMORY  = $D018         ; bits 4-7: video memory / 1 KB; bit 3: bitmap at $0000 or $2000
SCREEN      = $0400
BITMAP      = $2000
POINTER     = $FB           ; zero-page pointer ($FB-$FC are free for programs)
ROW         = $FD           ; pixel row within the cell (0-7)

            * = $0801
            .basic

            LDA #$3B
            STA VIC_CR1                 ; bitmap + display on + 25 rows + YSCROLL 3
            LDA #$18
            STA VIC_MEMORY              ; video memory at $0400 (1 x 1 KB), bitmap at $2000

; Colors: the 1 bits light blue (14), the 0 bits blue (6), in all 1000 cells
            LDX #0
            LDA #$E6
color:      STA SCREEN,X
            STA SCREEN+$100,X
            STA SCREEN+$200,X
            STA SCREEN+$300,X
            INX
            BNE color

; Pixels: Y runs through the 256 bytes of a page, POINTER points to the page; 32 cells per page
            LDA #<BITMAP
            STA POINTER
            LDA #>BITMAP
            STA POINTER+1
            LDY #0
fill:       TYA
            AND #%00000111              ; pixel row within the cell
            STA ROW
            TYA
            AND #%00001000              ; even or odd cell (Y / 8): selects the direction of the diagonal
            ORA ROW
            TAX
            LDA diagonals,X
            STA (POINTER),Y
            INY
            BNE fill
            INC POINTER+1               ; next page
            LDA POINTER+1
            CMP #>BITMAP+$2000          ; up to the end of the 8 KB
            BNE fill

done:       JMP done                    ; stay here: the display remains in bitmap mode

diagonals:  .byte %10000000, %01000000, %00100000, %00010000   ; diagonal going down to the right
            .byte %00001000, %00000100, %00000010, %00000001
            .byte %00000001, %00000010, %00000100, %00001000   ; diagonal going up to the right
            .byte %00010000, %00100000, %01000000, %10000000
