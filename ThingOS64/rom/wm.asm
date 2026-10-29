; ----------------------------------------------------------------------------------------------------------
; WM - the window manager
;
; Up to 8 windows, each in a slot (the win_* arrays). z_order lists the slots from the bottom to the top. The
; ownership map has a byte per cell: 0 for the desktop, slot + 1 for the window on top there; it is rebuilt
; from z_order whenever a window opens, closes, moves or comes to the front, and then the screen is drawn
; again: the desktop on its cells, each window on its own (see wput_char), so nothing is drawn twice.
;
; The OS keeps no copy of what is in a window: when its content must be drawn it calls the window's draw
; routine, with the port on the content and the cursor at 0,0. The window's event routine receives the keys
; (when it is on top), the clicks on its content and the closing.
;
;   +-------- title bar: close box, title (reversed, blue on top, grey below)
;   |X Title         |
;   ||content        |  frame: black lines
;   |+---------------+
; ----------------------------------------------------------------------------------------------------------

; The slot on top in A, $FF if there are no windows. Changes X
wm_top:     LDX z_count
            BEQ +
            LDA z_order-1,X
            RTS
+           LDA #$FF
            RTS

; WIN_OPEN: A/X = address of a descriptor (x, y, w, h, title, draw, event). Opens the window on top and draws
; it. Out: A = slot and carry clear, or carry set when all the slots are taken
win_open:   STA zp_t4
            STX zp_t5
            LDX #0
-           LDA win_flags,X
            BEQ +
            INX
            CPX #WIN_MAX
            BNE -
            SEC
            RTS
+           LDY #0
            LDA (zp_t4),Y
            STA win_x,X
            INY
            LDA (zp_t4),Y
            STA win_y,X
            INY
            LDA (zp_t4),Y
            STA win_w,X
            INY
            LDA (zp_t4),Y
            STA win_h,X
            INY
            LDA (zp_t4),Y
            STA win_title_lo,X
            INY
            LDA (zp_t4),Y
            STA win_title_hi,X
            INY
            LDA (zp_t4),Y
            STA win_draw_lo,X
            INY
            LDA (zp_t4),Y
            STA win_draw_hi,X
            INY
            LDA (zp_t4),Y
            STA win_event_lo,X
            INY
            LDA (zp_t4),Y
            STA win_event_hi,X
            LDA #0
            STA win_data0,X
            STA win_data1,X
            STA win_data2,X
            STA win_data3,X
            LDA #$80
            STA win_flags,X
            JSR fit_window
            TXA
            LDY z_count
            STA z_order,Y
            INC z_count
            TXA
            PHA                         ; redraw_all changes wm_slot
            JSR redraw_all
            PLA
            CLC
            RTS

; X = slot: at least 6 x 3, inside the screen and below the menu bar
fit_window: LDA win_w,X
            CMP #6
            BCS +
            LDA #6
+           CMP #COLUMNS + 1
            BCC +
            LDA #COLUMNS
+           STA win_w,X
            LDA win_h,X
            CMP #3
            BCS +
            LDA #3
+           CMP #ROWS
            BCC +
            LDA #ROWS - 1
+           STA win_h,X
            LDA #COLUMNS
            SEC
            SBC win_w,X
            CMP win_x,X
            BCS +
            STA win_x,X
+           LDA win_y,X
            BNE +
            INC win_y,X
+           LDA #ROWS
            SEC
            SBC win_h,X
            CMP win_y,X
            BCS +
            STA win_y,X
+           RTS

; WIN_CLOSE: A = slot. The window's event routine receives EV_CLOSE, then the window goes away
win_close:  TAX
            LDA win_flags,X
            BEQ +
            STX wm_slot
            LDA #EV_CLOSE
            JSR send_event
            LDX wm_slot
            LDA #0
            STA win_flags,X
            TXA
            JSR z_remove
            JSR redraw_all
+           RTS

; WIN_FRONT: A = slot. Brings the window to the top (and draws again) if it is not there already
win_front:  STA wm_slot
            JSR wm_top
            CMP wm_slot
            BEQ +
            LDA wm_slot
            JSR z_remove
            LDA wm_slot
            LDX z_count
            STA z_order,X
            INC z_count
            JSR redraw_all
+           RTS

; A = slot: takes it out of z_order
z_remove:   STA zp_t0
            LDX #0
@find:      LDA z_order,X
            CMP zp_t0
            BEQ @shift
            INX
            CPX z_count
            BCC @find
            RTS                         ; not there
@shift:     INX                         ; the ones above move down
            CPX z_count
            BCS @end
            LDA z_order,X
            STA z_order-1,X
            JMP @shift
@end:       DEC z_count
            RTS

