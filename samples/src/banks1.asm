; ----------------------------------------------------------------------------------------------------------
; BANKS (banco 1) - il secondo banco della cartuccia Magic Desk (vedi banks0.asm)
;
; Contiene solo il testo che la routine in RAM legge a $8100 quando questo banco è selezionato.
; ----------------------------------------------------------------------------------------------------------

TEXT        = $8100

            * = $8000                   ; anche questo banco compare a $8000-$9FFF
            * = TEXT                    ; lo spazio prima del testo resta a $FF, come in una EPROM vuota
            .text "E QUESTO NEL BANCO 1, SELEZIONATO SCRIVENDO 1 IN $DE00"
            .byte 13, 0
