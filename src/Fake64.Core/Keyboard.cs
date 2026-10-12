using System.Runtime.InteropServices;

namespace Fake64;

public class Keyboard(Board board)
{
    private byte[] keyboardMatrix = new byte[8] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };

    private int keyScanCycleCounter = 0;
    private const int KEY_SCAN_INTERVAL_CYCLES = 19656; // PAL: 312 linee x 63 cicli = 1 frame

    // Stato di un tasto del PC (codice virtual key di Windows); sostituibile nei test
    internal Func<int, bool> HostKeyDown { get; set; } = vk => (GetAsyncKeyState(vk) & 0x8000) != 0;

    // Method to get keyboard matrix state for CIA1
    public byte GetKeyboardState(byte rowSelectionMask)
    {
        // Return keyboard matrix columns based on rows selected by CIA1 Port A
        byte result = 0xFF;
        for (int row = 0; row < 8; row++)
        {
            // When a bit in rowSelectionMask is 0, that row is selected
            if ((rowSelectionMask & (1 << row)) == 0)
            {
                result &= keyboardMatrix[row];
            }
        }
        if (result != 0xff)
        { 
        }
        return result;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    // Virtual key codes
    private const int VK_SHIFT = 0x10;
    private const int VK_LSHIFT = 0xA0;
    private const int VK_RSHIFT = 0xA1;

    // C64 Keyboard Matrix position for SHIFT key
    private const byte SHIFT_ROW = 1;
    private const byte SHIFT_COL = 7;

    // Posizione nella matrice del C64 (riga = bit della porta A della CIA1, colonna = bit della porta B);
    // Shift = il tasto del C64 va premuto insieme allo Shift sinistro
    private readonly record struct C64Key(byte Row, byte Col, bool Shift = false)
    {
        public static implicit operator C64Key((byte Row, byte Col) position) => new(position.Row, position.Col);
    }

    // Map of PC virtual keys to C64 keyboard matrix positions
    private static readonly Dictionary<int, C64Key> KeyMap = new()
    {
        // Letters
        { 'A', (1, 2) },
        { 'B', (3, 4) },
        { 'C', (2, 4) },
        { 'D', (2, 2) },
        { 'E', (1, 6) },
        { 'F', (2, 5) },
        { 'G', (3, 2) },
        { 'H', (3, 5) },
        { 'I', (4, 1) },
        { 'J', (4, 2) },
        { 'K', (4, 5) },
        { 'L', (5, 2) },
        { 'M', (4, 4) },
        { 'N', (4, 7) },
        { 'O', (4, 6) },
        { 'P', (5, 1) },
        { 'Q', (7, 6) },
        { 'R', (2, 1) },
        { 'S', (1, 5) },
        { 'T', (2, 6) },
        { 'U', (3, 6) },
        { 'V', (3, 7) },
        { 'W', (1, 1) },
        { 'X', (2, 7) },
        { 'Y', (3, 1) },
        { 'Z', (1, 4) },
        
        // Numbers
        { '0', (4, 3) },
        { '1', (7, 0) },
        { '2', (7, 3) },
        { '3', (1, 0) },
        { '4', (1, 3) },
        { '5', (2, 0) },
        { '6', (2, 3) },
        { '7', (3, 0) },
        { '8', (3, 3) },
        { '9', (4, 0) },
        
        // Special keys
        { 32, (7, 4) },     // Space
        { 13, (0, 1) },     // Enter/Return
        { 8, (0, 0) },      // Backspace/Delete
        { 27, (7, 7) },     // Escape/Run Stop
        
        // Tasti cursore: il C64 ha solo CRSR destra e CRSR giù, sinistra e su si ottengono con Shift
        { 0x27, new C64Key(0, 2) },              // Right
        { 0x25, new C64Key(0, 2, Shift: true) }, // Left
        { 0x28, new C64Key(0, 7) },              // Down
        { 0x26, new C64Key(0, 7, Shift: true) }, // Up

        // Tasti funzione: F1, F3, F5, F7 sono tasti fisici, F2, F4, F6, F8 sono gli stessi con Shift
        { 0x70, new C64Key(0, 4) },              // F1
        { 0x71, new C64Key(0, 4, Shift: true) }, // F2
        { 0x72, new C64Key(0, 5) },              // F3
        { 0x73, new C64Key(0, 5, Shift: true) }, // F4
        { 0x74, new C64Key(0, 6) },              // F5
        { 0x75, new C64Key(0, 6, Shift: true) }, // F6
        { 0x76, new C64Key(0, 3) },              // F7
        { 0x77, new C64Key(0, 3, Shift: true) }, // F8
        
        // Punctuation
        { 190, (5, 4) },    // . (Period)
        { 188, (5, 7) },    // , (Comma)
        { 186, (5, 5) },    // ; (Semicolon)
        { 222, (6, 2) },    // ' (Apostrophe)
        { 191, (6, 7) },    // / (Slash)
        { 187, (5, 0) },    // + (Plus)
        { 189, (5, 3) },    // - (Minus)
        { 220, (6, 6) },    // \ (Backslash mapped to ^ on C64)
    };

    // Process keyboard signals for the upcoming cycle
    internal void ProcessSignals(int currentCycle)
    {
        // Lo stato dei tasti del PC viene letto una volta per frame (50 Hz): interrogare
        // GetAsyncKeyState per ogni tasto ogni pochi cicli rallentava l'emulazione di oltre 10 volte
        keyScanCycleCounter++;
        if (keyScanCycleCounter < KEY_SCAN_INTERVAL_CYCLES) return;
        keyScanCycleCounter = 0;

        ScanHostKeyboard();
    }

    // La matrice viene ricostruita da zero: lo Shift del C64 è premuto se lo è quello del PC
    // o se un tasto premuto lo richiede (F2, cursore su, ...)
    internal void ScanHostKeyboard()
    {
        var matrix = new byte[8] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
        bool shift = HostKeyDown(VK_SHIFT) || HostKeyDown(VK_LSHIFT) || HostKeyDown(VK_RSHIFT);

        foreach (var (virtualKey, key) in KeyMap)
        {
            if (!HostKeyDown(virtualKey)) continue;
            matrix[key.Row] &= (byte)~(1 << key.Col);
            shift |= key.Shift;
        }

        if (shift) matrix[SHIFT_ROW] &= unchecked((byte)~(1 << SHIFT_COL));

        keyboardMatrix = matrix;
    }

    // Execute a single clock cycle
    public void Clock()
    {
    }
}
