namespace Fake64;

// I 64 tasti della matrice del C64. Il valore è riga * 8 + colonna:
// la riga è il bit della porta A della CIA1 che la seleziona, la colonna il bit letto sulla porta B.
// RESTORE non fa parte della matrice (è collegato alla linea NMI)
public enum C64Key
{
    Delete = 0, Return, CursorRight, F7, F1, F3, F5, CursorDown,
    D3 = 8, W, A, D4, Z, S, E, LeftShift,
    D5 = 16, R, D, D6, C, F, T, X,
    D7 = 24, Y, G, D8, B, H, U, V,
    D9 = 32, I, J, D0, M, K, O, N,
    Plus = 40, P, L, Minus, Period, Colon, At, Comma,
    Pound = 48, Asterisk, Semicolon, Home, RightShift, Equals, UpArrow, Slash,
    D1 = 56, LeftArrow, Control, D2, Space, Commodore, Q, RunStop,
}

// Tastiera del C64: la matrice di 8x8 contatti letta dalla CIA1.
// Chi la usa (ad esempio un adattatore per la tastiera del PC) comunica quali tasti sono premuti
public class Keyboard
{
    // Una riga per bit della porta A; un bit a 0 = tasto premuto. Sostituita in blocco, quindi
    // può essere aggiornata da un altro thread mentre la CIA1 la legge
    volatile byte[] matrix = NewMatrix();

    static byte[] NewMatrix() => [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];

    // Colonne lette dalla CIA1 (porta B) con le righe selezionate dai bit a 0 della porta A
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

    // Imposta l'insieme completo dei tasti premuti; quelli non elencati risultano rilasciati
    public void SetPressedKeys(IEnumerable<C64Key> keys)
    {
        var rows = NewMatrix();
        foreach (var key in keys)
        {
            int position = (int)key;
            rows[position >> 3] &= (byte)~(1 << (position & 0x07));
        }
        matrix = rows;
    }

    public void ReleaseAll() => matrix = NewMatrix();

    public bool IsPressed(C64Key key)
    {
        int position = (int)key;
        return (matrix[position >> 3] & (1 << (position & 0x07))) == 0;
    }
}
