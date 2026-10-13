; ----------------------------------------------------------------------------------------------------------
; BANKS (bank 1) - the second bank of the Magic Desk cartridge (see banks0.asm)
;
; It only holds the text that the routine in RAM reads at $8100 when this bank is selected.
; ----------------------------------------------------------------------------------------------------------

TEXT        = $8100

            * = $8000                   ; this bank appears at $8000-$9FFF too
            .fill TEXT-*, $FF           ; the space before the text stays at $FF, as in a blank EPROM
            .text "AND THIS IS IN BANK 1, SELECTED BY WRITING 1 TO $DE00"
            .byte 13, 0
