; ----------------------------------------------------------------------------------------------------------
; JUMP TABLE - the fixed addresses applications call. An entry never moves: new ones go at the end
; ----------------------------------------------------------------------------------------------------------

JUMP_TABLE  = $FF80

            * = JUMP_TABLE
os_getkey:  JMP getkey                  ; $FF80 the next key typed in A (0 = none)
os_win_open: JMP win_open               ; $FF83 A/X = descriptor: opens a window on top; A = slot, carry set = no room
os_win_close: JMP win_close             ; $FF86 A = slot
os_win_front: JMP win_front             ; $FF89 A = slot: to the top
os_win_invalidate: JMP win_invalidate   ; $FF8C A = slot: draws its content again
os_wput_char: JMP wput_char             ; $FF8F A = character, at the cursor of the port
os_wput_string: JMP wput_string         ; $FF92 A/X = string ending with 0
os_wput_number: JMP put_number          ; $FF95 A/X = 0-999, three digits

; ---------------------------------------------------------------------------------------------- vectors
            * = $FFFA
            !word nmi
            !word reset
            !word irq
