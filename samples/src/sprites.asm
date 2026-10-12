; ----------------------------------------------------------------------------------------------------------
; SPRITES - otto sprite: espansione, multicolor, priorità e collisioni
;
; Il VIC-II disegna fino a 8 sprite di 24 x 21 pixel sopra (o sotto) la grafica. Ogni sprite legge 63 byte
; da un blocco di 64 nel banco del VIC-II: il numero del blocco sta negli ultimi 8 byte della memoria video
; ($07F8-$07FF con lo schermo a $0400). Qui i dati vanno nel buffer della cassetta: blocco 13 ($0340) per
; lo smile, blocco 14 ($0380) per la palla multicolor.
;
;   sprite 0: normale          sprite 4: multicolor ($D01C): coppie di bit, %01 = $D025, %11 = $D026
;   sprite 1: largo il doppio  sprite 5: multicolor, espanso in larghezza e altezza
;   sprite 2: alto il doppio   sprite 6: dietro la grafica ($D01B): il testo gli passa sopra
;   sprite 3: largo e alto     sprite 7: sovrapposto al 6: collisione fra sprite
;
; Dopo un frame il programma legge $D01E (collisioni fra sprite: un bit per sprite coinvolto) e ne scrive
; il valore in binario sullo schermo. Leggere il registro lo azzera. Il programma torna al BASIC.
; ----------------------------------------------------------------------------------------------------------

VIC         = $D000         ; $D000 + 2n: X dello sprite n, $D001 + 2n: Y
X_MSB       = $D010         ; nono bit delle X
ENABLE      = $D015
EXPAND_Y    = $D017
PRIORITY    = $D01B         ; bit a 1: sprite dietro la grafica
MULTICOLOR  = $D01C
EXPAND_X    = $D01D
COLLISIONS  = $D01E         ; sprite-sprite (si azzera alla lettura)
MCOLOR0     = $D025
MCOLOR1     = $D026
COLORS      = $D027         ; $D027 + n: colore dello sprite n
RASTER      = $D012
POINTERS    = $07F8
SCREEN      = $0400
CLRSCR      = $E544
SMILEY      = $0340         ; blocco 13
BALL        = $0380         ; blocco 14
TEXT_ROW    = SCREEN+40*14  ; riga del testo che passa sopra lo sprite 6
RESULT      = SCREEN+40*22

            * = $0801
            .basic

            JSR CLRSCR
            LDX #62
data:       LDA smiley,X                ; i dati degli sprite nei blocchi 13 e 14
            STA SMILEY,X
            LDA ball,X
            STA BALL,X
            DEX
            BPL data

            LDX #0
setup:      LDA blocks,X
            STA POINTERS,X              ; blocco di ciascuno sprite
            LDA colors,X
            STA COLORS,X
            INX
            CPX #8
            BNE setup

            LDX #15                     ; X e Y degli 8 sprite (coppie consecutive a partire da $D000)
position:   LDA positions,X
            STA VIC,X
            DEX
            BPL position

            LDA #0
            STA X_MSB                   ; tutte le X sotto 256
            LDA #%00101010              ; sprite 1, 3, 5 larghi il doppio
            STA EXPAND_X
            LDA #%00101100              ; sprite 2, 3, 5 alti il doppio
            STA EXPAND_Y
            LDA #%00110000              ; sprite 4 e 5 multicolor
            STA MULTICOLOR
            LDA #1
            STA MCOLOR0                 ; %01 bianco
            LDA #0
            STA MCOLOR1                 ; %11 nero
            LDA #%01000000              ; sprite 6 dietro la grafica
            STA PRIORITY
            LDA #%11111111
            STA ENABLE                  ; accende tutti e 8

            LDX #0
text:       LDA message,X               ; testo che attraversa lo sprite 6
            CMP #$FF
            BEQ wait
            STA TEXT_ROW,X
            INX
            BNE text

wait:       LDA RASTER                  ; aspetta un frame intero: le collisioni si rilevano mentre si disegna,
            CMP #255                    ; quindi serve che il VIC-II abbia disegnato tutte le righe degli sprite
            BNE wait
next_line:  LDA RASTER
            CMP #255
            BEQ next_line
wait_again: LDA RASTER
            CMP #255
            BNE wait_again
            LDA COLLISIONS              ; legge (e azzera) le collisioni fra sprite

            LDX #0                      ; scrive il valore in binario: 8 cifre, sprite 7 a sinistra
bits:       ASL
            PHA
            LDA #'0'
            ADC #0                      ; il bit uscito è nel carry: '0' + carry = '0' o '1'
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

message:    .screen "IL TESTO PASSA SOPRA LO SPRITE 6"
            .byte $FF
caption:    .screen "COLLISIONI:"
            .byte $FF

; Smile, 24 x 21 pixel, un bit per pixel
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

; Palla multicolor, 12 x 21 pixel larghi: %01 = bianco ($D025), %10 = colore dello sprite, %11 = nero ($D026)
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
