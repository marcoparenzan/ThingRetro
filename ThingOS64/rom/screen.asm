; ----------------------------------------------------------------------------------------------------------
; SCREEN - writing on the 40x25 text screen: characters, strings, numbers, rectangles.
; The screen, the color RAM and the ownership map have the same layout, so one row table serves all three.
;
; Two ways to write. put_char writes anywhere (the menu bar, the drop down menus, which are always on top).
; wput_char writes through the drawing port: the cursor counts from the port's origin, and a character lands
; only inside the port's rectangle and on the cells the map gives to port_win. This is how a window draws
; itself without spilling over the windows in front of it.
; ----------------------------------------------------------------------------------------------------------

; In: Y = row. zp_ptr, zp_cptr and zp_mptr point to the start of that row in the screen, the color RAM and
; the map. Keeps X and Y
row_address:
            LDA row_low,Y
            STA zp_ptr
            STA zp_cptr
            STA zp_mptr
            LDA row_high,Y
            STA zp_ptr+1
            CLC
            ADC #>(COLOR_RAM - SCREEN)
            STA zp_cptr+1
            LDA row_high,Y
            CLC
            ADC #>(MAP - SCREEN)
            STA zp_mptr+1
            RTS

; In: X = column, Y = row
goto:       STX cur_col
            STY cur_row
            RTS

; Writes A at the cursor in text_color and moves the cursor right. Keeps X and Y
put_char:   STA zp_char
            TYA
            PHA
            LDY cur_row
            JSR row_address
            LDY cur_col
            LDA zp_char
            STA (zp_ptr),Y
            LDA text_color
            STA (zp_cptr),Y
            INC cur_col
            PLA
            TAY
            LDA zp_char
            RTS

; In: A/X = address of a string ending with 0. Writes it at the cursor
put_string: STA zp_str
            STX zp_str+1
            LDY #0
-           LDA (zp_str),Y
            BEQ +
            JSR put_char
            INY
            BNE -
+           RTS

; Fills rect_x, rect_y, rect_w, rect_h (at least 1 x 1) with fill_char in text_color
fill_rect:  LDA rect_y
            STA zp_t0
            LDA rect_h
            STA zp_t1
@row:       LDY zp_t0
            JSR row_address
            LDY rect_x
            LDX rect_w
-           LDA fill_char
            STA (zp_ptr),Y
            LDA text_color
            STA (zp_cptr),Y
            INY
            DEX
            BNE -
            INC zp_t0
            DEC zp_t1
            BNE @row
            RTS

; WPUTCHAR: writes A at the cursor through the port, in text_color, and moves the cursor right (also when
; the character is clipped away). Keeps X and Y
wput_char:  STA zp_char
            TYA
            PHA
            LDA cur_col
            CLC
            ADC port_ox
            CMP port_left
            BCC @skip
            CMP port_right
            BCS @skip
            STA zp_t6
            LDA cur_row
            CLC
            ADC port_oy
            CMP port_top
            BCC @skip
            CMP port_bottom
            BCS @skip
            TAY
            JSR row_address
            LDY zp_t6
            LDA (zp_mptr),Y
            CMP port_win
            BNE @skip
            LDA zp_char
            STA (zp_ptr),Y
            LDA text_color
            STA (zp_cptr),Y
@skip:      INC cur_col
            PLA
            TAY
            LDA zp_char
            RTS

; WPUTSTRING: A/X = address of a string ending with 0, written through the port
wput_string:
            STA zp_str
            STX zp_str+1
            LDY #0
-           LDA (zp_str),Y
            BEQ +
            JSR wput_char
            INY
            BNE -
+           RTS

; WPUTNUMBER: A/X = a number 0-999, written through the port with three digits
put_number: STA zp_num
            STX zp_num+1
            LDY #0
@digit:     LDX #0
-           LDA zp_num                  ; subtract the power of ten while it fits
            SEC
            SBC powers_low,Y
            STA zp_t0
            LDA zp_num+1
            SBC powers_high,Y
            BCC +
            STA zp_num+1
            LDA zp_t0
            STA zp_num
            INX
            BNE -
+           TXA
            ORA #'0'
            JSR wput_char
            INY
            CPY #3
            BNE @digit
            RTS

powers_low: !byte <100, <10, <1
powers_high: !byte >100, >10, >1

; Fills the whole port with fill_char in text_color, on the cells the map gives to port_win
wfill:      LDA port_top
            STA zp_t4
@row:       LDY zp_t4
            JSR row_address
            LDY port_left
-           LDA (zp_mptr),Y
            CMP port_win
            BNE +
            LDA fill_char
            STA (zp_ptr),Y
            LDA text_color
            STA (zp_cptr),Y
+           INY
            CPY port_right
            BCC -
            INC zp_t4
            LDA zp_t4
            CMP port_bottom
            BCC @row
            RTS

row_low:    !for r, 0, ROWS - 1 { !byte <(SCREEN + r * COLUMNS) }
row_high:   !for r, 0, ROWS - 1 { !byte >(SCREEN + r * COLUMNS) }
