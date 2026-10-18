; ----------------------------------------------------------------------------------------------------------
; BANKS (bank 0) - Magic Desk cartridge with several 8 KB banks (becomes banks.crt together with banks1.asm)
;
; A cartridge can hold more than 8 KB by showing 8 at a time at $8000-$9FFF. In Magic Desk cartridges the bank
; is selected by writing its number to $DE00 (I/O 1 area, wired to the expansion port); bit 7 set
; disconnects the cartridge.
;
; The problem: switching bank while the CPU runs code from $8000 changes the code under its feet.
; That is why the routine that switches banks is copied into RAM ($C000) and run from there: it reads the text
; at $8100 in bank 0 and in bank 1, prints it, and selects bank 0 again before returning.
; ----------------------------------------------------------------------------------------------------------

IOINIT      = $FF84
RAMTAS      = $FF87
RESTOR      = $FF8A
CINT        = $FF81
CHROUT      = $FFD2
BANK        = $DE00         ; Magic Desk bank register
TEXT        = $8100         ; each bank has its text here
RAM_ROUTINE = $C000         ; where the bank switching routine is copied

            * = $8000
            .word start, start
            .byte $C3, $C2, $CD, '8', '0'   ; "CBM80" signature: autostart

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
done:       JMP done                    ; back in bank 0: this code is still there

; This part runs at $C000, not where it was assembled: it uses only relative branches and fixed addresses
routine:    LDY #0                      ; bank to read
next_bank:  STY BANK                    ; from here $8000-$9FFF shows bank Y
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
            STA BANK                    ; bank 0 again, before returning to the code in ROM
            RTS
routine_end:

            * = TEXT
            .text "THIS TEXT IS IN BANK 0"
            .byte 13, 0
