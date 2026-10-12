; ----------------------------------------------------------------------------------------------------------
; MCBITMAP - bitmap multicolor: 160 x 200 pixel, 4 colori per cella
;
; Come la bitmap ad alta risoluzione, ma con il multicolor attivo ($D016 bit 4): ogni coppia di bit è un
; pixel largo il doppio e sceglie fra 4 colori, i primi tre diversi per ogni cella di 8 x 8:
;   %00 = sfondo ($D021)   %01 = memoria video, nibble alto   %10 = memoria video, nibble basso
;   %11 = color RAM
;
; Il programma divide ogni cella in quattro strisce orizzontali, una per sorgente di colore. La color RAM
; cambia di cella in cella, quindi la striscia in basso è a colori diversi. Il programma non torna al BASIC.
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
            STA VIC_CR1                 ; bitmap, display attivo, 25 righe
            LDA VIC_CR2
            ORA #%00010000
            STA VIC_CR2                 ; multicolor
            LDA #$18
            STA VIC_MEMORY              ; memoria video a $0400, bitmap a $2000
            LDA #0
            STA BACKGROUND              ; %00 nero

            LDX #0
colors:     LDA #$27                    ; %01 rosso (2), %10 giallo (7)
            STA SCREEN,X
            STA SCREEN+$100,X
            STA SCREEN+$200,X
            STA SCREEN+$300,X
            TXA
            AND #%00001111              ; %11: un colore diverso per ogni cella (si ripete ogni 16)
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
            AND #%00000111              ; riga di pixel nella cella (0-7)
            LSR                         ; due righe per striscia: 0-3
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

stripes:    .byte %00000000             ; righe 0-1: sfondo
            .byte %01010101             ; righe 2-3: nibble alto della memoria video
            .byte %10101010             ; righe 4-5: nibble basso della memoria video
            .byte %11111111             ; righe 6-7: color RAM
