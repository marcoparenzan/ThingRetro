; ----------------------------------------------------------------------------------------------------------
; BANKS (banco 0) - cartuccia Magic Desk con più banchi da 8 KB (diventa banks.crt con banks1.asm)
;
; Una cartuccia può contenere più di 8 KB mostrandone 8 alla volta a $8000-$9FFF. Nelle Magic Desk il banco
; si sceglie scrivendo il suo numero a $DE00 (area I/O 1, collegata alla porta di espansione); il bit 7 a 1
; scollega la cartuccia.
;
; Il problema: cambiare banco mentre la CPU esegue codice da $8000 cambia il codice sotto i suoi piedi.
; Per questo la routine che cambia banco viene copiata in RAM ($C000) ed eseguita da lì: legge il testo a
; $8100 nel banco 0 e nel banco 1, lo stampa, e rimette il banco 0 prima di tornare.
; ----------------------------------------------------------------------------------------------------------

IOINIT      = $FF84
RAMTAS      = $FF87
RESTOR      = $FF8A
CINT        = $FF81
CHROUT      = $FFD2
BANK        = $DE00         ; registro del banco della Magic Desk
TEXT        = $8100         ; ogni banco ha il suo testo qui
RAM_ROUTINE = $C000         ; dove si copia la routine che cambia banco

            * = $8000
            .word start, start
            .byte $C3, $C2, $CD, '8', '0'   ; firma "CBM80": avvio automatico

start:      JSR IOINIT
            JSR RAMTAS
            JSR RESTOR
            JSR CINT

            LDX #routine_end-routine-1
copy:       LDA routine,X
            STA RAM_ROUTINE,X
            DEX
            BPL copy
            JSR RAM_ROUTINE
done:       JMP done                    ; di nuovo nel banco 0: questo codice c'è ancora

; Questa parte viene eseguita a $C000, non dove è stata assemblata: usa solo salti relativi e indirizzi fissi
routine:    LDY #0                      ; banco da leggere
next_bank:  STY BANK                    ; da qui $8000-$9FFF mostra il banco Y
            LDX #0
print:      LDA TEXT,X
            BEQ printed
            JSR CHROUT
            INX
            BNE print
printed:    INY
            CPY #2
            BNE next_bank
            LDA #0
            STA BANK                    ; di nuovo il banco 0, prima di tornare al codice in ROM
            RTS
routine_end:

            * = TEXT
            .text "QUESTO TESTO E' NEL BANCO 0"
            .byte 13, 0
