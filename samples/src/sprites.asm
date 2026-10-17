; ----------------------------------------------------------------------------------------------------------
; SPRITES - eight sprites: expansion, multicolor, priority and collisions
;
; The VIC-II draws up to 8 sprites of 24 x 21 pixels over (or under) the graphics. Each sprite reads 63 bytes
; from a block of 64 in the VIC-II bank: the block number is in the last 8 bytes of video memory
; ($07F8-$07FF with the screen at $0400). Here the data goes into the cassette buffer: block 13 ($0340) for
; the smiley, block 14 ($0380) for the multicolor ball.
;
;   sprite 0: normal             sprite 4: multicolor ($D01C): pairs of bits, %01 = $D025, %11 = $D026
;   sprite 1: double width       sprite 5: multicolor, expanded in width and height
;   sprite 2: double height      sprite 6: behind the graphics ($D01B): the text passes over it
;   sprite 3: wide and tall      sprite 7: overlapping sprite 6: a sprite-sprite collision
;
; After a frame the program reads $D01E (sprite-sprite collisions: one bit per sprite involved) and writes its
; value in binary on the screen. Reading the register clears it. The program returns to BASIC.
; ----------------------------------------------------------------------------------------------------------

VIC         = $D000         ; $D000 + 2n: X of sprite n, $D001 + 2n: Y
X_MSB       = $D010         ; ninth bit of the Xs
ENABLE      = $D015
EXPAND_Y    = $D017
PRIORITY    = $D01B         ; bit set: sprite behind the graphics
MULTICOLOR  = $D01C
EXPAND_X    = $D01D
COLLISIONS  = $D01E         ; sprite-sprite (cleared when read)
MCOLOR0     = $D025
MCOLOR1     = $D026
COLORS      = $D027         ; $D027 + n: color of sprite n
RASTER      = $D012
POINTERS    = $07F8
SCREEN      = $0400
CLRSCR      = $E544
SMILEY      = $0340         ; block 13
BALL        = $0380         ; block 14
TEXT_ROW    = SCREEN+40*14  ; row of the text that passes over sprite 6
RESULT      = SCREEN+40*22

            * = $0801
            .basic

            JSR CLRSCR
            LDX #62
data:       LDA smiley,X                ; the sprite data into blocks 13 and 14
            STA SMILEY,X
            LDA ball,X
            STA BALL,X
            DEX
            BPL data

            LDX #0
setup:      LDA blocks,X
            STA POINTERS,X              ; block of each sprite
            LDA colors,X
            STA COLORS,X
            INX
            CPX #8
            BNE setup

            LDX #15                     ; X and Y of the 8 sprites (consecutive pairs starting at $D000)
position:   LDA positions,X
            STA VIC,X
            DEX
            BPL position

            LDA #0
            STA X_MSB                   ; all the Xs below 256
            LDA #%00101010              ; sprites 1, 3, 5 double width
            STA EXPAND_X
            LDA #%00101100              ; sprites 2, 3, 5 double height
            STA EXPAND_Y
            LDA #%00110000              ; sprites 4 and 5 multicolor
            STA MULTICOLOR
            LDA #1
            STA MCOLOR0                 ; %01 white
            LDA #0
            STA MCOLOR1                 ; %11 black
            LDA #%01000000              ; sprite 6 behind the graphics
            STA PRIORITY
            LDA #%11111111
            STA ENABLE                  ; turns all 8 on

            LDX #0
text:       LDA message,X               ; text crossing sprite 6
            CMP #$FF
            BEQ wait
            STA TEXT_ROW,X
            INX
            BNE text

wait:       LDA RASTER                  ; wait a whole frame: collisions are detected while drawing,
            CMP #255                    ; so the VIC-II must have drawn all the sprite lines
            BNE wait
next_line:  LDA RASTER
            CMP #255
            BEQ next_line
wait_again: LDA RASTER
            CMP #255
            BNE wait_again
            LDA COLLISIONS              ; reads (and clears) the sprite-sprite collisions

            LDX #0                      ; writes the value in binary: 8 digits, sprite 7 on the left
bits:       ASL
            PHA
            LDA #'0'
            ADC #0                      ; the bit shifted out is in the carry: '0' + carry = '0' or '1'
            STA RESULT+12,X
            PLA
            INX
            CPX #8
            BNE bits

            LDX #0
label:      LDA caption,X
            CMP #$FF
            BEQ done
            STA RESULT,X
            INX
            BNE label
done:       RTS

blocks:     .byte 13, 13, 13, 13, 14, 14, 13, 13
colors:     .byte 7, 3, 5, 10, 2, 4, 13, 14
positions:  .byte 24,90, 56,90, 112,90, 144,90, 200,90, 230,90, 120,150, 132,156

message:    .screen "THIS TEXT PASSES OVER SPRITE 6"
            .byte $FF
caption:    .screen "COLLISIONS:"
            .byte $FF

; Smiley, 24 x 21 pixels, one bit per pixel
smiley:     .byte %00000000, %11111111, %00000000
            .byte %00000011, %11111111, %11000000
            .byte %00000111, %11111111, %11100000
            .byte %00001111, %11111111, %11110000
            .byte %00011110, %01111110, %01111000
            .byte %00011110, %01111110, %01111000
            .byte %00111111, %11111111, %11111100
            .byte %00111111, %11111111, %11111100
            .byte %00111111, %11111111, %11111100
            .byte %00111111, %11111111, %11111100
            .byte %00111011, %11111111, %11011100
            .byte %00011101, %11111111, %10111000
            .byte %00011110, %11111111, %01111000
            .byte %00001111, %00000000, %11110000
            .byte %00000111, %11111111, %11100000
            .byte %00000011, %11111111, %11000000
            .byte %00000000, %11111111, %00000000
            .fill 12, 0

; Multicolor ball, 12 x 21 wide pixels: %01 = white ($D025), %10 = sprite color, %11 = black ($D026)
ball:       .byte %00000000, %11111111, %00000000
            .byte %00001111, %10101010, %11110000
            .byte %00111010, %10101010, %10101100
            .byte %00111010, %01011010, %10101100
            .byte %11101001, %01010110, %10101011
            .byte %11101001, %01010110, %10101011
            .byte %11101010, %01011010, %10101011
            .byte %11101010, %10101010, %10101011
            .byte %11101010, %10101010, %10101011
            .byte %11101010, %10101010, %10101011
            .byte %11101010, %10101010, %10101011
            .byte %00111010, %10101010, %10101100
            .byte %00111010, %10101010, %10101100
            .byte %00001111, %10101010, %11110000
            .byte %00000000, %11111111, %00000000
            .fill 18, 0
