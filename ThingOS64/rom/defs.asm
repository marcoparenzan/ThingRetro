; ----------------------------------------------------------------------------------------------------------
; DEFS - the chips' registers and the memory map of ThingOS
; ----------------------------------------------------------------------------------------------------------

; VIC-II
VIC_SPR0X   = $D000
VIC_SPR0Y   = $D001
VIC_SPR1X   = $D002
VIC_SPR1Y   = $D003
VIC_SPRXHI  = $D010         ; bit 8 of the sprites' X
VIC_CTRL1   = $D011
VIC_RASTER  = $D012
VIC_SPREN   = $D015
VIC_CTRL2   = $D016
VIC_MEMPTR  = $D018
VIC_IRQ     = $D019         ; interrupt flags: writing a 1 clears the flag
VIC_IRQEN   = $D01A
VIC_BORDER  = $D020
VIC_BG      = $D021

; SID
SID         = $D400
SID_POTX    = $D419         ; POT X of the selected control port (CIA 1 PA6: port 1, PA7: port 2)
SID_POTY    = $D41A

; CIA 1: keyboard, control ports
CIA1_PRA    = $DC00         ; out: keyboard columns, PA6/PA7 POT switch; in: control port 2
CIA1_PRB    = $DC01         ; in: keyboard rows, control port 1
CIA1_DDRA   = $DC02
CIA1_DDRB   = $DC03
CIA1_ICR    = $DC0D
CIA1_CRA    = $DC0E
CIA1_CRB    = $DC0F

; CIA 2: VIC-II bank, serial bus
CIA2_PRA    = $DD00         ; PA0-PA1 VIC-II bank (inverted), PA3 ATN out, PA4 CLK out, PA5 DATA out, PA6 CLK in, PA7 DATA in
CIA2_DDRA   = $DD02
CIA2_ICR    = $DD0D
CIA2_CRA    = $DD0E
CIA2_CRB    = $DD0F

; Colors
BLACK       = 0
WHITE       = 1
RED         = 2
CYAN        = 3
PURPLE      = 4
GREEN       = 5
BLUE        = 6
YELLOW      = 7
ORANGE      = 8
BROWN       = 9
LIGHT_RED   = 10
DARK_GREY   = 11
GREY        = 12
LIGHT_GREEN = 13
LIGHT_BLUE  = 14
LIGHT_GREY  = 15

; Screen: VIC-II bank 0, screen at $0400, characters from the CHARGEN ROM (the VIC-II sees it at $1000)
SCREEN      = $0400
COLOR_RAM   = $D800
SPRITE_PTRS = SCREEN + $03F8
COLUMNS     = 40
ROWS        = 25

; Characters (see font.asm): $20-$7E ASCII, $80-$FF the reverse of $00-$7F
G_SOLID     = $A0           ; reverse space: the whole cell in the foreground color
G_CLOSE     = $0C

; Keys that are not ASCII (GETKEY)
KEY_DEL     = $08
KEY_RETURN  = $0D
KEY_ESCAPE  = $1B           ; RUN/STOP
KEY_UP      = $81
KEY_DOWN    = $82
KEY_LEFT    = $83
KEY_RIGHT   = $84
KEY_F1      = $85           ; F1-F8: $85-$8C
KEY_HOME    = $8D
KEY_CLEAR   = $8E           ; SHIFT + HOME
KEY_INSERT  = $8F           ; SHIFT + DEL

; Modifier bits (modifiers)
MOD_SHIFT   = %00000001
MOD_CTRL    = %00000010
MOD_CBM     = %00000100

; Pointer buttons (buttons)
BUTTON_LEFT = %00000001     ; mouse left button or joystick fire
BUTTON_RIGHT = %00000010    ; mouse right button

; ----------------------------------------------------------------------------------------------------------
; RAM used by the OS. Zero page $02-$3F is the OS's, $40-$FF the application's
; ----------------------------------------------------------------------------------------------------------

; Main program (desktop, drawing): temporaries that the IRQ never touches
zp_ptr      = $02           ; screen RAM of the current row (2)
zp_cptr     = $04           ; color RAM of the current row (2)
zp_str      = $06           ; string being written (2)
zp_t0       = $08
zp_t1       = $09
zp_t2       = $0A
zp_t3       = $0B
zp_num      = $0C           ; number being written in decimal (2)
zp_char     = $0E

; Drawing state
text_color  = $10           ; color of the characters written
cur_col     = $11           ; cursor of put_char
cur_row     = $12
rect_x      = $13           ; rectangle of fill_rect
rect_y      = $14
rect_w      = $15
rect_h      = $16
fill_char   = $17
zp_mptr     = $18           ; ownership map of the current row (2)
zp_call     = $1A           ; routine called through JMP (zp_call): drawing, events, menu actions (2)
zp_t4       = $1C           ; zp_t4/zp_t5 also as a pointer
zp_t5       = $1D
zp_t6       = $1E           ; used by wput_char only
zp_t7       = $1F

