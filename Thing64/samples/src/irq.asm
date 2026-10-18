; ----------------------------------------------------------------------------------------------------------
; IRQ - hooking into the CIA 1 timer interrupt
;
; The KERNAL programs timer A of CIA 1 to generate an IRQ 60 times per second: at every interrupt it reads
; the keyboard, blinks the cursor and updates the TI clock. Before running its own handler the KERNAL saves
; A, X and Y and jumps to the address written in vector $0314.
;
; This program puts its own handler in the vector: every 30 interrupts (half a second) it toggles the
; background color between blue and black, then carries on into the KERNAL handler. So keyboard and BASIC
; keep working: the program returns to BASIC and the background blinks "by itself".
; ----------------------------------------------------------------------------------------------------------

IRQ_VECTOR  = $0314         ; KERNAL IRQ vector (normally points to $EA31)
KERNAL_IRQ  = $EA31         ; KERNAL handler: keyboard, cursor, clock, then RTI
BACKGROUND  = $D021         ; background color
COUNT       = $FB           ; interrupt counter (free zero page)
PERIOD      = 30            ; interrupts between two changes: 30 / 60 Hz = half a second

            * = $0801
            .basic

            SEI                         ; an IRQ halfway through the vector change would jump to a wrong address
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
            EOR #%00000110              ; 6 (blue) <-> 0 (black): flips the bits that tell them apart
            STA BACKGROUND
kernal:     JMP KERNAL_IRQ              ; the KERNAL does the rest of the work, ending with RTI
