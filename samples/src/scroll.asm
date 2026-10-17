; ----------------------------------------------------------------------------------------------------------
; SCROLL - horizontal fine scrolling of a line of text
;
; Bits 0-2 of $D016 (XSCROLL) move all the graphics 0-7 pixels to the right. To scroll a text to the left,
; XSCROLL goes down from 7 to 0 (one pixel per frame) and, when it starts again from 7, the characters of the
; line move one position to the left and the next character of the message is added on the right. With bit 3
; at 0 the screen is 38 columns wide: the border covers the columns that come in and go out.
;
; XSCROLL applies to the whole screen: to scroll a single line the program changes $D016 with the raster,
; just before the line (scrolling and 38 columns) and right after it (still again and 40 columns).
; The program does not return to BASIC.
; ----------------------------------------------------------------------------------------------------------

SCREEN      = $0400
VIC_CR2     = $D016         ; bits 0-2 = XSCROLL, bit 3 = 40 columns
RASTER      = $D012
CLRSCR      = $E544
TEXT_ROW    = 12
LINE        = SCREEN+40*TEXT_ROW
FIRST_LINE  = 51+8*TEXT_ROW ; first raster line of the text row (row 0 starts at raster line 51)
SCROLL      = $FB           ; current XSCROLL value (7-0)
POSITION    = $FC           ; next character of the message

            * = $0801
            .basic

            JSR CLRSCR
            LDA #7
            STA SCROLL
            LDA #0
            STA POSITION

frame:      LDA RASTER                  ; just before the line: scrolling on and 38 columns
            CMP #FIRST_LINE-4
            BNE frame
            LDA SCROLL
            STA VIC_CR2

after:      LDA RASTER                  ; right after the line: still again, 40 columns
            CMP #FIRST_LINE+12
            BNE after
            LDA #%00001000
            STA VIC_CR2

            DEC SCROLL                  ; one pixel to the left per frame
            BPL frame
            LDA #7                      ; eight pixels done: the line moves by one character
            STA SCROLL

            LDX #0
shift:      LDA LINE+1,X
            STA LINE,X
            INX
            CPX #39
            BNE shift

            LDY POSITION
            LDA message,Y
            CMP #$FF                    ; end of the message: start again
            BNE append
            LDY #0
            STY POSITION
            LDA message
append:     STA LINE+39
            INC POSITION
            JMP frame

message:    .screen "THING65: A COMMODORE 64 EMULATOR IN C#, NOW WITH FINE SCROLLING...      "
            .byte $FF
