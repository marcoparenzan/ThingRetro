; ----------------------------------------------------------------------------------------------------------
; IRQ - agganciarsi all'interrupt del timer della CIA 1
;
; Il KERNAL programma il timer A della CIA 1 perché generi un IRQ 60 volte al secondo: a ogni interrupt
; legge la tastiera, fa lampeggiare il cursore e aggiorna l'orologio TI. Prima di eseguire il proprio
; gestore il KERNAL salva A, X e Y e salta all'indirizzo scritto nel vettore $0314.
;
; Questo programma mette nel vettore un proprio gestore: ogni 30 interrupt (mezzo secondo) cambia il colore
; dello sfondo fra blu e nero, poi prosegue nel gestore del KERNAL. Così tastiera e BASIC continuano a
; funzionare: il programma torna al BASIC e lo sfondo lampeggia "da solo".
; ----------------------------------------------------------------------------------------------------------

IRQ_VECTOR  = $0314         ; vettore IRQ del KERNAL (normalmente punta a $EA31)
KERNAL_IRQ  = $EA31         ; gestore del KERNAL: tastiera, cursore, orologio, poi RTI
BACKGROUND  = $D021         ; colore dello sfondo
COUNT       = $FB           ; contatore degli interrupt (pagina zero libera)
PERIOD      = 30            ; interrupt fra un cambio e l'altro: 30 / 60 Hz = mezzo secondo

            * = $0801
            .basic

            SEI                         ; un IRQ a metà del cambio di vettore salterebbe a un indirizzo sbagliato
            LDA #<irq
            STA IRQ_VECTOR
            LDA #>irq
            STA IRQ_VECTOR+1
            LDA #PERIOD
            STA COUNT
            CLI
            RTS

irq:        DEC COUNT
            BNE kernal
            LDA #PERIOD
            STA COUNT
            LDA BACKGROUND
            EOR #%00000110              ; 6 (blu) <-> 0 (nero): cambia i bit che li distinguono
            STA BACKGROUND
kernal:     JMP KERNAL_IRQ              ; il resto del lavoro lo fa il KERNAL, che termina con RTI
