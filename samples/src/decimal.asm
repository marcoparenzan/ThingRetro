; ----------------------------------------------------------------------------------------------------------
; DECIMAL - il modo decimale (BCD) del 6510: un contatore a 4 cifre
;
; Con il flag D attivo (SED) ADC e SBC lavorano in BCD: ogni byte contiene due cifre decimali, una per
; nibble ($09 + $01 = $10, $99 + $01 = $00 con riporto). Così un numero si mostra a schermo senza divisioni:
; ogni nibble è già una cifra.
;
; Attenzione: un interrupt non azzera il flag D, e il gestore del KERNAL non lo fa. Per questo la parte in
; modo decimale è racchiusa fra SEI e CLI: se arrivasse un IRQ con D attivo, i calcoli del KERNAL
; (l'orologio TI, per esempio) verrebbero sbagliati.
;
; Il contatore aumenta di uno a ogni frame (50 volte al secondo). Il programma non torna al BASIC.
; ----------------------------------------------------------------------------------------------------------

SCREEN      = $0400
RASTER      = $D012         ; riga raster attuale (8 bit bassi)
CLRSCR      = $E544         ; routine del KERNAL che pulisce lo schermo
LOW         = $FB           ; cifre delle decine e delle unità (BCD)
HIGH        = $FC           ; cifre delle migliaia e delle centinaia (BCD)
ZERO        = $30           ; codice schermo della cifra 0
TEXT_COLUMN = 6

            * = $0801
            .basic

            JSR CLRSCR
            LDX #0
title:      LDA text,X                  ; descrizione accanto alle cifre
            CMP #$FF
            BEQ start
            STA SCREEN+TEXT_COLUMN,X
            INX
            BNE title

start:      LDA #0
            STA LOW
            STA HIGH

loop:       LDA RASTER                  ; aspetta la riga 255: una volta per frame
            CMP #255
            BNE loop

            SEI
            SED                         ; da qui ADC lavora in BCD
            CLC
            LDA LOW
            ADC #1
            STA LOW
            LDA HIGH
            ADC #0                      ; il riporto (da 99 a 00) passa alle centinaia
            STA HIGH
            CLD                         ; di nuovo binario prima di riabilitare gli interrupt
            CLI

            LDX #0                      ; colonna sullo schermo
            LDA HIGH
            JSR digits
            LDA LOW
            JSR digits

wait:       LDA RASTER                  ; aspetta che la riga 255 sia passata, per non contare due volte
            CMP #255
            BEQ wait
            JMP loop

; Scrive le due cifre BCD di A a SCREEN+X e avanza X di 2
digits:     PHA
            LSR                         ; nibble alto: cifra di sinistra
            LSR
            LSR
            LSR
            ORA #ZERO
            STA SCREEN,X
            INX
            PLA
            AND #%00001111              ; nibble basso: cifra di destra
            ORA #ZERO
            STA SCREEN,X
            INX
            RTS

text:       .screen "<- CONTATORE BCD, +1 PER FRAME"
            .byte $FF                   ; fine del testo (0 è il codice schermo di @)
