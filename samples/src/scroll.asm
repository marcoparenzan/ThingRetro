; ----------------------------------------------------------------------------------------------------------
; SCROLL - scroll fine orizzontale di una riga di testo
;
; I bit 0-2 di $D016 (XSCROLL) spostano tutta la grafica a destra di 0-7 pixel. Per far scorrere un testo
; verso sinistra si fa scendere XSCROLL da 7 a 0 (un pixel per frame) e, quando ricomincia da 7, si spostano
; i caratteri della riga di una posizione a sinistra e si aggiunge il prossimo carattere del messaggio a
; destra. Con il bit 3 a 0 lo schermo è largo 38 colonne: il bordo copre le colonne che entrano ed escono.
;
; XSCROLL vale per tutto lo schermo: per far scorrere una riga sola il programma cambia $D016 con il raster,
; poco prima della riga (scroll e 38 colonne) e subito dopo (di nuovo fermo e a 40 colonne).
; Il programma non torna al BASIC.
; ----------------------------------------------------------------------------------------------------------

SCREEN      = $0400
VIC_CR2     = $D016         ; bit 0-2 = XSCROLL, bit 3 = 40 colonne
RASTER      = $D012
CLRSCR      = $E544
TEXT_ROW    = 12
LINE        = SCREEN+40*TEXT_ROW
FIRST_LINE  = 51+8*TEXT_ROW ; prima riga raster della riga di testo (la riga 0 inizia alla riga raster 51)
SCROLL      = $FB           ; valore attuale di XSCROLL (7-0)
POSITION    = $FC           ; prossimo carattere del messaggio

            * = $0801
            .basic

            JSR CLRSCR
            LDA #7
            STA SCROLL
            LDA #0
            STA POSITION

frame:      LDA RASTER                  ; poco prima della riga: scroll attivo e 38 colonne
            CMP #FIRST_LINE-4
            BNE frame
            LDA SCROLL
            STA VIC_CR2

after:      LDA RASTER                  ; subito dopo la riga: di nuovo fermo, 40 colonne
            CMP #FIRST_LINE+12
            BNE after
            LDA #%00001000
            STA VIC_CR2

            DEC SCROLL                  ; un pixel a sinistra per frame
            BPL frame
            LDA #7                      ; otto pixel fatti: si sposta la riga di un carattere
            STA SCROLL

            LDX #0
shift:      LDA LINE+1,X
            STA LINE,X
            INX
            CPX #39
            BNE shift

            LDY POSITION
            LDA message,Y
            CMP #$FF                    ; fine del messaggio: si ricomincia
            BNE append
            LDY #0
            STY POSITION
            LDA message
append:     STA LINE+39
            INC POSITION
            JMP frame

message:    .screen "FAKE64: UN EMULATORE DEL COMMODORE 64 IN C#, ORA CON LO SCROLL FINE...      "
            .byte $FF
