; ----------------------------------------------------------------------------------------------------------
; IRQ - once per frame, from the VIC-II raster: mouse, joystick, buttons, keyboard, the pointer sprites
;
; The order matters. The SID has been measuring the POT lines of port 1 since the end of the previous
; keyboard scan (CIA 1 PA6 high), so the mouse is read first; reading the buttons and scanning the keyboard
; change port A, and the scan puts $7F back at the end.
; ----------------------------------------------------------------------------------------------------------

irq:        PHA
            TXA
            PHA
            TYA
            PHA
            JMP (irq_vector)

irq_default:
            LDA VIC_IRQ
            STA VIC_IRQ                 ; acknowledge the raster interrupt
            JSR read_mouse
            JSR read_joystick
            JSR read_buttons
            JSR scan_keyboard
            JSR place_pointer
            INC ticks
            BNE irq_return
            INC ticks+1
irq_return: PLA
            TAY
            PLA
            TAX
            PLA
            RTI

; 1351 mouse in port 1, proportional mode: POT X and POT Y are its position mod 64, times 2. Only the
; difference from the previous reading counts
read_mouse: LDA SID_POTX
            LDY pot_x_old
            JSR mouse_delta
            STY pot_x_old
            CLC
            ADC pointer_x
            STA pointer_x
            TXA
            ADC pointer_x+1
            STA pointer_x+1

            LDA SID_POTY
            LDY pot_y_old
            JSR mouse_delta
            STY pot_y_old
            STA irq_t0                  ; the mouse counts upwards, the screen downwards
            STX irq_t1
            SEC
            LDA pointer_y
            SBC irq_t0
            STA pointer_y
            LDA pointer_y+1
            SBC irq_t1
            STA pointer_y+1
            RTS

; In: A = new POT value, Y = previous one. Out: A/X = the movement (signed 16 bits), Y = the value to keep.
; The movement is 7 bits wide; bit 0 is noise, so a difference of one step or less is no movement and the
; previous value is kept (as in the driver Commodore shipped with the 1351)
mouse_delta:
            STA irq_t2                  ; new
            STY irq_t3                  ; old
            SEC
            SBC irq_t3
            AND #%01111111
            CMP #%01000000
            BCS @negative
            LSR
            BEQ @none
            LDX #0
            LDY irq_t2
            RTS
@negative:  ORA #%11000000              ; extend the sign of the 7 bits
            CMP #$FF
            BEQ @none
            SEC
            ROR                         ; halve, keeping the sign
            LDX #$FF
            LDY irq_t2
            RTS
@none:      LDA #0
            TAX                         ; Y is still the old value
            RTS

; Joystick in port 2: one pixel per frame, three after a third of a second
read_joystick:
            LDA CIA1_PRA                ; bits 0-4 follow the joystick: the latch holds them high ($7F)
            EOR #$FF
            AND #%00011111
            STA joy2
            AND #%00001111
            BNE +
            STA joy_held
            RTS
+           STA irq_t1                  ; directions: bit 0 up, 1 down, 2 left, 3 right
            LDX joy_held
            CPX #$FF
            BEQ +
            INX
            STX joy_held
+           LDY #1
            CPX #16
            BCC +
            LDY #3
+           STY irq_t0                  ; the step

            LSR irq_t1
            BCC +
            SEC
            LDA pointer_y
            SBC irq_t0
            STA pointer_y
            LDA pointer_y+1
            SBC #0
            STA pointer_y+1
+           LSR irq_t1
            BCC +
            CLC
            LDA pointer_y
            ADC irq_t0
            STA pointer_y
            LDA pointer_y+1
            ADC #0
            STA pointer_y+1
+           LSR irq_t1
            BCC +
            SEC
            LDA pointer_x
            SBC irq_t0
            STA pointer_x
            LDA pointer_x+1
            SBC #0
            STA pointer_x+1
+           LSR irq_t1
            BCC +
            CLC
            LDA pointer_x
            ADC irq_t0
            STA pointer_x
            LDA pointer_x+1
            ADC #0
            STA pointer_x+1
+           RTS

; With no keyboard column selected only the device in port 1 can pull port B down: the 1351 closes "fire"
; with the left button and "up" with the right one. The joystick's fire is a left button too
read_buttons:
            LDA #$FF
            STA CIA1_PRA
            LDA CIA1_PRB
            EOR #$FF
            STA port1
            LDY #0
            AND #%00010000
            BEQ +
            LDY #BUTTON_LEFT
+           LDA port1
            AND #%00000001
            BEQ +
            TYA
            ORA #BUTTON_RIGHT
            TAY
+           LDA joy2
            AND #%00010000
            BEQ +
            TYA
            ORA #BUTTON_LEFT
            TAY
+           STY buttons
            RTS