; Input, updated by the IRQ
pointer_x   = $20           ; 0-319 (2)
pointer_y   = $22           ; 0-199 (2)
buttons     = $24           ; BUTTON_LEFT, BUTTON_RIGHT
modifiers   = $25           ; MOD_SHIFT, MOD_CTRL, MOD_CBM
ticks       = $26           ; frames since power on (2)
key_head    = $28           ; key buffer: next key to read
key_tail    = $29           ; key buffer: next free place
pot_x_old   = $2A           ; last POT X/Y of the mouse
pot_y_old   = $2B
joy_held    = $2C           ; frames the joystick has been held in a direction
joy2        = $2D           ; control port 2 lines (1 = closed)
port1       = $2E           ; control port 1 lines (1 = closed)

; IRQ temporaries
irq_t0      = $30
irq_t1      = $31
irq_t2      = $32
irq_t3      = $33

; Window manager: the drawing port, where wput_char may write (absolute cells, right and bottom excluded;
; the origin is the cell of cursor 0,0) and which map value it must find there
port_win    = $34
port_ox     = $35
port_oy     = $36
port_left   = $37
port_top    = $38
port_right  = $39
port_bottom = $3A
pcol        = $3B           ; the cell under the pointer (pointer_cell)
prow        = $3C
prev_buttons = $3D          ; the left button in the previous pass of the main loop
cur_win     = $3E           ; the window being drawn or receiving an event
last_key    = $3F           ; the last key typed

; Pages 2 and 3
irq_vector  = $0300         ; the IRQ handler (after A, X, Y have been pushed) (2)
nmi_vector  = $0302         ; the NMI handler (2)
key_matrix  = $0310         ; keyboard columns read in this frame, 0 = pressed (8)
key_prev    = $0318         ; the same, in the previous frame (8)
key_buffer  = $0320         ; keys typed, a ring of 16
KEY_BUFFER_SIZE = 16
POINTER_SPRITES = $0340     ; sprites 0 and 1 of the pointer: outline at $0340 (block 13), fill at $0380 (14)

; Applications
APP_START   = $0800         ; programs go from here up to $9FFF

; OS RAM at $C000
OS_RAM      = $C000
OS_RAM_PAGES = 6
MAP         = $C000         ; one byte per cell: 0 = desktop, n = window n - 1 on top there (1000)
WIN_MAX     = 8
win_x       = $C400         ; the windows, one array per field, indexed by the slot (0-7)
win_y       = $C408         ; outer rectangle: title bar on the first row, frame around the content
win_w       = $C410
win_h       = $C418
win_flags   = $C420         ; bit 7: in use
win_title_lo = $C428
win_title_hi = $C430
win_draw_lo = $C438         ; draws the content (0 = none): X = slot, the port on the content
win_draw_hi = $C440
win_event_lo = $C448        ; receives the events (0 = none): A = event, X/Y = its data, cur_win = slot
win_event_hi = $C450
win_data0   = $C458         ; four bytes for the owner of the window
win_data1   = $C460
win_data2   = $C468
win_data3   = $C470
z_order     = $C478         ; slots from the bottom to the top (8)
z_count     = $C480
wm_i        = $C481         ; loop counters of build_map and redraw_all
wm_j        = $C482
wm_slot     = $C483
click_slot  = $C484
drag_grab   = $C485         ; column of the title bar where the drag started
ev_x        = $C486         ; data of the event being sent
ev_y        = $C487
menu_cur    = $C488         ; the open menu
menu_sel    = $C489         ; its highlighted item ($FF = none)
menu_phase  = $C48A         ; 0 = button held since the opening, 1 = released, 2 = pressed again
menu_mask   = $C48B         ; $80 = reversed
menu_x      = $C48C         ; the drop down: left column, width, items
menu_w      = $C48D
menu_n      = $C48E
about_slot  = $C48F         ; the About window ($FF = closed)
demo_count  = $C490         ; windows opened with File > New window
open_desc   = $C498         ; a window descriptor being built (10)

; Window descriptor (WIN_OPEN): x, y, w, h, title (2), draw (2), event (2)
; Events
EV_KEY      = 1             ; X = the key
EV_CLICK    = 2             ; X, Y = the cell of the content
EV_CLOSE    = 3             ; the window is about to close

; Interface colors
COLOR_DESKTOP  = LIGHT_BLUE
COLOR_TEXT     = BLACK
COLOR_FRAME    = BLACK
COLOR_ACTIVE   = BLUE       ; title bar of the window on top, highlighted menu item
COLOR_INACTIVE = DARK_GREY

; Frame characters
G_HLINE     = $02
G_VLINE     = $04
G_BL        = $05
G_BR        = $06
