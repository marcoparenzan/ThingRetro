; ----------------------------------------------------------------------------------------------------------
; CARTRIDGE - an 8 KB cartridge that starts by itself (becomes hello.crt)
;
; A "normal" 8 KB cartridge holds the EXROM line low: the PLA shows its ROM (ROML) at $8000-$9FFF.
; At power on the KERNAL checks whether the "CBM80" signature is at $8004 (in PETSCII, with bit 7 of the
; letters set): if it is, instead of starting BASIC it jumps to the address stored at $8000 (cold start).
; At that point the system is not initialized yet: it is up to the cartridge to call the KERNAL routines
; that set up the CIAs, memory, vectors and screen.
; ----------------------------------------------------------------------------------------------------------

IOINIT      = $FF84         ; initializes CIAs, SID, ports
RAMTAS      = $FF87         ; clears the zero page, tests the RAM, sets the pointers
RESTOR      = $FF8A         ; default KERNAL vectors
CINT        = $FF81         ; VIC-II and screen (clears the screen)
CHROUT      = $FFD2         ; writes the character in A at the cursor position

            * = $8000
            .word start                 ; cold start (at power on and at reset)
            .word start                 ; warm start (RUN/STOP + RESTORE)
            .byte $C3, $C2, $CD, '8', '0'   ; "CBM80" signature

start:      JSR IOINIT
            JSR RAMTAS
            JSR RESTOR
            JSR CINT
            LDX #0
print:      LDA message,X
            BEQ done                    ; the message ends with 0
            JSR CHROUT
            INX
            BNE print
done:       JMP done                    ; no BASIC to return to: stay here

message:    .text "HELLO FROM CARTRIDGE"
            .byte 0
