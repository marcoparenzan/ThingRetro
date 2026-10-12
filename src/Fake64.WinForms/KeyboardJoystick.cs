using Fake64.C64;

namespace Fake64.WinForms;

public enum JoystickKeySet
{
    // 8 su, 2 giù, 4 sinistra, 6 destra; 7, 9, 1, 3 diagonali; 0 o 5 fuoco (con Bloc Num attivo)
    NumericKeypad,
    // frecce per le direzioni, Ctrl per il fuoco (per i portatili senza tastierino)
    ArrowsAndCtrl,
}

// Simulatore: la tastiera del PC muove un Joystick collegato a una porta di controllo. I tasti arrivano da
// un controllo WinForms (di solito il C64Monitor) e solo quando ha il focus; quelli usati dal joystick vanno
// esclusi dalla tastiera del C64 (PcKeyboard.Excluded = UsesKey)
public sealed class KeyboardJoystick : IDisposable
{
    [Flags]
    enum Direction { None = 0, Up = 1, Down = 2, Left = 4, Right = 8, Fire = 16 }

    static readonly Dictionary<Keys, Direction> NumericKeypad = new()
    {
        [Keys.NumPad8] = Direction.Up,
        [Keys.NumPad2] = Direction.Down,
        [Keys.NumPad4] = Direction.Left,
        [Keys.NumPad6] = Direction.Right,
        [Keys.NumPad7] = Direction.Up | Direction.Left,
        [Keys.NumPad9] = Direction.Up | Direction.Right,
        [Keys.NumPad1] = Direction.Down | Direction.Left,
        [Keys.NumPad3] = Direction.Down | Direction.Right,
        [Keys.NumPad0] = Direction.Fire,
        [Keys.NumPad5] = Direction.Fire,
    };

    static readonly Dictionary<Keys, Direction> ArrowsAndCtrl = new()
    {
        [Keys.Up] = Direction.Up,
        [Keys.Down] = Direction.Down,
        [Keys.Left] = Direction.Left,
        [Keys.Right] = Direction.Right,
        [Keys.ControlKey] = Direction.Fire,
    };

    readonly Control source;
    readonly Joystick joystick;
    readonly Dictionary<Keys, Direction> keys;
    readonly HashSet<Keys> pressed = [];

    public KeyboardJoystick(Control source, Joystick joystick, JoystickKeySet keySet)
    {
        this.source = source;
        this.joystick = joystick;
        KeySet = keySet;
        keys = keySet == JoystickKeySet.NumericKeypad ? NumericKeypad : ArrowsAndCtrl;
        source.KeyDown += OnKeyDown;
        source.KeyUp += OnKeyUp;
        source.LostFocus += OnLostFocus;
    }

    public JoystickKeySet KeySet { get; }

    public bool UsesKey(Keys key) => keys.ContainsKey(key);

    void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!UsesKey(e.KeyCode)) return;
        e.Handled = true;
        e.SuppressKeyPress = true;
        if (pressed.Add(e.KeyCode)) Update();
    }

    void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (!UsesKey(e.KeyCode)) return;
        e.Handled = true;
        if (pressed.Remove(e.KeyCode)) Update();
    }

    void OnLostFocus(object? sender, EventArgs e)
    {
        pressed.Clear();
        joystick.ReleaseAll();
    }

    // Più tasti insieme si sommano (su + destra = diagonale), come gli interruttori del joystick
    void Update()
    {
        var direction = Direction.None;
        foreach (var key in pressed) direction |= keys[key];
        joystick.SetState(
            up: direction.HasFlag(Direction.Up),
            down: direction.HasFlag(Direction.Down),
            left: direction.HasFlag(Direction.Left),
            right: direction.HasFlag(Direction.Right),
            fire: direction.HasFlag(Direction.Fire));
    }

    public void Dispose()
    {
        source.KeyDown -= OnKeyDown;
        source.KeyUp -= OnKeyUp;
        source.LostFocus -= OnLostFocus;
        joystick.ReleaseAll();
    }
}
