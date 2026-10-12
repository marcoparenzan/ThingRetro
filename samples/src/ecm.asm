; ----------------------------------------------------------------------------------------------------------
; ECM - extended background color mode: quattro colori di sfondo per i caratteri
;
; In modo ECM ($D011 bit 6) i bit 6 e 7 del codice di schermo non scelgono più il carattere ma il colore di
; sfondo della cella, fra $D021, $D022, $D023 e $D024. Il prezzo: restano solo 64 caratteri (codici 0-63),
; abbastanza per lettere, cifre e punteggiatura.
;
; Il programma scrive la stessa frase quattro volte, una per colore di sfondo, e torna al BASIC: il BASIC
; continua a funzionare in modo ECM, perché lettere e cifre hanno codici sotto 64. Il cursore invece appare
; come un quadratino verde: per lampeggiare inverte il carattere impostando il bit 7, che in ECM sceglie
; lo sfondo $D023.
; ----------------------------------------------------------------------------------------------------------

SCREEN      = $0400
VIC_CR1     = $D011         ; bit 6 = extended color mode
BACKGROUND0 = $D021
BACKGROUND1 = $D022
BACKGROUND2 = $D023
BACKGROUND3 = $D024
CLRSCR      = $E544
ROW2        = SCREEN+40*2   ; righe in cui scrivere
ROW4        = SCREEN+40*4
ROW6        = SCREEN+40*6
ROW8        = SCREEN+40*8

            * = $0801
            .basic

            JSR CLRSCR
            LDA #6
            STA BACKGROUND0             ; blu
            LDA #2
            STA BACKGROUND1             ; rosso
            LDA #5
            STA BACKGROUND2             ; verde
            LDA #0
            STA BACKGROUND3             ; nero
            LDA VIC_CR1
            ORA #%01000000
            STA VIC_CR1                 ; ECM attivo

            LDX #0
copy:       LDA text,X
            CMP #$FF
            BEQ done
            STA ROW2,X                  ; bit 7-6 = %00: sfondo $D021
            ORA #%01000000
            STA ROW4,X                  ; %01: $D022
            EOR #%11000000
            STA ROW6,X                  ; %10: $D023
            ORA #%11000000
            STA ROW8,X                  ; %11: $D024
            INX
            BNE copy
done:       RTS

text:       .screen "EXTENDED COLOR MODE: 4 SFONDI"
            .byte $FF
