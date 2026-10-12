; ----------------------------------------------------------------------------------------------------------
; SYS - un programma in linguaggio macchina a $C000, avviato con SYS 49152
;
; $C000-$CFFF sono 4 KB di RAM che il BASIC non usa: il posto classico per le routine in linguaggio macchina.
; Un file .prg che inizia a $C000 non è un programma BASIC: dopo il caricamento non si scrive RUN ma
; SYS 49152 (49152 = $C000). SYS salta all'indirizzo e, quando la routine esegue RTS, torna al BASIC.
;
; La routine cambia i colori e scrive un messaggio con CHROUT, la routine del KERNAL che stampa un carattere
; alla posizione del cursore (come PRINT).
; ----------------------------------------------------------------------------------------------------------

BORDER      = $D020
BACKGROUND  = $D021
CHROUT      = $FFD2
RETURN      = 13            ; PETSCII: a capo

            * = $C000

start:      LDA #0
            STA BORDER                  ; nero
            LDA #11
            STA BACKGROUND              ; grigio scuro
            LDX #0
print:      LDA message,X
            BEQ done
            JSR CHROUT
            INX
            BNE print
done:       RTS                         ; torna al BASIC, che scrive READY.

message:    .byte RETURN
            .text "CIAO DA $C000! (SYS 49152)"
            .byte RETURN, 0
