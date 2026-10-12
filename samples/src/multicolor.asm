; ----------------------------------------------------------------------------------------------------------
; MULTICOLOR - testo multicolor con un set di caratteri definito in RAM
;
; In modo multicolor ($D016 bit 4) i caratteri con il bit 3 del colore a 1 si disegnano a coppie di bit:
; ogni coppia è un pixel largo il doppio, con 4 colori possibili:
;   %00 = sfondo ($D021)   %01 = $D022   %10 = $D023   %11 = color RAM (bit 0-2)
; I caratteri con il bit 3 del colore a 0 restano normali (8 pixel, 2 colori).
;
; Il programma definisce 4 caratteri, ciascuno pieno di una delle 4 coppie, in un set di caratteri a $3000
; ($D018 dice al VIC-II dove leggerlo), e riempie lo schermo a strisce diagonali. Il colore della color RAM
; cambia a ogni riga, quindi la quarta striscia cambia colore scendendo. Il programma non torna al BASIC.
; ----------------------------------------------------------------------------------------------------------

SCREEN      = $0400
COLOR_RAM   = $D800
CHARSET     = $3000
VIC_CR2     = $D016         ; bit 4 = multicolor
VIC_MEMORY  = $D018         ; bit 4-7: schermo / 1 KB, bit 1-3: caratteri / 2 KB
BACKGROUND  = $D021
COLOR1      = $D022
COLOR2      = $D023
SCREEN_PTR  = $FB           ; puntatore alla riga corrente dello schermo
COLOR_PTR   = $FD           ; puntatore alla riga corrente della color RAM
ROW         = $02           ; riga corrente (0-24)

            * = $0801
            .basic

            LDX #4*8-1                  ; 4 caratteri da 8 byte
copy:       LDA chars,X
            STA CHARSET,X
            DEX
            BPL copy

            LDA #%00011100              ; schermo a $0400 (1 x 1 KB), caratteri a $3000 (6 x 2 KB)
            STA VIC_MEMORY
            LDA VIC_CR2
            ORA #%00010000
            STA VIC_CR2                 ; multicolor attivo
            LDA #6
            STA BACKGROUND              ; blu
            LDA #2
            STA COLOR1                  ; rosso
            LDA #7
            STA COLOR2                  ; giallo

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
            AND #%00000011              ; (colonna + riga) mod 4: strisce diagonali
            STA (SCREEN_PTR),Y
            TXA
            AND #%00000111
            ORA #%00001000              ; bit 3 = carattere multicolor; bit 0-2 = colore di %11
            STA (COLOR_PTR),Y
            INY
            CPY #40
            BNE column

            CLC                         ; riga successiva: +40 a entrambi i puntatori
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

chars:      .fill 8, %00000000          ; carattere 0: tutto %00 (sfondo)
            .fill 8, %01010101          ; carattere 1: tutto %01 ($D022)
            .fill 8, %10101010          ; carattere 2: tutto %10 ($D023)
            .fill 8, %11111111          ; carattere 3: tutto %11 (color RAM)
