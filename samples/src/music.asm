; ----------------------------------------------------------------------------------------------------------
; MUSIC - three SID voices played by the interrupt
;
; The player is hooked into the KERNAL IRQ (vector $0314, 60 times per second, as in irq.asm): the program
; returns to BASIC and the music goes on by itself. Every SPEED interrupts it plays the next step of a
; sequence of 32 notes that repeats (A minor, F, C, G):
; - voice 1, melody: square wave, with a pulse width that changes slowly (PWM);
; - voice 2, bass: sawtooth going through the low-pass filter with resonance, whose cutoff frequency
;   opens and closes;
; - voice 3, hi-hat: a burst of noise at every step.
;
; To restart the envelope of a note the GATE must go to 0 and then back to 1. To stop the music a reset is
; needed (right click on the monitor).
; ----------------------------------------------------------------------------------------------------------

SID         = $D400         ; 25 write-only registers, from $D400 to $D418
V1_FREQ     = $D400         ; voice 1: frequency (low byte, high byte)
V1_PW       = $D402         ; 12-bit pulse width (low byte, 4 high bits)
V1_CONTROL  = $D404         ; waveform and GATE
V1_AD       = $D405         ; attack (bits 4-7) and decay (bits 0-3)
V1_SR       = $D406         ; sustain (bits 4-7) and release (bits 0-3)
V2_FREQ     = $D407         ; voice 2: same registers, 7 bytes further on
V2_CONTROL  = $D40B
V2_AD       = $D40C
V2_SR       = $D40D
V3_FREQ     = $D40E         ; voice 3
V3_CONTROL  = $D412
V3_AD       = $D413
V3_SR       = $D414
FC_HI       = $D416         ; bits 3-10 of the filter cutoff frequency
RES_FILT    = $D417         ; resonance (bits 4-7), voices going through the filter (bits 0-2)
MODE_VOL    = $D418         ; filter type (bits 4-6) and master volume (bits 0-3)

SQUARE      = $40           ; waveforms in the control register; bit 0 is GATE
SAWTOOTH    = $20
NOISE       = $80

IRQ_VECTOR  = $0314         ; KERNAL IRQ vector
KERNAL_IRQ  = $EA31         ; KERNAL handler: keyboard, cursor, clock, then RTI
CHROUT      = $FFD2         ; prints the PETSCII character in A

TICK        = $FB           ; interrupts left before the next step (free zero page)
STEP        = $FC           ; step of the sequence, 0-31
FRAME       = $FD           ; interrupts counted, for the slow sweep of PWM and filter
SPEED       = 7             ; interrupts per step: 60 / 7 = about 8.6 notes per second

            * = $0801
            .basic

            LDX #0
print:      LDA message,X
            BEQ init
            JSR CHROUT
            INX
            BNE print

init:       LDA #0
            LDX #$18
clear:      STA SID,X                   ; all the registers at zero: voices silent, filter off
            DEX
            BPL clear
            STA STEP
            STA FRAME
            LDA #1
            STA TICK                    ; the first step at the first interrupt

            LDA #$09                    ; melody: immediate attack, decay 9
            STA V1_AD
            LDA #$40                    ; sustain 4 (out of 15), release 0
            STA V1_SR
            LDA #$0A                    ; bass: decay 10
            STA V2_AD
            LDA #$80                    ; sustain 8
            STA V2_SR
            LDA #$02                    ; hi-hat: decay 2, sustain 0: a short burst
            STA V3_AD
            LDA #$00
            STA V3_SR
            LDA #$C0                    ; very high-pitched noise
            STA V3_FREQ+1

            LDA #$F2                    ; maximum resonance, only voice 2 into the filter
            STA RES_FILT
            LDA #$1F                    ; low-pass, volume 15
            STA MODE_VOL

            SEI                         ; an IRQ halfway through the vector change would jump to a wrong address
            LDA #<irq
            STA IRQ_VECTOR
            LDA #>irq
            STA IRQ_VECTOR+1
            CLI
            RTS

; ----------------------------------------------------------------------------------------------------------
; The interrupt handler: at every call it updates timbre and filter, every SPEED calls it plays a step
; ----------------------------------------------------------------------------------------------------------

