; ----------------------------------------------------------------------------------------------------------
; CARTRIDGE - cartuccia da 8 KB che parte da sola (diventa hello.crt)
;
; Una cartuccia "normale" da 8 KB tiene bassa la linea EXROM: il PLA mostra la sua ROM (ROML) a $8000-$9FFF.
; All'accensione il KERNAL controlla se a $8004 c'è la firma "CBM80" (in PETSCII, con il bit 7 delle lettere
; a 1): se c'è, invece di avviare il BASIC salta all'indirizzo scritto a $8000 (avvio a freddo).
; A quel punto il sistema non è ancora inizializzato: tocca alla cartuccia chiamare le routine del KERNAL
; che preparano CIA, memoria, vettori e schermo.
; ----------------------------------------------------------------------------------------------------------

IOINIT      = $FF84         ; inizializza CIA, SID, porte
RAMTAS      = $FF87         ; azzera la pagina zero, prova la RAM, imposta i puntatori
RESTOR      = $FF8A         ; vettori del KERNAL predefiniti
CINT        = $FF81         ; VIC-II e schermo (pulisce lo schermo)
CHROUT      = $FFD2         ; scrive il carattere in A alla posizione del cursore

            * = $8000
            .word start                 ; avvio a freddo (all'accensione e al reset)
            .word start                 ; avvio a caldo (RUN/STOP + RESTORE)
            .byte $C3, $C2, $CD, '8', '0'   ; firma "CBM80"

start:      JSR IOINIT
            JSR RAMTAS
            JSR RESTOR
            JSR CINT
            LDX #0
print:      LDA message,X
            BEQ done                    ; il messaggio termina con 0
            JSR CHROUT
            INX
            BNE print
done:       JMP done                    ; nessun BASIC da cui tornare: resta qui

message:    .text "HELLO FROM CARTRIDGE"
            .byte 0
