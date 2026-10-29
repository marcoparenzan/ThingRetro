; ----------------------------------------------------------------------------------------------------------
; DESKTOP - the menu bar, the main loop and the windows the OS opens by itself
;
; The main loop sends the keys to the window on top and follows the left button: a press on the menu bar
; opens a menu (held down: release on an item to choose it), on a window brings it to the front, then on
; its close box closes it, on its title bar starts dragging it, on its content sends it EV_CLICK.
; The right end of the menu bar shows what the input drivers see: pointer position, buttons, last key.
; ----------------------------------------------------------------------------------------------------------

desktop:    LDA #$FF
            STA about_slot
            STA drag_grab
            LDA #' '
            STA last_key
            JSR draw_menu_bar
            JSR redraw_all

main_loop:  JSR getkey
            BEQ @mouse
            STA last_key
            STA ev_x
            JSR wm_top
            CMP #$FF
            BEQ @mouse
            TAX
            LDA #EV_KEY
            JSR send_event
@mouse:     JSR pointer_cell
            LDA buttons
            AND #BUTTON_LEFT
            BEQ @released
            LDA prev_buttons
            BNE @held
            JSR on_press
            JMP @done
@held:      JSR on_drag
            JMP @done
@released:  LDA #$FF
            STA drag_grab
@done:      LDA buttons
            AND #BUTTON_LEFT
            STA prev_buttons
            JSR show_input
            JMP main_loop

; pcol, prow: the cell under the tip of the pointer
pointer_cell:
            SEI
            LDA pointer_x
            LDX pointer_x+1
            LDY pointer_y
            CLI
            STA zp_t1
            STX zp_t0
            LSR zp_t0                   ; x / 8: the first shift through both bytes, the high byte is 0 or 1
            ROR zp_t1
            LSR zp_t1
            LSR zp_t1
            LDA zp_t1
            STA pcol
            TYA
            LSR
            LSR
            LSR
            STA prow
            RTS

; The map value of the cell under the pointer in A
cell_owner: LDY prow
            JSR row_address
            LDY pcol
            LDA (zp_mptr),Y
            RTS

; The left button went down
on_press:   LDA #$FF
            STA drag_grab
            LDA prow
            BNE +
            JMP menu_click
+           JSR cell_owner
            BEQ @none                   ; the desktop
            SEC
            SBC #1
            STA click_slot
            JSR win_front
            LDX click_slot
            LDA prow
            CMP win_y,X
            BNE @content
            LDA pcol                    ; title bar: the close box is its first cell
            CMP win_x,X
            BNE +
            LDA click_slot
            JMP win_close
+           SEC
            SBC win_x,X
            STA drag_grab
            RTS
@content:   LDA pcol                    ; inside the frame?
            SEC
            SBC win_x,X
            BEQ @none
            STA zp_t0
            CLC
            ADC #1
            CMP win_w,X
            BCS @none
            DEC zp_t0
            LDA zp_t0
            STA ev_x
            LDA prow
            SEC
            SBC win_y,X
            STA zp_t0
            CLC
            ADC #1
            CMP win_h,X
            BCS @none
            DEC zp_t0
            LDA zp_t0
            STA ev_y
            LDA #EV_CLICK
            JMP send_event
@none:      RTS

; The left button is held: the window follows the pointer if a drag started on its title bar
on_drag:    LDA drag_grab
            CMP #$FF
            BEQ @none
            LDX click_slot
            LDA win_x,X
            STA zp_t2
            LDA win_y,X
            STA zp_t3
            LDA pcol
            SEC
            SBC drag_grab
            BCS +
            LDA #0
+           STA win_x,X
            LDA prow
            STA win_y,X
            JSR fit_window
            LDA win_x,X
            CMP zp_t2
            BNE +
            LDA win_y,X
            CMP zp_t3
            BEQ @none
+           JMP redraw_all
@none:      RTS

; ---------------------------------------------------------------------------------------------- menus

MENU_COUNT  = 3
MENU_W      = 15            ; width of a drop down

