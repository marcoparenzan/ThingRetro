; ----------------------------------------------------------------------------------------------------------
; BITMAP - il modo bitmap ad alta risoluzione (320 x 200 pixel, 2 colori per cella di 8 x 8)
;
; In modo bitmap ogni pixel è un bit in memoria (8000 byte). La memoria NON è ordinata per righe dello
; schermo ma per celle di 8 x 8 pixel, come i caratteri: i primi 8 byte sono la prima cella in alto a
; sinistra (una riga di pixel per byte), i successivi 8 la cella alla sua destra, e così via per 40 celle;
; poi la seconda riga di celle. Per ogni cella i due colori vengono dalla memoria video ($0400):
; nibble alto = colore dei bit a 1, nibble basso = colore dei bit a 0.
;
; Il programma riempie ogni cella con una diagonale e cambia verso a ogni colonna di celle: uno zig-zag.
; Resta in modo bitmap: per tornare al BASIC serve un reset.
; ----------------------------------------------------------------------------------------------------------

VIC_CR1     = $D011         ; bit 5 = modo bitmap, bit 4 = display attivo, bit 3 = 25 righe
VIC_MEMORY  = $D018         ; bit 4-7: memoria video / 1 KB; bit 3: bitmap a $0000 o $2000
SCREEN      = $0400
BITMAP      = $2000
POINTER     = $FB           ; puntatore in pagina zero ($FB-$FC sono liberi per i programmi)
ROW         = $FD           ; riga di pixel nella cella (0-7)

            * = $0801
            .basic

            LDA #$3B
            STA VIC_CR1                 ; bitmap + display attivo + 25 righe + YSCROLL 3
            LDA #$18
            STA VIC_MEMORY              ; memoria video a $0400 (1 x 1 KB), bitmap a $2000

; Colori: i bit a 1 azzurri (14), quelli a 0 blu (6), in tutte le 1000 celle
            LDX #0
            LDA #$E6
color:      STA SCREEN,X
            STA SCREEN+$100,X
            STA SCREEN+$200,X
            STA SCREEN+$300,X
            INX
            BNE color

; Pixel: Y scorre i 256 byte di una pagina, POINTER punta alla pagina; 32 celle per pagina
            LDA #<BITMAP
            STA POINTER
            LDA #>BITMAP
            STA POINTER+1
            LDY #0
fill:       TYA
            AND #%00000111              ; riga di pixel nella cella
            STA ROW
            TYA
            AND #%00001000              ; cella pari o dispari (Y / 8): sceglie il verso della diagonale
            ORA ROW
            TAX
            LDA diagonals,X
            STA (POINTER),Y
            INY
            BNE fill
            INC POINTER+1               ; pagina successiva
            LDA POINTER+1
            CMP #>BITMAP+$2000          ; fino alla fine degli 8 KB
            BNE fill

done:       JMP done                    ; resta qui: il display rimane in bitmap

diagonals:  .byte %10000000, %01000000, %00100000, %00010000   ; diagonale che scende verso destra
            .byte %00001000, %00000100, %00000010, %00000001
            .byte %00000001, %00000010, %00000100, %00001000   ; diagonale che sale verso destra
            .byte %00010000, %00100000, %01000000, %10000000
