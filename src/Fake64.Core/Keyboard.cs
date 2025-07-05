using System.Runtime.InteropServices;

namespace Fake64.Core;

public class Keyboard(Board board)
{
    private byte[] keyboardMatrix = new byte[8] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };

    // For cycle-exact emulation
    private bool[,] pendingKeyChanges = new bool[8, 8];
    private bool[,] pendingKeyStates = new bool[8, 8];
    private bool pendingShiftKeyChange = false;
    private bool pendingShiftKeyState = false;
    private bool keyPollingNeeded = false;
    private int keyScanCycleCounter = 0;

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
        return result;
    }

    // Methods for keyboard input
    public void KeyDown(byte row, byte col)
    {
        if (row >= 0 && row < 8 && col >= 0 && col < 8)
        {
            // Clear the bit (0 = pressed)
            keyboardMatrix[row] &= (byte)~(1 << col);
        }
    }

    public void KeyUp(byte row, byte col)
    {
        if (row >= 0 && row < 8 && col >= 0 && col < 8)
        {
            // Set the bit (1 = released)
            keyboardMatrix[row] |= (byte)(1 << col);
        }
    }

    // Dictionary to track key states
    private Dictionary<int, bool> keyStates = new();

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    // Virtual key codes
    private const int VK_SHIFT = 0x10;
    private const int VK_LSHIFT = 0xA0;
    private const int VK_RSHIFT = 0xA1;

    // C64 Keyboard Matrix position for SHIFT key
    private const byte SHIFT_ROW = 1;
    private const byte SHIFT_COL = 7;

    // Map of PC virtual keys to C64 keyboard matrix positions
    private static readonly Dictionary<int, (byte Row, byte Col)> KeyMap = new()
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
        
        // Arrow keys
        { 37, (0, 2) },     // Left
        { 38, (6, 4) },     // Up 
        { 39, (0, 2) },     // Right
        { 40, (0, 7) },     // Down
        
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

    // Common special keys to poll
    private static readonly int[] keysToCheck = {
        // Letters
        'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M',
        'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
        
        // Numbers
        '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
        
        // Special keys
        32, 13, 8, 27, 37, 38, 39, 40,
        
        // Punctuation & others
        187, 189, 186, 222, 188, 190, 191, 220
    };

    // Process keyboard signals for the upcoming cycle
    internal void ProcessSignals(int currentCycle)
    {
        // In a cycle-exact emulation, we would check keyboard state at specific intervals
        // For example, every 10 cycles to avoid polling Windows keyboard APIs too frequently
        keyScanCycleCounter++;
        if (keyScanCycleCounter >= 10)
        {
            keyScanCycleCounter = 0;
            keyPollingNeeded = true;
            
            // Reset pending changes
            pendingKeyChanges = new bool[8, 8];
            pendingShiftKeyChange = false;
            
            // Check shift key state
            bool isShiftPressed = 
                (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0 ||
                (GetAsyncKeyState(VK_LSHIFT) & 0x8000) != 0 ||
                (GetAsyncKeyState(VK_RSHIFT) & 0x8000) != 0;
                
            // Determine if shift key state is changing
            bool currentShiftState = ((keyboardMatrix[SHIFT_ROW] & (1 << SHIFT_COL)) == 0);
            if (isShiftPressed != currentShiftState)
            {
                pendingShiftKeyChange = true;
                pendingShiftKeyState = isShiftPressed;
            }
            
            // Check all other keys
            foreach (int key in keysToCheck)
            {
                short keyState = GetAsyncKeyState(key);
                bool isPressed = (keyState & 0x8000) != 0;
                
                // Initialize state if this is the first check
                if (!keyStates.ContainsKey(key))
                    keyStates[key] = false;
                
                // Key state is changing
                if (isPressed != keyStates[key])
                {
                    // Find the matrix position for this key
                    if (KeyMap.TryGetValue(key, out var position))
                    {
                        pendingKeyChanges[position.Row, position.Col] = true;
                        pendingKeyStates[position.Row, position.Col] = isPressed;
                    }
                }
            }
        }
    }

    // Execute a single clock cycle
    public void Clock()
    {
        // Only apply keyboard changes when polling is needed
        if (keyPollingNeeded)
        {
            // First handle shift key
            if (pendingShiftKeyChange)
            {
                if (pendingShiftKeyState)
                    KeyDown(SHIFT_ROW, SHIFT_COL);
                else
                    KeyUp(SHIFT_ROW, SHIFT_COL);
            }
            
            // Then handle all other keys
            for (byte row = 0; row < 8; row++)
            {
                for (byte col = 0; col < 8; col++)
                {
                    if (pendingKeyChanges[row, col])
                    {
                        if (pendingKeyStates[row, col])
                            KeyDown(row, col);
                        else
                            KeyUp(row, col);
                            
                        // Update the tracked state in keyStates
                        foreach (var kvp in KeyMap)
                        {
                            if (kvp.Value.Row == row && kvp.Value.Col == col)
                            {
                                keyStates[kvp.Key] = pendingKeyStates[row, col];
                                break;
                            }
                        }
                    }
                }
            }
            
            keyPollingNeeded = false;
        }
    }
}