draw_menu_bar:
            LDA #COLOR_TEXT
            STA text_color
            LDA #' '
            STA fill_char
            LDA #0
            STA rect_x
            STA rect_y
            LDA #COLUMNS
            STA rect_w
            LDA #1
            STA rect_h
            JSR fill_rect
            LDX #1
            LDY #0
            JSR goto
            LDA #<menu_text
            LDX #>menu_text
            JMP put_string

; A press on the menu bar: which menu is under the pointer
menu_click: LDX #MENU_COUNT - 1
-           LDA pcol
            CMP menu_start,X
            BCS +
            DEX
            BPL -
            RTS
+           CMP menu_end,X
            BCS @none
            STX menu_cur
            LDA drop_x,X
            STA menu_x
            LDA menu_count,X
            STA menu_n
            JMP menu_loop
@none:      RTS

; The drop down follows the pointer while the button is held; releasing it on an item runs its action
menu_loop:  LDA #$FF
            STA menu_sel
            JSR draw_drop_down
@poll:      JSR pointer_cell
            LDA #$FF
            STA zp_t1
            LDA prow
            SEC
            SBC #1
            BCC @set
            CMP menu_n
            BCS @set
            STA zp_t2
            LDA pcol
            SEC
            SBC menu_x
            BCC @set
            CMP #MENU_W
            BCS @set
            LDA zp_t2
            STA zp_t1
@set:       LDA zp_t1
            CMP menu_sel
            BEQ +
            STA menu_sel
            JSR draw_drop_down
+           LDA buttons
            AND #BUTTON_LEFT
            BNE @poll

            JSR redraw_all              ; the drop down goes away
            LDA menu_sel
            CMP #$FF
            BEQ @none
            ASL
            ASL
            CLC
            ADC #2                      ; the action follows the text of the item
            TAY
            JSR menu_items
            LDA (zp_t4),Y
            STA zp_call
            INY
            LDA (zp_t4),Y
            BEQ @none                   ; an item that does nothing (yet)
            STA zp_call+1
            JMP (zp_call)
@none:      RTS

; zp_t4: the items of the open menu (text, action for each)
menu_items: LDX menu_cur
            LDA menu_list_lo,X
            STA zp_t4
            LDA menu_list_hi,X
            STA zp_t5
            RTS

; The items of the open menu below its title, menu_sel highlighted
draw_drop_down:
            JSR menu_items
            LDA #0
            STA zp_t0
@item:      CMP menu_n
            BEQ @done
            CMP menu_sel
            BNE +
            LDA #COLOR_ACTIVE
            STA text_color
            LDA #$80
            STA menu_mask
            BNE ++
+           LDA #COLOR_TEXT
            STA text_color
            LDA #0
            STA menu_mask
++          LDA zp_t0
            ASL
            ASL
            TAY
            LDA (zp_t4),Y
            STA zp_str
            INY
            LDA (zp_t4),Y
            STA zp_str+1
            LDX menu_x
            LDY zp_t0
            INY
            JSR goto
            LDA #' '
            ORA menu_mask
            JSR put_char
            LDY #0
@char:      LDA (zp_str),Y
            BEQ @pad
            ORA menu_mask
            JSR put_char
            INY
            BNE @char
@pad:       LDA cur_col
            SEC
            SBC menu_x
            CMP #MENU_W
            BCS @next
            LDA #' '
            ORA menu_mask
            JSR put_char
            JMP @pad
@next:      INC zp_t0
            LDA zp_t0
            JMP @item
@done:      RTS

menu_text:  !raw "ThingOS   File   Disk", 0

menu_start: !byte 1, 10, 17         ; the columns of the menu bar each menu answers to
menu_end:   !byte 10, 17, 24
drop_x:     !byte 0, 10, 17         ; left column of the drop down
menu_count: !byte 1, 2, 1
menu_list_lo: !byte <menu_thingos, <menu_file, <menu_disk
menu_list_hi: !byte >menu_thingos, >menu_file, >menu_disk

menu_thingos:
            !word item_about, open_about
menu_file:  !word item_new, open_demo
            !word item_close, close_top
menu_disk:  !word item_directory, 0

item_about: !raw "About ThingOS", 0
item_new:   !raw "New window", 0
item_close: !raw "Close window", 0
item_directory: !raw "Directory", 0

close_top:  JSR wm_top
            CMP #$FF
            BEQ +
            JMP win_close
