; ----------------------------------------------------------------------------------------------------------
; KEYBOARD - the 8x8 matrix read once per frame; the keys that went down become ASCII codes in a buffer
;
; Writing a column (bit n of CIA 1 port A low) and reading the rows (port B, bit m low = key pressed) gives
; the key at index n * 8 + m of the tables below, the same order as the matrix printed in the C64 manuals.
; ----------------------------------------------------------------------------------------------------------

scan_keyboard:
            LDA joy2                    ; a joystick held in port 2 selects columns by itself: phantom keys.
            BNE @done                   ; skip this frame, the keys are not lost (they are compared, not counted)
            LDA port1                   ; the lines a device holds in port 1 read as pressed in every column
            STA irq_t0
            LDA #%11111110
            STA irq_t1
            LDX #0
-           LDA irq_t1
            STA CIA1_PRA
            LDA CIA1_PRB
            ORA irq_t0
            STA key_matrix,X
            SEC
            ROL irq_t1
            INX
            CPX #8
            BNE -
            JSR decode_keys
@done:      LDA #$7F                    ; PA6 high again: the SID measures port 1 until the next frame
            STA CIA1_PRA
            RTS

; The modifiers from the matrix, then every key that is down now and was up in the previous frame
decode_keys:
            LDY #0
            LDA key_matrix+1
            AND #%10000000              ; left SHIFT (and SHIFT LOCK)
            BNE +
            LDY #MOD_SHIFT
+           LDA key_matrix+6
            AND #%00010000              ; right SHIFT
            BNE +
            TYA
            ORA #MOD_SHIFT
            TAY
+           LDA key_matrix+7
            AND #%00000100              ; CTRL
            BNE +
            TYA
            ORA #MOD_CTRL
            TAY
+           LDA key_matrix+7
            AND #%00100000              ; Commodore
            BNE +
            TYA
            ORA #MOD_CBM
            TAY
+           STY modifiers

            LDX #7
@column:    LDA key_matrix,X
            EOR #$FF                    ; 1 = down now
            AND key_prev,X              ; and up before
            AND not_modifier,X
            BEQ @next
            STA irq_t2                  ; the keys that went down in this column
            STX irq_t3
            TXA
            ASL
            ASL
            ASL
            TAX                         ; index of row 0 of this column
-           LSR irq_t2
            BCC +
            JSR push_key
+           INX
            LDA irq_t2
            BNE -
            LDX irq_t3
@next:      LDA key_matrix,X
            STA key_prev,X
            DEX
            BPL @column
            RTS

; In: X = index of the key in the matrix. Its ASCII code (with SHIFT and CTRL) goes into the buffer;
; when the buffer is full the key is lost. Keeps X
push_key:   LDA modifiers
            AND #MOD_SHIFT
            BEQ +
            LDA keys_shifted,X
            JMP ++
+           LDA keys_normal,X
++          BEQ @done                   ; a key with no code
            STA irq_t0
            LDA modifiers
            AND #MOD_CTRL
            BEQ +
            LDA irq_t0                  ; CTRL + letter: control code 1-26
            AND #%11011111              ; upper case
            CMP #'A'
            BCC +
            CMP #'Z'+1
            BCS +
            AND #%00011111
            STA irq_t0
+           TXA
            PHA
            LDX key_tail
            LDA irq_t0
            STA key_buffer,X
            INX
            TXA
            AND #KEY_BUFFER_SIZE - 1
            CMP key_head
            BEQ +                       ; full
            STA key_tail
+           PLA
            TAX
@done:      RTS

; GETKEY: the next key typed in A (0 = none, with Z set). Changes X
getkey:     LDX key_head
            CPX key_tail
            BEQ +
            LDA key_buffer,X
            PHA
            INX
            TXA
            AND #KEY_BUFFER_SIZE - 1
            STA key_head
            PLA
            RTS
+           LDA #0
            RTS

; Bits of each column that are not modifiers: the modifiers never go into the buffer
not_modifier:
            !byte $FF, %01111111, $FF, $FF, $FF, $FF, %11101111, %11011011

;           index: column * 8 + row
keys_normal:
            !byte KEY_DEL, KEY_RETURN, KEY_RIGHT, KEY_F1+6, KEY_F1, KEY_F1+2, KEY_F1+4, KEY_DOWN
            !byte '3', 'w', 'a', '4', 'z', 's', 'e', 0
            !byte '5', 'r', 'd', '6', 'c', 'f', 't', 'x'
            !byte '7', 'y', 'g', '8', 'b', 'h', 'u', 'v'
            !byte '9', 'i', 'j', '0', 'm', 'k', 'o', 'n'
            !byte '+', 'p', 'l', '-', '.', ':', '@', ','
            !byte $5C, '*', ';', KEY_HOME, 0, '=', '^', '/'
            !byte '1', '_', 0, '2', ' ', 0, 'q', KEY_ESCAPE
keys_shifted:
            !byte KEY_INSERT, KEY_RETURN, KEY_LEFT, KEY_F1+7, KEY_F1+1, KEY_F1+3, KEY_F1+5, KEY_UP
            !byte '#', 'W', 'A', '$', 'Z', 'S', 'E', 0
            !byte '%', 'R', 'D', '&', 'C', 'F', 'T', 'X'
            !byte $27, 'Y', 'G', '(', 'B', 'H', 'U', 'V'
            !byte ')', 'I', 'J', '0', 'M', 'K', 'O', 'N'
            !byte '{', 'P', 'L', '}', '>', '[', '`', '<'
            !byte '|', '~', ']', KEY_CLEAR, 0, '=', '^', '?'
            !byte '!', '_', 0, $22, ' ', 0, 'Q', KEY_ESCAPE
