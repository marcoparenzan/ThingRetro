; ----------------------------------------------------------------------------------------------------------
; VICBANK - spostare lo schermo e i caratteri in un altro banco del VIC-II
;
; Il VIC-II vede solo 16 KB alla volta. Il banco lo scelgono i bit 0-1 della porta A della CIA 2, invertiti:
;   %11 = banco 0 ($0000-$3FFF)   %10 = banco 1 ($4000-$7FFF)   %01 = banco 2   %00 = banco 3
; Nei banchi 0 e 2 il VIC-II vede la ROM dei caratteri a $1000-$1FFF; nei banchi 1 e 3 no: i caratteri
; vanno copiati in RAM. La CPU vede la ROM dei caratteri a $D000 solo con CHAREN = 0 (bit 2 di $01): in quel
; momento a $D000 non c'è l'I/O, quindi la copia si fa con gli interrupt disabilitati.
;
; Il programma copia la ROM dei caratteri a $6000, inverte la lettera A per dimostrare che il VIC-II ora
; legge la RAM, passa al banco 1 con lo schermo a $4400 e dice al KERNAL ($0288) dove scrivere. Poi torna al
; BASIC, che continua a funzionare nel banco 1: ogni A appare in negativo.
; ----------------------------------------------------------------------------------------------------------

CPU_PORT    = $01           ; bit 2 = CHAREN: 1 = I/O a $D000, 0 = ROM dei caratteri
CHAR_ROM    = $D000
CHARSET     = $6000         ; banco 1 + $2000
SCREEN      = $4400         ; banco 1 + $0400
CIA2_PRA    = $DD00         ; bit 0-1: banco del VIC-II (invertiti)
VIC_MEMORY  = $D018
HIBASE      = $0288         ; pagina dello schermo per l'editor del KERNAL
CLRSCR      = $E544
CHROUT      = $FFD2
SOURCE      = $FB
TARGET      = $FD

            * = $0801
            .basic

            SEI                         ; con CHAREN = 0 l'I/O sparisce: niente interrupt durante la copia
            LDA CPU_PORT
            AND #%11111011
            STA CPU_PORT
            LDA #<CHAR_ROM
            STA SOURCE
            LDA #>CHAR_ROM
            STA SOURCE+1
            LDA #<CHARSET
            STA TARGET
            LDA #>CHARSET
            STA TARGET+1
            LDX #8                      ; 8 pagine = 2 KB: il set maiuscolo/grafico
            LDY #0
copy:       LDA (SOURCE),Y
            STA (TARGET),Y
            INY
            BNE copy
            INC SOURCE+1
            INC TARGET+1
            DEX
            BNE copy
            LDA CPU_PORT
            ORA #%00000100              ; di nuovo l'I/O a $D000
            STA CPU_PORT
            CLI

            LDX #7                      ; la lettera A (codice schermo 1) in negativo
invert:     LDA CHARSET+8,X
            EOR #%11111111
            STA CHARSET+8,X
            DEX
            BPL invert

            LDA CIA2_PRA
            AND #%11111100
            ORA #%00000010              ; %10 = banco 1
            STA CIA2_PRA
            LDA #%00011000              ; schermo a banco + $0400 (1 KB x 1), caratteri a banco + $2000 (2 KB x 4)
            STA VIC_MEMORY
            LDA #>SCREEN
            STA HIBASE                  ; il KERNAL scriverà a $4400
            JSR CLRSCR

            LDX #0
print:      LDA message,X
            BEQ done
            JSR CHROUT
            INX
            BNE print
done:       RTS

message:    .text "SCHERMO A $4400, CARATTERI A $6000 (BANCO 1)"
            .byte 13
            .text "LA A E' COPIATA IN RAM E INVERTITA"
            .byte 13, 0