+           RTS

; ---------------------------------------------------------------------------------------------- about

open_about: LDX about_slot
            CPX #$FF
            BEQ +
            TXA
            JMP win_front
+           LDA #<about_window
            LDX #>about_window
            JSR win_open
            BCS +
            STA about_slot
+           RTS

about_window:
            !byte 7, 6, 26, 8
            !word about_title, about_draw, about_event

about_title: !raw "About", 0

about_draw: LDA #2
            STA cur_col
            LDA #1
            STA cur_row
            LDA #<about_line1
            LDX #>about_line1
            JSR wput_string
            LDA #2
            STA cur_col
            LDA #3
            STA cur_row
            LDA #<about_line2
            LDX #>about_line2
            JSR wput_string
            LDA #2
            STA cur_col
            LDA #4
            STA cur_row
            LDA #<about_line3
            LDX #>about_line3
            JMP wput_string

about_line1: !raw "ThingOS 0.2", 0
about_line2: !raw "A windowing system", 0
about_line3: !raw "for the Commodore 64", 0

about_event:
            CMP #EV_CLOSE
            BNE +
            LDA #$FF
            STA about_slot
+           RTS

; ---------------------------------------------------------------------------------------------- demo

; File > New window: a window that shows the keys typed and marks the clicks, each a bit lower and to
; the right of the previous one
open_demo:  LDX #9
-           LDA demo_window,X
            STA open_desc,X
            DEX
            BPL -
            LDA demo_count
            ASL
            CLC
            ADC demo_count
            CLC
            ADC #2
            STA open_desc               ; x = 2 + 3 n
            LDA demo_count
            ASL
            CLC
            ADC #3
            STA open_desc+1             ; y = 3 + 2 n
            INC demo_count
            LDA demo_count
            CMP #6
            BCC +
            LDA #0
            STA demo_count
+           LDA #<open_desc
            LDX #>open_desc
            JMP win_open

demo_window:
            !byte 0, 0, 22, 8
            !word demo_title, demo_draw, demo_event

demo_title: !raw "Window", 0
demo_text:  !raw "Click or type here", 0

demo_draw:  LDA #<demo_text
            LDX #>demo_text
            JMP wput_string

; EV_KEY: the key on row 2 (win_data0 is the column); EV_CLICK: a star where the click was
demo_event: CMP #EV_CLICK
            BNE +
            STX cur_col
            STY cur_row
            LDA #COLOR_TEXT
            STA text_color
            LDA #'*'
            JMP wput_char
+           CMP #EV_KEY
            BNE @done
            TXA
            CMP #' '
            BCC @done
            CMP #$7F
            BCS @done
            PHA
            LDX cur_win
            LDA win_data0,X
            STA cur_col
            INC win_data0,X
            LDA #2
            STA cur_row
            LDA #COLOR_TEXT
            STA text_color
            PLA
            JMP wput_char
@done:      RTS

; ---------------------------------------------------------------------------------------------- status

; The port on the menu bar: the map is 0 there, like port_win
menubar_port:
            LDA #0
            STA port_win
            STA port_ox
            STA port_oy
            STA port_left
            STA port_top
            LDA #COLUMNS
            STA port_right
            LDA #1
            STA port_bottom
            RTS

; "xxx,yyy LR k" at the right end of the menu bar
show_input: JSR menubar_port
            LDA #COLOR_TEXT
            STA text_color
            LDX #27
            LDY #0
            JSR goto
            SEI                         ; the IRQ must not change the position halfway through the read
            LDA pointer_x
            LDX pointer_x+1
            CLI
            JSR put_number
            LDA #','
            JSR wput_char
            SEI
            LDA pointer_y
            LDX pointer_y+1
            CLI
            JSR put_number
            LDA #' '
            JSR wput_char
            LDA buttons
            AND #BUTTON_LEFT
            BEQ +
            LDA #'L'
            BNE ++
+           LDA #'-'
++          JSR wput_char
            LDA buttons
            AND #BUTTON_RIGHT
            BEQ +
            LDA #'R'
            BNE ++
+           LDA #'-'
++          JSR wput_char
            LDA #' '
            JSR wput_char
            LDA last_key
            JMP wput_char