irq:        INC FRAME
            LDA FRAME                   ; a triangle from FRAME: 0, 1, ... 127, 127, ... 1, 0 every 256 interrupts
            ASL A                       ; bit 7 into the carry: which half of the cycle we are in
            BCC rising
            EOR #$FF                    ; second half: going back down
rising:     LSR A                       ; A = 0-127
            PHA
            LSR A
            LSR A
            LSR A
            LSR A                       ; 0-7
            CLC
            ADC #4
            STA V1_PW+1                 ; pulse from $400 to $B00 (25%-69%): the timbre changes slowly
            PLA
            LSR A                       ; 0-63
            CLC
            ADC #8
            STA FC_HI                   ; cutoff from $40 to $238 (about 400-3300 Hz): the bass opens and closes

            DEC TICK
            BNE done
            LDA #SPEED
            STA TICK

            LDX STEP
            LDY melody,X                ; index of the note in the frequency table
            LDA notes_lo,Y
            STA V1_FREQ
            LDA notes_hi,Y
            STA V1_FREQ+1
            LDA #SQUARE                 ; GATE to 0...
            STA V1_CONTROL
            LDA #SQUARE+1               ; ...and back to 1: the envelope restarts with the attack
            STA V1_CONTROL

            LDY bassline,X
            LDA notes_lo,Y
            STA V2_FREQ
            LDA notes_hi,Y
            STA V2_FREQ+1
            LDA #SAWTOOTH
            STA V2_CONTROL
            LDA #SAWTOOTH+1
            STA V2_CONTROL

            LDA #NOISE
            STA V3_CONTROL
            LDA #NOISE+1
            STA V3_CONTROL

            INX
            TXA
            AND #31                     ; after 32 steps it starts again
            STA STEP
done:       JMP KERNAL_IRQ

; ----------------------------------------------------------------------------------------------------------
; Data
; ----------------------------------------------------------------------------------------------------------

message:    .byte 147                   ; clears the screen
            .text "MUSIC FROM THE SID, PLAYED BY THE IRQ"
            .byte 13, 13
            .text "VOICE 1: MELODY, SQUARE WAVE WITH PWM"
            .byte 13
            .text "VOICE 2: BASS THROUGH THE LOW-PASS FILTER"
            .byte 13
            .text "VOICE 3: NOISE"
            .byte 13, 13, 0

; Indexes into the note table: 0 = C2, 12 = C3, 24 = C4, 36 = C5. One bar = 8 steps
melody:     .byte 33, 36, 40, 45, 40, 36, 33, 36        ; A minor: A4 C5 E5 A5
            .byte 29, 33, 36, 41, 36, 33, 29, 33        ; F major: F4 A4 C5 F5
            .byte 31, 36, 40, 43, 40, 36, 31, 36        ; C major: G4 C5 E5 G5
            .byte 31, 35, 38, 43, 38, 35, 31, 35        ; G major: G4 B4 D5 G5
bassline:   .byte  9, 21,  9, 21,  9, 21,  9, 21        ; A2 and A3
            .byte  5, 17,  5, 17,  5, 17,  5, 17        ; F2 and F3
            .byte 12, 24, 12, 24, 12, 24, 12, 24        ; C3 and C4
            .byte  7, 19,  7, 19,  7, 19,  7, 19        ; G2 and G3

; Note frequencies from C2 (65.4 Hz) to B5, equal temperament: register = Hz x 2^24 / 985248
notes_lo:   .byte  90, 156, 226,  44, 123, 207,  39, 133, 232,  81, 192,  55
            .byte 180,  56, 196,  89, 247, 157,  78,  10, 208, 162, 129, 109
            .byte 103, 112, 137, 178, 237,  59, 156,  19, 160,  68,   2, 218
            .byte 206, 224,  17, 100, 218, 118,  57,  38,  64, 137,   4, 180
notes_hi:   .byte   4,   4,   4,   5,   5,   5,   6,   6,   6,   7,   7,   8
            .byte   8,   9,   9,  10,  10,  11,  12,  13,  13,  14,  15,  16
            .byte  17,  18,  19,  20,  21,  23,  24,  26,  27,  29,  31,  32
            .byte  34,  36,  39,  41,  43,  46,  49,  52,  55,  58,  62,  65
