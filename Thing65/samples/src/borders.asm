; ----------------------------------------------------------------------------------------------------------
; BORDERS - opening the top and bottom borders, and putting sprites in them
;
; The vertical border of the VIC-II is a flip-flop: it is set when the raster equals the bottom line (251
; with 25 rows, 247 with 24) and cleared when it equals the top line (51 or 55). The compares are equalities:
; if between line 248 and 250 the screen switches to 24 rows, the bottom becomes 247, which has already gone
; by, and 251 is no longer compared. The border is not set and stays open, at the bottom and at the top, until
; the top line of the next frame. It is enough to go back to 25 rows before line 247 and repeat the trick at
; every frame.
;
; In the open border the VIC-II shows the background, the "idle" byte at the last address of the bank ($3FFF,
; cleared here) and the sprites: four sprites at Y 255 (lines 256-276) scroll in the bottom border.
;
; The switch must fall within three precise lines: no other interrupt may delay it. That is why the program
; turns off the CIA 1 timer interrupt and calls the KERNAL handler (keyboard, cursor, clock) from its own
; raster interrupt, once per frame: BASIC keeps working.
; ----------------------------------------------------------------------------------------------------------

VIC_SPRITES = $D000         ; sprite X and Y: $D000/$D001 sprite 0, $D002/$D003 sprite 1...
VIC_CR1     = $D011         ; bit 3 = 25 rows (RSEL), bit 7 = ninth bit of the raster line
VIC_RASTER  = $D012
VIC_ENABLE  = $D015         ; sprites enabled
VIC_IRQ     = $D019
VIC_IRQEN   = $D01A
SPRITE_COLS = $D027         ; sprite colors
CIA1_ICR    = $DC0D         ; CIA 1 interrupts: $7F turns them all off
POINTERS    = $07F8         ; sprite pointers (last 8 bytes of the screen at $0400)
SPRITE_DATA = $0340         ; block 13: datasette buffer, free
IDLE_BYTE   = $3FFF         ; what the graphics sequencer shows outside the display lines
IRQ_VECTOR  = $0314
KERNAL_IRQ  = $EA31         ; KERNAL IRQ handler: keyboard, cursor, clock, then RTI
KERNAL_RTI  = $EA81         ; end of the handler: restores A, X, Y and does RTI
CHROUT      = $FFD2

OPEN_LINE   = 248           ; the trick: between line 247 (bottom with 24 rows) and 251 (bottom with 25)
RESET_LINE  = 20            ; in the top border, before the top line: back to 25 rows
SPRITE_Y    = 255           ; lines 256-276, in the bottom border

            * = $0801
            .basic

            LDX #0
print:      LDA message,X
            BEQ sprites
            JSR CHROUT
            INX
            BNE print

sprites:    LDX #62
copy:       LDA ball,X
            STA SPRITE_DATA,X
            DEX
            BPL copy
            LDA #0
            STA IDLE_BYTE               ; in the open border only the background color

            LDX #3                      ; sprites 3, 2, 1, 0
setup:      LDA #SPRITE_DATA/64
            STA POINTERS,X
            LDA colors,X
            STA SPRITE_COLS,X
            TXA
            ASL A
            TAY                         ; Y = 2 x sprite number: offset of the X and Y registers
            LDA xpos,X
            STA VIC_SPRITES,Y
            LDA #SPRITE_Y
            STA VIC_SPRITES+1,Y
            DEX
            BPL setup
            LDA #%00001111
            STA VIC_ENABLE

            SEI
            LDA #$7F
            STA CIA1_ICR                ; no interrupts from the CIA 1 timer...
            LDA CIA1_ICR                ; ...and acknowledge any pending one
            LDA #$01
            STA VIC_IRQEN               ; only the raster interrupt
            LDA #OPEN_LINE
            STA VIC_RASTER
            LDA VIC_CR1
            AND #$7F                    ; ninth bit of the compare value at 0
            STA VIC_CR1
            LDA #<irq
            STA IRQ_VECTOR
            LDA #>irq
            STA IRQ_VECTOR+1
            CLI
            RTS

; Two raster interrupts per frame: at line 248 the border opens, at line 20 the next frame is prepared
irq:        LDA VIC_IRQ
            STA VIC_IRQ                 ; acknowledge the raster interrupt
            LDA VIC_RASTER
            CMP #100
            BCC top

            LDA VIC_CR1                 ; line 248: 24 rows, the bottom (247) has already gone by
            AND #%01110111
            STA VIC_CR1
            LDA #RESET_LINE
            STA VIC_RASTER
            JMP KERNAL_RTI

top:        LDA VIC_CR1                 ; line 20: 25 rows again, for the trick of the next frame
            AND #$7F
            ORA #%00001000
            STA VIC_CR1
            LDA #OPEN_LINE
            STA VIC_RASTER
            INC VIC_SPRITES             ; the sprites scroll by one pixel per frame
            INC VIC_SPRITES+2
            INC VIC_SPRITES+4
            INC VIC_SPRITES+6
            JMP KERNAL_IRQ              ; once per frame the KERNAL's work, which ends with RTI

message:    .byte 147
            .text "OPEN BORDERS: THE SPRITES SCROLL"
            .byte 13
            .text "IN THE BOTTOM BORDER"
            .byte 13, 0

colors:     .byte 7, 1, 3, 13           ; yellow, white, cyan, light green
xpos:       .byte 40, 100, 160, 220

; A ball: 21 rows of 3 bytes
ball:       .byte %00000000, %01111110, %00000000
            .byte %00000001, %11111111, %10000000
            .byte %00000111, %11111111, %11100000
            .byte %00001111, %11111111, %11110000
            .byte %00011111, %11111111, %11111000
            .byte %00111111, %11111111, %11111100
            .byte %00111111, %11111111, %11111100
            .byte %01111111, %11111111, %11111110
            .byte %01111111, %11111111, %11111110
            .byte %01111111, %11111111, %11111110
            .byte %01111111, %11111111, %11111110
            .byte %01111111, %11111111, %11111110
            .byte %01111111, %11111111, %11111110
            .byte %01111111, %11111111, %11111110
            .byte %00111111, %11111111, %11111100
            .byte %00111111, %11111111, %11111100
            .byte %00011111, %11111111, %11111000
            .byte %00001111, %11111111, %11110000
            .byte %00000111, %11111111, %11100000
            .byte %00000001, %11111111, %10000000
            .byte %00000000, %01111110, %00000000
