; ----------------------------------------------------------------------------------------------------------
; ULTIMAX - una cartuccia in modo Ultimax: il C64 senza KERNAL (diventa ultimax.crt)
;
; Una cartuccia che tiene bassa la linea GAME (e alta EXROM) mette il C64 in modo Ultimax, pensato per la
; console MAX Machine: la ROM della cartuccia (ROMH) prende il posto del KERNAL a $E000-$FFFF, quindi anche
; dei vettori di reset, NMI e IRQ a $FFFA-$FFFF. Della memoria restano visibili solo $0000-$0FFF, l'I/O e
; le ROM della cartuccia: il resto non è collegato. Il BASIC e il KERNAL non ci sono: tutto è da fare.
;
; Il codice parte dal vettore di reset, spegne il display (il bordo copre tutto lo schermo) e a ogni nuova
; riga raster cambia il colore del bordo: un arcobaleno di righe orizzontali.
; ----------------------------------------------------------------------------------------------------------

VIC_CR1     = $D011
RASTER      = $D012
BORDER      = $D020

            * = $E000

reset:      SEI                         ; nessuno ha preparato la macchina: si parte da zero
            CLD
            LDX #$FF
            TXS                         ; stack in cima alla pagina 1
            LDA #%00001011              ; display spento (bit 4 = 0), 25 righe, YSCROLL 3
            STA VIC_CR1

loop:       LDA RASTER
same_line:  CMP RASTER                  ; aspetta che la riga raster cambi
            BEQ same_line
            LDA RASTER
            LSR                         ; un colore ogni due righe
            STA BORDER                  ; il VIC-II usa solo i 4 bit bassi: 16 colori che si ripetono
            JMP loop

nmi:        RTI                         ; nessun NMI previsto
irq:        RTI                         ; nessun IRQ abilitato

            * = $FFFA                   ; vettori della CPU, alla fine della ROMH
            .word nmi                   ; $FFFA: NMI
            .word reset                 ; $FFFC: reset
            .word irq                   ; $FFFE: IRQ e BRK
