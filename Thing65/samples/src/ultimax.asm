; ----------------------------------------------------------------------------------------------------------
; ULTIMAX - a cartridge in Ultimax mode: the C64 without KERNAL (becomes ultimax.crt)
;
; A cartridge that holds the GAME line low (and EXROM high) puts the C64 in Ultimax mode, meant for the
; MAX Machine console: the cartridge ROM (ROMH) takes the place of the KERNAL at $E000-$FFFF, and so also of
; the reset, NMI and IRQ vectors at $FFFA-$FFFF. Of the memory only $0000-$0FFF, the I/O and the cartridge
; ROMs remain visible: the rest is not connected. There is no BASIC and no KERNAL: everything is up to you.
;
; The code starts from the reset vector, turns the display off (the border covers the whole screen) and at
; every new raster line changes the border color: a rainbow of horizontal lines.
; ----------------------------------------------------------------------------------------------------------

VIC_CR1     = $D011
RASTER      = $D012
BORDER      = $D020

            .initmem $FF                ; unused ROM bytes read $FF, as in a blank EPROM
            * = $E000

reset:      SEI                         ; nobody has set up the machine: we start from scratch
            CLD
            LDX #$FF
            TXS                         ; stack at the top of page 1
            LDA #%00001011              ; display off (bit 4 = 0), 25 rows, YSCROLL 3
            STA VIC_CR1

loop:       LDA RASTER
same_line:  CMP RASTER                  ; wait for the raster line to change
            BEQ same_line
            LDA RASTER
            LSR                         ; one color every two lines
            STA BORDER                  ; the VIC-II uses only the 4 low bits: 16 colors that repeat
            JMP loop

nmi:        RTI                         ; no NMI expected
irq:        RTI                         ; no IRQ enabled

            * = $FFFA                   ; CPU vectors, at the end of ROMH
            .word nmi                   ; $FFFA: NMI
            .word reset                 ; $FFFC: reset
            .word irq                   ; $FFFE: IRQ and BRK
