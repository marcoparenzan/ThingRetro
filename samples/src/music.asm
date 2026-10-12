; ----------------------------------------------------------------------------------------------------------
; MUSIC - tre voci del SID suonate dall'interrupt
;
; Il lettore è agganciato all'IRQ del KERNAL (vettore $0314, 60 volte al secondo, come in irq.asm): il
; programma torna al BASIC e la musica continua da sola. Ogni SPEED interrupt suona il passo successivo
; di una sequenza di 32 note che si ripete (la minore, fa, do, sol):
; - voce 1, melodia: onda quadra, con la larghezza dell'impulso che cambia lentamente (PWM);
; - voce 2, basso: dente di sega che passa dal filtro passa basso con risonanza, la cui frequenza
;   di taglio si apre e si chiude;
; - voce 3, charleston: un colpo di rumore a ogni passo.
;
; Per far ripartire l'inviluppo di una nota il GATE va portato a 0 e poi di nuovo a 1. Per fermare
; la musica serve un reset (clic destro sul monitor).
; ----------------------------------------------------------------------------------------------------------

SID         = $D400         ; 25 registri di sola scrittura, da $D400 a $D418
V1_FREQ     = $D400         ; voce 1: frequenza (byte basso, alto)
V1_PW       = $D402         ; larghezza dell'impulso a 12 bit (byte basso, 4 bit alti)
V1_CONTROL  = $D404         ; forma d'onda e GATE
V1_AD       = $D405         ; attacco (bit 4-7) e decadimento (bit 0-3)
V1_SR       = $D406         ; sostegno (bit 4-7) e rilascio (bit 0-3)
V2_FREQ     = $D407         ; voce 2: stessi registri, 7 byte più avanti
V2_CONTROL  = $D40B
V2_AD       = $D40C
V2_SR       = $D40D
V3_FREQ     = $D40E         ; voce 3
V3_CONTROL  = $D412
V3_AD       = $D413
V3_SR       = $D414
FC_HI       = $D416         ; bit 3-10 della frequenza di taglio del filtro
RES_FILT    = $D417         ; risonanza (bit 4-7), voci che passano dal filtro (bit 0-2)
MODE_VOL    = $D418         ; tipo di filtro (bit 4-6) e volume generale (bit 0-3)

SQUARE      = $40           ; forme d'onda nel registro di controllo; il bit 0 è il GATE
SAWTOOTH    = $20
NOISE       = $80

IRQ_VECTOR  = $0314         ; vettore IRQ del KERNAL
KERNAL_IRQ  = $EA31         ; gestore del KERNAL: tastiera, cursore, orologio, poi RTI
CHROUT      = $FFD2         ; stampa il carattere PETSCII in A

TICK        = $FB           ; interrupt che mancano al prossimo passo (pagina zero libera)
STEP        = $FC           ; passo della sequenza, 0-31
FRAME       = $FD           ; interrupt contati, per l'andamento lento di PWM e filtro
SPEED       = 7             ; interrupt per passo: 60 / 7 = circa 8,6 note al secondo

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
clear:      STA SID,X                   ; tutti i registri a zero: voci ferme, filtro spento
            DEX
            BPL clear
            STA STEP
            STA FRAME
            LDA #1
            STA TICK                    ; il primo passo al primo interrupt

            LDA #$09                    ; melodia: attacco immediato, decadimento 9
            STA V1_AD
            LDA #$40                    ; sostegno 4 (su 15), rilascio 0
            STA V1_SR
            LDA #$0A                    ; basso: decadimento 10
            STA V2_AD
            LDA #$80                    ; sostegno 8
            STA V2_SR
            LDA #$02                    ; charleston: decadimento 2, sostegno 0: un colpo breve
            STA V3_AD
            LDA #$00
            STA V3_SR
            LDA #$C0                    ; rumore molto acuto
            STA V3_FREQ+1

            LDA #$F2                    ; risonanza massima, solo la voce 2 nel filtro
            STA RES_FILT
            LDA #$1F                    ; passa basso, volume 15
            STA MODE_VOL

            SEI                         ; un IRQ a metà del cambio di vettore salterebbe a un indirizzo sbagliato
            LDA #<irq
            STA IRQ_VECTOR
            LDA #>irq
            STA IRQ_VECTOR+1
            CLI
            RTS