; Calls the event routine of the window in X with the event in A, ev_x in X and ev_y in Y, cur_win = slot
; and the port on the content
send_event: STA zp_t3
            LDA win_event_hi,X
            BEQ +                       ; no event routine
            STA zp_call+1
            LDA win_event_lo,X
            STA zp_call
            STX cur_win
            JSR content_port
            LDA zp_t3
            LDX ev_x
            LDY ev_y
            JMP (zp_call)               ; its RTS returns to our caller
+           RTS

; The port on the whole window in X (frame and title bar)
frame_port: TXA
            CLC
            ADC #1
            STA port_win
            LDA win_x,X
            STA port_ox
            STA port_left
            CLC
            ADC win_w,X
            STA port_right
            LDA win_y,X
            STA port_oy
            STA port_top
            CLC
            ADC win_h,X
            STA port_bottom
            RTS

; The port on the content of the window in X
content_port:
            JSR frame_port
            INC port_ox
            INC port_left
            INC port_oy
            INC port_top
            DEC port_right
            DEC port_bottom
            RTS

; The map from z_order: each window covers the ones below it
build_map:  LDA #0
            TAX
-           STA MAP,X
            STA MAP+$100,X
            STA MAP+$200,X
            STA MAP+$300,X
            INX
            BNE -
            STA wm_i
@window:    LDX wm_i
            CPX z_count
            BEQ @done
            LDA z_order,X
            TAX
            JSR frame_port
            LDA port_top
            STA zp_t4
@row:       LDY zp_t4
            JSR row_address
            LDY port_left
            LDA port_win
-           STA (zp_mptr),Y
            INY
            CPY port_right
            BCC -
            INC zp_t4
            LDA zp_t4
            CMP port_bottom
            BCC @row
            INC wm_i
            JMP @window
@done:      RTS

; The desktop on the cells no window covers (the menu bar row is not touched)
draw_desktop:
            LDA #1
            STA zp_t4
@row:       LDY zp_t4
            JSR row_address
            LDY #0
-           LDA (zp_mptr),Y
            BNE +
            LDA #G_SOLID
            STA (zp_ptr),Y
            LDA #COLOR_DESKTOP
            STA (zp_cptr),Y
+           INY
            CPY #COLUMNS
            BNE -
            INC zp_t4
            LDA zp_t4
            CMP #ROWS
            BNE @row
            RTS

; The whole screen below the menu bar, from the windows' state
redraw_all: JSR build_map
            JSR draw_desktop
            LDA #0
            STA wm_j
-           LDX wm_j
            CPX z_count
            BEQ +
            LDA z_order,X
            TAX
            JSR draw_window
            INC wm_j
            JMP -
+           RTS

; X = slot: title bar, frame, content
draw_window:
            STX wm_slot
            JSR frame_port
            JSR wm_top
            LDY #COLOR_INACTIVE
            CMP wm_slot
            BNE +
            LDY #COLOR_ACTIVE
+           STY text_color
            LDX wm_slot
            LDA #0
            STA cur_col
            STA cur_row
            LDA #G_CLOSE | $80
            JSR wput_char
            LDA #G_SOLID
            JSR wput_char
            LDA win_title_lo,X
            STA zp_str
            LDA win_title_hi,X
            STA zp_str+1
            LDY #0
-           LDA (zp_str),Y
            BEQ +
            ORA #$80
            JSR wput_char
            INY
            BNE -
+           LDA cur_col                 ; the rest of the bar
            CMP win_w,X
            BCS +
            LDA #G_SOLID
            JSR wput_char
            JMP -
+           LDA #COLOR_FRAME
            STA text_color
            LDA #1
            STA cur_row
@side:      LDA win_h,X
            SEC
            SBC #1
            CMP cur_row
            BEQ @bottom
            LDA #0
            STA cur_col
            LDA #G_VLINE
            JSR wput_char
            LDA win_w,X
            SEC
            SBC #1
            STA cur_col
            LDA #G_VLINE
            JSR wput_char
            INC cur_row
            JMP @side
@bottom:    LDA #0
            STA cur_col
            LDA #G_BL
            JSR wput_char
-           LDA win_w,X
            SEC
            SBC #1
            CMP cur_col
            BEQ +
            LDA #G_HLINE
            JSR wput_char
            JMP -
+           LDA #G_BR
            JSR wput_char
            ; falls through: the content

; X = slot: clears the content and calls the window's draw routine
draw_content:
            STX cur_win
            JSR content_port
            LDA #' '
            STA fill_char
            LDA #COLOR_TEXT
            STA text_color
            JSR wfill
            LDA #0
            STA cur_col
            STA cur_row
            LDX cur_win
            LDA win_draw_hi,X
            BEQ +
            STA zp_call+1
            LDA win_draw_lo,X
            STA zp_call
            JMP (zp_call)               ; X = slot
+           RTS

; WIN_INVALIDATE: A = slot. Draws its content again (only the visible part changes on the screen)
win_invalidate:
            TAX
            LDA win_flags,X
            BEQ +
            JMP draw_content
+           RTS
