; ----------------------------------------------------------------------------------------------------------
; RASTERBARS - a red bar in the border, drawn with the VIC-II raster interrupt
;
; The VIC-II draws the screen line by line (312 raster lines on PAL). Register $D012 holds the current line;
; writing a value to it asks the VIC-II to generate an interrupt (IRQ) when the electron beam reaches that
; line. Here:
;   - at line 100 the border turns red and the next interrupt is requested at line 150
;   - at line 150 the border turns light blue again and the next interrupt is requested at line 100
; The result is a still red bar in the border, while BASIC keeps working.
;
; The IRQ vector ($0314) is shared with the CIA 1 timer, which 60 times per second has the KERNAL read the
; keyboard: the handler checks who asked for the interrupt and passes the CIA ones on to the KERNAL.
; ----------------------------------------------------------------------------------------------------------

VIC_CR1     = $D011         ; control 1: bit 7 is the ninth bit of the raster line
VIC_RASTER  = $D012         ; raster line (read: the current one; written: the one for the interrupt)
VIC_IRQ     = $D019         ; interrupts that happened (bit 7 = the VIC-II holds the /IRQ line low)
VIC_IRQEN   = $D01A         ; interrupts enabled (bit 0 = raster)
BORDER      = $D020         ; border color
IRQ_VECTOR  = $0314         ; the KERNAL jumps here at every IRQ (after saving the registers)
KERNAL_IRQ  = $EA31         ; KERNAL IRQ handler: keyboard, cursor, clock
KERNAL_RTI  = $EA81         ; end of the handler: restores A, X, Y and does RTI

RED         = 2
LIGHT_BLUE  = 14
TOP         = 100           ; line where the bar starts
BOTTOM      = 150           ; line where it ends
MIDDLE      = 120           ; any line between the two, to tell which interrupt arrived

            * = $0801
            .basic                      ; 10 SYS2061: RUN starts the code that follows

            SEI                         ; no interrupts while the vector changes
            LDA #$01
            STA VIC_IRQEN               ; enable the raster interrupt
            LDA #TOP
            STA VIC_RASTER              ; first interrupt at line 100...
            LDA VIC_CR1
            AND #$7F
            STA VIC_CR1                 ; ...with the ninth bit at 0 (lines 0-255)
            LDA #<irq
            STA IRQ_VECTOR
            LDA #>irq
            STA IRQ_VECTOR+1            ; from now on the IRQs go through our handler
            CLI
            RTS                         ; back to BASIC: the bar goes on by itself

; Interrupt handler: called by the KERNAL with A, X and Y already saved on the stack
irq:        LDA VIC_IRQ
            BMI raster                  ; bit 7 set: the interrupt comes from the VIC-II
            JMP KERNAL_IRQ              ; otherwise it is the CIA 1 timer: the KERNAL handles it

raster:     STA VIC_IRQ                 ; acknowledge: writing 1 to the bits clears them and releases /IRQ
            LDA VIC_RASTER
            CMP #MIDDLE                 ; are we at the top or at the bottom?
            BCS bottom

            LDA #RED                    ; start of the bar
            STA BORDER
            LDA #BOTTOM
            STA VIC_RASTER              ; the next interrupt comes at the end of the bar
            JMP KERNAL_RTI

bottom:     LDA #LIGHT_BLUE             ; end of the bar
            STA BORDER
            LDA #TOP
            STA VIC_RASTER              ; the next interrupt comes in the next frame
            JMP KERNAL_RTI
