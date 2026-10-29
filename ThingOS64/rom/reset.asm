; ----------------------------------------------------------------------------------------------------------
; RESET - power on: the chips in a known state, the RAM of the OS cleared, then the desktop
; ----------------------------------------------------------------------------------------------------------

reset:      SEI
            CLD
            LDX #$FF
            TXS
            LDA #%00110111              ; LORAM, HIRAM, CHAREN high: both ROMs and the I/O. The latch first:
            STA $01                     ; until the pins become outputs the pull-ups keep the ROMs visible
            LDA #%00101111              ; 6510 port: P0-P3 and P5 outputs, as on every C64
            STA $00

            JSR init_cias
            JSR init_sid
            JSR clear_ram
            JSR init_vic
            JSR init_pointer

            LDA #<irq_default
            STA irq_vector
            LDA #>irq_default
            STA irq_vector+1
            LDA #<nmi_default
            STA nmi_vector
            LDA #>nmi_default
            STA nmi_vector+1

            CLI
            JMP desktop                 ; the upper ROM takes over

; CIA 1 to the keyboard and the control ports, CIA 2 to the VIC-II bank and the serial bus. No CIA
; interrupts: the IRQ comes from the VIC-II, once per frame
init_cias:  LDA #$7F
            STA CIA1_ICR
            STA CIA2_ICR
            LDA CIA1_ICR                ; reading clears what was pending
            LDA CIA2_ICR
            LDA #0
            STA CIA1_CRA
            STA CIA1_CRB
            STA CIA2_CRA
            STA CIA2_CRB
            LDA #$FF
            STA CIA1_DDRA               ; port A drives the keyboard columns
            LDA #0
            STA CIA1_DDRB               ; port B reads the rows
            LDA #$7F                    ; PA6 high, PA7 low: the SID measures the POT lines of port 1 (the mouse)
            STA CIA1_PRA
            LDA #%00000011              ; VIC-II bank 0 ($0000-$3FFF), ATN, CLK and DATA released
            STA CIA2_PRA
            LDA #%00111111              ; PA0-PA5 outputs, PA6-PA7 (CLK in, DATA in) inputs
            STA CIA2_DDRA
            RTS

init_sid:   LDA #0
            LDX #$18
-           STA SID,X                   ; all voices silent, volume 0
            DEX
            BPL -
            RTS

; Zero page from $02 and pages 2 and 3; the stack is left alone (we are in a subroutine)
clear_ram:  LDA #0
            LDX #2
-           STA $00,X
            INX
            BNE -
-           STA $0200,X
            STA $0300,X
            STA OS_RAM,X                ; the OS's own RAM: the ownership map and the window table
            STA OS_RAM+$100,X
            STA OS_RAM+$200,X
            STA OS_RAM+$300,X
            STA OS_RAM+$400,X
            STA OS_RAM+$500,X
            INX
            BNE -
            RTS

; The VIC-II registers from a table: text mode, 40x25, screen at $0400, characters from the ROM at $1000,
; the two pointer sprites on, the raster interrupt at line 251 (below the text, on PAL and NTSC)
init_vic:   LDX #vic_registers_end - vic_registers - 1
-           LDA vic_registers,X
            STA $D000,X
            DEX
            BPL -
            LDA #POINTER_SPRITES / 64
            STA SPRITE_PTRS
            LDA #POINTER_SPRITES / 64 + 1
            STA SPRITE_PTRS+1
            LDX #127
-           LDA pointer_shape,X
            STA POINTER_SPRITES,X
            DEX
            BPL -
            RTS

RASTER_LINE = 251

vic_registers:
            !fill 16, 0                 ; $D000-$D00F sprite positions
            !byte 0                     ; $D010 sprite X bit 8
            !byte $1B                   ; $D011 screen on, 25 rows, raster bit 8 = 0
            !byte RASTER_LINE           ; $D012 raster compare
            !byte 0, 0                  ; $D013-$D014 light pen
            !byte %00000011             ; $D015 sprites 0 and 1: the pointer
            !byte $08                   ; $D016 40 columns, no multicolor
            !byte 0                     ; $D017 no Y expansion
            !byte $14                   ; $D018 screen $0400, characters $1000
            !byte $0F                   ; $D019 clear the pending interrupts
            !byte $01                   ; $D01A raster interrupt on
            !byte 0                     ; $D01B sprites in front of the text
            !byte 0                     ; $D01C no multicolor sprites
            !byte 0                     ; $D01D no X expansion
            !byte 0, 0                  ; $D01E-$D01F collisions
            !byte LIGHT_BLUE            ; $D020 border
            !byte WHITE                 ; $D021 background
            !byte 0, 0, 0               ; $D022-$D024 extra backgrounds
            !byte 0, 0                  ; $D025-$D026 sprite multicolors
            !byte BLACK                 ; $D027 sprite 0: the outline of the pointer
            !byte WHITE                 ; $D028 sprite 1: its fill
            !fill 6, 0                  ; $D029-$D02E
vic_registers_end:

; The pointer starts in the middle of the screen
init_pointer:
            LDA #160
            STA pointer_x
            LDA #100
            STA pointer_y
            LDA SID_POTX                ; the first movement is measured from here
            STA pot_x_old
            LDA SID_POTY
            STA pot_y_old
            JMP place_pointer

nmi:        JMP (nmi_vector)

nmi_default:
            RTI                         ; RESTORE does nothing (yet)
