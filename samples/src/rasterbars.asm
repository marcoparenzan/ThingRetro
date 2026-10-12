; ----------------------------------------------------------------------------------------------------------
; RASTERBARS - una barra rossa nel bordo, disegnata con il raster interrupt del VIC-II
;
; Il VIC-II disegna lo schermo riga per riga (312 righe raster in PAL). Il registro $D012 contiene la riga
; in corso; scrivendoci un valore si chiede al VIC-II di generare un interrupt (IRQ) quando il pennello
; elettronico arriva a quella riga. Qui:
;   - a riga 100 il bordo diventa rosso e si chiede il prossimo interrupt a riga 150
;   - a riga 150 il bordo torna azzurro e si chiede il prossimo interrupt a riga 100
; Il risultato è una barra rossa ferma nel bordo, mentre il BASIC continua a funzionare.
;
; Il vettore di IRQ ($0314) è condiviso con il timer della CIA 1, che 60 volte al secondo fa leggere la
; tastiera al KERNAL: il gestore controlla chi ha chiesto l'interrupt e passa al KERNAL quelli della CIA.
; ----------------------------------------------------------------------------------------------------------

VIC_CR1     = $D011         ; controllo 1: il bit 7 è il nono bit della riga raster
VIC_RASTER  = $D012         ; riga raster (in lettura: quella attuale; in scrittura: quella dell'interrupt)
VIC_IRQ     = $D019         ; interrupt avvenuti (bit 7 = il VIC-II tiene bassa la linea /IRQ)
VIC_IRQEN   = $D01A         ; interrupt abilitati (bit 0 = raster)
BORDER      = $D020         ; colore del bordo
IRQ_VECTOR  = $0314         ; il KERNAL salta qui a ogni IRQ (dopo aver salvato i registri)
KERNAL_IRQ  = $EA31         ; gestore IRQ del KERNAL: tastiera, cursore, orologio
KERNAL_RTI  = $EA81         ; fine del gestore: ripristina A, X, Y e fa RTI

RED         = 2
LIGHT_BLUE  = 14
TOP         = 100           ; riga in cui inizia la barra
BOTTOM      = 150           ; riga in cui finisce
MIDDLE      = 120           ; una riga qualsiasi fra le due, per capire quale interrupt è arrivato

            * = $0801
            .basic                      ; 10 SYS2061: RUN fa partire il codice che segue

            SEI                         ; niente interrupt mentre si cambia il vettore
            LDA #$01
            STA VIC_IRQEN               ; abilita l'interrupt raster
            LDA #TOP
            STA VIC_RASTER              ; primo interrupt a riga 100...
            LDA VIC_CR1
            AND #$7F
            STA VIC_CR1                 ; ...con il nono bit a 0 (righe 0-255)
            LDA #<irq
            STA IRQ_VECTOR
            LDA #>irq
            STA IRQ_VECTOR+1            ; da ora gli IRQ passano dal nostro gestore
            CLI
            RTS                         ; torna al BASIC: la barra va avanti da sola

; Gestore degli interrupt: lo chiama il KERNAL con A, X e Y già salvati sullo stack
irq:        LDA VIC_IRQ
            BMI raster                  ; bit 7 a 1: l'interrupt viene dal VIC-II
            JMP KERNAL_IRQ              ; altrimenti è il timer della CIA 1: lo gestisce il KERNAL

raster:     STA VIC_IRQ                 ; conferma: scrivere 1 nei bit li azzera e rilascia la linea /IRQ
            LDA VIC_RASTER
            CMP #MIDDLE                 ; siamo in alto o in basso?
            BCS bottom

            LDA #RED                    ; inizio della barra
            STA BORDER
            LDA #BOTTOM
            STA VIC_RASTER              ; il prossimo interrupt arriva a fine barra
            JMP KERNAL_RTI

bottom:     LDA #LIGHT_BLUE             ; fine della barra
            STA BORDER
            LDA #TOP
            STA VIC_RASTER              ; il prossimo interrupt arriva al prossimo frame
            JMP KERNAL_RTI
