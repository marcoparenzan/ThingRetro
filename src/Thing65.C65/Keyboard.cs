namespace Thing65.C65;

// The 64 keys of the C64 matrix. The value is row * 8 + column:
// the row is the bit of CIA1 port A that selects it, the column the bit read on port B.
// RESTORE is not part of the matrix (it is wired to the NMI line, see Keyboard.Restore)
public enum C65Key
{
    Delete = 0, Return, CursorRight, F7, F1, F3, F5, CursorDown,
    D3 = 8, W, A, D4, Z, S, E, LeftShift,
    D5 = 16, R, D, D6, C, F, T, X,
    D7 = 24, Y, G, D8, B, H, U, V,
    D9 = 32, I, J, D0, M, K, O, N,
    Plus = 40, P, L, Minus, Period, Colon, At, Comma,
    Pound = 48, Asterisk, Semicolon, Home, RightShift, Equals, UpArrow, Slash,
    D1 = 56, LeftArrow, Control, D2, Space, Commodore, Q, RunStop,
    // The nine extra keys of the C65 keyboard, on a ninth line (C8): row 8, read through the same columns. F10, F12 and
    // F14 are the shifted F9, F11 and F13
    NoScroll = 64, Tab, Alt, Help, F9, F11, F13, Escape,
    // Not in the matrix: RESTORE pulls the NMI line low
    Restore = 100,
}

// The C64 keyboard: the 8x8 contact matrix read by CIA1.
// Whoever uses it (for example an adapter for the PC keyboard) tells which keys are pressed
public class Keyboard
{
    // One row per bit of port A; a bit at 0 = key pressed. Replaced as a whole, so it can be
    // updated from another thread while CIA1 reads it
    volatile byte[] matrix = NewMatrix();
    volatile bool capsLock;
    volatile bool restore;

    static byte[] NewMatrix() => [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];

    // Columns read by CIA1 (port B) with the rows selected by the bits at 0 of port A
    public byte GetKeyboardState(byte rowSelectionMask)
    {
        var rows = matrix;
        byte result = 0xFF;
        for (int row = 0; row < 8; row++)
        {
            if ((rowSelectionMask & (1 << row)) == 0)
                result &= rows[row];
        }
        return result;
    }

    // The CAPS LOCK key of the C65: a locking key, down until it is pressed again. It is not in the matrix: the KERNAL
    // senses it at bit 0 of $D607 (low = down, the board takes it there)
    public bool CapsLock { get => capsLock; set => capsLock = value; }

    // The RESTORE key: while it is down the NMI line is low
    public bool Restore => restore;

    // Columns read when the C65's ninth line (C8) is selected: the keys NoScroll...Escape
    public byte GetExtendedKeyboardState() => matrix[8];

    // Sets the complete set of pressed keys; the keys not listed are released
    public void SetPressedKeys(IEnumerable<C65Key> keys)
    {
        var rows = NewMatrix();
        bool restoreDown = false;
        foreach (var key in keys)
        {
            if (key == C65Key.Restore)
            {
                restoreDown = true;
                continue;
            }
            int position = (int)key;
            rows[position >> 3] &= (byte)~(1 << (position & 0x07));
        }
        matrix = rows;
        restore = restoreDown;
    }

    public void ReleaseAll()
    {
        matrix = NewMatrix();
        restore = false;
    }

    public bool IsPressed(C65Key key)
    {
        if (key == C65Key.Restore) return restore;
        int position = (int)key;
        return (matrix[position >> 3] & (1 << (position & 0x07))) == 0;
    }
}
