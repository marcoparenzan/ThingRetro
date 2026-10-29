; ----------------------------------------------------------------------------------------------------------
; POINTER - the arrow on the screen: two hires sprites, a black outline (0, in front) over a white fill (1).
; The hot spot is the top left pixel of the arrow
; ----------------------------------------------------------------------------------------------------------

SPRITE_LEFT = 24            ; screen pixel (0, 0) in sprite coordinates
SPRITE_TOP  = 50

; Keeps the pointer inside 0-319, 0-199 and moves the sprites there
place_pointer:
            LDA pointer_x+1
            BPL +
            LDA #0                      ; past the left edge
            STA pointer_x
            STA pointer_x+1
            BEQ @y
+           BEQ @y                      ; 0-255
            CMP #1
            BNE @right
            LDA pointer_x
            CMP #<320
            BCC @y
@right:     LDA #<319
            STA pointer_x
            LDA #>319
            STA pointer_x+1

@y:         LDA pointer_y+1
            BPL +
            LDA #0                      ; past the top
            STA pointer_y
            STA pointer_y+1
            BEQ @place
+           BNE @bottom
            LDA pointer_y
            CMP #200
            BCC @place
@bottom:    LDA #199
            STA pointer_y
            LDA #0
            STA pointer_y+1

@place:     CLC
            LDA pointer_x
            ADC #SPRITE_LEFT
            STA VIC_SPR0X
            STA VIC_SPR1X
            LDA pointer_x+1
            ADC #0
            BEQ +
            LDA #%00000011              ; bit 8 of X for both sprites
+           STA irq_t0
            LDA VIC_SPRXHI
            AND #%11111100              ; the other sprites are the application's
            ORA irq_t0
            STA VIC_SPRXHI
            CLC
            LDA pointer_y
            ADC #SPRITE_TOP
            STA VIC_SPR0Y
            STA VIC_SPR1Y
            RTS

; 21 rows of 3 bytes plus one unused byte, for each sprite
pointer_shape:
            ; outline
            !byte %#......., %........, 0
            !byte %##......, %........, 0
            !byte %#.#....., %........, 0
            !byte %#..#...., %........, 0
            !byte %#...#..., %........, 0
            !byte %#....#.., %........, 0
            !byte %#.....#., %........, 0
            !byte %#......#, %........, 0
            !byte %#......., %#......., 0
            !byte %#......., %.#......, 0
            !byte %#.....##, %###....., 0
            !byte %#..#..#., %........, 0
            !byte %#.#.#..#, %........, 0
            !byte %##..#..#, %........, 0
            !byte %#....#.., %#......., 0
            !byte %.....#.., %#......., 0
            !byte %......##, %........, 0
            !fill 4 * 3, 0
            !byte 0
            ; fill
            !byte %........, %........, 0
            !byte %........, %........, 0
            !byte %.#......, %........, 0
            !byte %.##....., %........, 0
            !byte %.###...., %........, 0
            !byte %.####..., %........, 0
            !byte %.#####.., %........, 0
            !byte %.######., %........, 0
            !byte %.#######, %........, 0
            !byte %.#######, %#......., 0
            !byte %.#####.., %........, 0
            !byte %.##.##.., %........, 0
            !byte %.#...##., %........, 0
            !byte %.....##., %........, 0
            !byte %......##, %........, 0
            !byte %......##, %........, 0
            !byte %........, %........, 0
            !fill 4 * 3, 0
            !byte 0