; ----------------------------------------------------------------------------------------------------------
; Il gestore dell'interrupt: a ogni chiamata aggiorna timbro e filtro, ogni SPEED chiamate suona un passo
; ----------------------------------------------------------------------------------------------------------

irq:        INC FRAME
            LDA FRAME                   ; un triangolo da FRAME: 0, 1, ... 127, 127, ... 1, 0 ogni 256 interrupt
            ASL A                       ; bit 7 nel carry: in quale metà del giro siamo
            BCC rising
            EOR #$FF                    ; seconda metà: si torna indietro
rising:     LSR A                       ; A = 0-127
            PHA
            LSR A
            LSR A
            LSR A
            LSR A                       ; 0-7
            CLC
            ADC #4
            STA V1_PW+1                 ; impulso da $400 a $B00 (25%-69%): il timbro cambia lentamente
            PLA
            LSR A                       ; 0-63
            CLC
            ADC #8
            STA FC_HI                   ; taglio da $40 a $238 (400-3300 Hz circa): il basso si apre e si chiude

            DEC TICK
            BNE done
            LDA #SPEED
            STA TICK

            LDX STEP
            LDY melody,X                ; indice della nota nella tabella delle frequenze
            LDA notes_lo,Y
            STA V1_FREQ
            LDA notes_hi,Y
            STA V1_FREQ+1
            LDA #SQUARE                 ; GATE a 0...
            STA V1_CONTROL
            LDA #SQUARE+1               ; ...e di nuovo a 1: l'inviluppo riparte con l'attacco
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
            AND #31                     ; dopo 32 passi si ricomincia
            STA STEP
done:       JMP KERNAL_IRQ

; ----------------------------------------------------------------------------------------------------------
; Dati
; ----------------------------------------------------------------------------------------------------------

message:    .byte 147                   ; cancella lo schermo
            .text "MUSICA DAL SID, SUONATA DALL'IRQ"
            .byte 13, 13
            .text "VOCE 1: MELODIA, ONDA QUADRA CON PWM"
            .byte 13
            .text "VOCE 2: BASSO NEL FILTRO PASSA BASSO"
            .byte 13
            .text "VOCE 3: RUMORE"
            .byte 13, 13, 0

; Indici nella tabella delle note: 0 = do2, 12 = do3, 24 = do4, 36 = do5. Una battuta = 8 passi
melody:     .byte 33, 36, 40, 45, 40, 36, 33, 36        ; la minore: la4 do5 mi5 la5
            .byte 29, 33, 36, 41, 36, 33, 29, 33        ; fa maggiore: fa4 la4 do5 fa5
            .byte 31, 36, 40, 43, 40, 36, 31, 36        ; do maggiore: sol4 do5 mi5 sol5
            .byte 31, 35, 38, 43, 38, 35, 31, 35        ; sol maggiore: sol4 si4 re5 sol5
bassline:   .byte  9, 21,  9, 21,  9, 21,  9, 21        ; la2 e la3
            .byte  5, 17,  5, 17,  5, 17,  5, 17        ; fa2 e fa3
            .byte 12, 24, 12, 24, 12, 24, 12, 24        ; do3 e do4
            .byte  7, 19,  7, 19,  7, 19,  7, 19        ; sol2 e sol3

; Frequenze delle note da do2 (65,4 Hz) a si5, temperamento equabile: registro = Hz x 2^24 / 985248
notes_lo:   .byte  90, 156, 226,  44, 123, 207,  39, 133, 232,  81, 192,  55
            .byte 180,  56, 196,  89, 247, 157,  78,  10, 208, 162, 129, 109
            .byte 103, 112, 137, 178, 237,  59, 156,  19, 160,  68,   2, 218
            .byte 206, 224,  17, 100, 218, 118,  57,  38,  64, 137,   4, 180
notes_hi:   .byte   4,   4,   4,   5,   5,   5,   6,   6,   6,   7,   7,   8
            .byte   8,   9,   9,  10,  10,  11,  12,  13,  13,  14,  15,  16
            .byte  17,  18,  19,  20,  21,  23,  24,  26,  27,  29,  31,  32
            .byte  34,  36,  39,  41,  43,  46,  49,  52,  55,  58,  62,  65
