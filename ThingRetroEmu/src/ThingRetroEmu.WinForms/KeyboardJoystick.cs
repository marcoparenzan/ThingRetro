using ThingRetroEmu.Ports;
using ThingRetroEmu.Audio;

namespace ThingRetroEmu.WinForms;

public enum JoystickKeySet
{
    // 8 up, 2 down, 4 left, 6 right; 7, 9, 1, 3 diagonals; 0 or 5 fire (with Num Lock on)
    NumericKeypad,
    // arrows for the directions, Ctrl for fire (for laptops without a keypad)
    ArrowsAndCtrl,
}

// Simulator: the PC keyboard moves a Joystick connected to a control port. The keys come from a WinForms
// control (usually the MonitorControl) and only while it has the focus; the keys used by the joystick must be
// kept away from the machine keyboard (PcKeyboard.Excluded = UsesKey)
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

    public KeyboardJoystick(Control source, Joystick joystick, JoystickKeySet keySet, bool twoButtons = false)
    {
        this.source = source;
        this.joystick = joystick;
        KeySet = keySet;
        keys = new(keySet == JoystickKeySet.NumericKeypad ? NumericKeypad : ArrowsAndCtrl);
        if (twoButtons) keys[keySet == JoystickKeySet.NumericKeypad ? Keys.Decimal : Keys.ShiftKey] = (Direction)32;
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

    // Several keys together add up (up + right = diagonal), like the joystick's switches
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
        joystick.Fire2 = ((int)direction & 32) != 0;
    }

    public void Dispose()
    {
        source.KeyDown -= OnKeyDown;
        source.KeyUp -= OnKeyUp;
        source.LostFocus -= OnLostFocus;
        joystick.ReleaseAll();
    }
}
