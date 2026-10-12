; ----------------------------------------------------------------------------------------------------------
; NMI - l'interrupt non mascherabile, generato dal timer della CIA 2
;
; Sul C64 l'uscita /IRQ della CIA 2 è collegata alla linea /NMI della CPU (insieme al tasto RESTORE).
; A differenza dell'IRQ, l'NMI non si può disabilitare con SEI e scatta sul fronte di discesa della linea:
; una volta servito, il prossimo arriva solo dopo che la linea è tornata alta, cioè dopo aver letto
; il registro degli interrupt della CIA ($DD0D), che azzera i flag e la rilascia.
;
; Il KERNAL, all'arrivo di un NMI, esegue SEI e salta all'indirizzo nel vettore $0318 SENZA salvare i
; registri: il gestore deve salvarli e terminare con RTI. Qui il timer A della CIA 2 genera un NMI ogni
; 50000 cicli (circa 20 volte al secondo) e ogni 10 NMI il bordo cambia colore. Il BASIC resta utilizzabile.
; ----------------------------------------------------------------------------------------------------------

NMI_VECTOR  = $0318         ; vettore NMI del KERNAL
CIA2_TA_LO  = $DD04         ; timer A: valore di partenza (latch), byte basso
CIA2_TA_HI  = $DD05         ;          byte alto
CIA2_ICR    = $DD0D         ; interrupt: in scrittura abilita (bit 7 = 1) o disabilita, in lettura li azzera
CIA2_CRA    = $DD0E         ; controllo del timer A
BORDER      = $D020
COUNT       = $FB
PERIOD      = 10            ; NMI fra un cambio di colore e l'altro
TICKS       = 50000         ; cicli fra due NMI

            * = $0801
            .basic

            LDA #<nmi
            STA NMI_VECTOR
            LDA #>nmi
            STA NMI_VECTOR+1
            LDA #PERIOD
            STA COUNT
            LDA #<TICKS
            STA CIA2_TA_LO
            LDA #>TICKS
            STA CIA2_TA_HI
            LDA #%10000001              ; bit 7 = abilita, bit 0 = timer A
            STA CIA2_ICR
            LDA #%00010001              ; bit 4 = carica il latch, bit 0 = avvia; bit 3 = 0: continuo
            STA CIA2_CRA
            RTS

nmi:        PHA                         ; il KERNAL non ha salvato nulla: salviamo A
            LDA CIA2_ICR                ; conferma: azzera i flag e rilascia la linea /NMI
            DEC COUNT
            BNE done
            LDA #PERIOD
            STA COUNT
            INC BORDER                  ; prossimo colore (il VIC-II usa solo i 4 bit bassi)
done:       PLA
            RTI                         ; ripristina anche lo stato (e il flag I) di prima dell'NMI
