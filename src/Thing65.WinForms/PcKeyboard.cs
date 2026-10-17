using Thing65.C65;

namespace Thing65.WinForms;

// The keyboard cable: takes the keys pressed on a WinForms control (usually the C65Monitor) to the
// C64 keyboard. The keys arrive only while that control has the focus, so the C64 can live in a form
// with other controls; when the focus is lost all keys are released
public sealed class PcKeyboard : IDisposable
{
    // The KERNAL reads the keyboard 60 times per second: a key pressed and released faster than that
    // (SendKeys, paste, very fast typing) would be lost, so it stays pressed at least this long
    public static readonly TimeSpan MinimumHold = TimeSpan.FromMilliseconds(40);

    readonly Control source;
    readonly Keyboard target;
    readonly HashSet<Keys> pressed = [];
    readonly Dictionary<Keys, long> pressedAt = [];
    readonly Dictionary<Keys, long> releaseDueAt = [];
    readonly System.Windows.Forms.Timer releaseTimer = new() { Interval = 10 };

    // Clock in milliseconds, replaceable in the tests
    internal Func<long> Now { get; set; } = () => Environment.TickCount64;

    // Keys reserved for something else (for example the keyboard joystick): they do not reach the C64 keyboard
    public Func<Keys, bool>? Excluded { get; set; }

    bool Handles(Keys key) => PcKeyboardLayout.IsMapped(key) && Excluded?.Invoke(key) != true;

    public PcKeyboard(Control source, Keyboard target)
    {
        this.source = source;
        this.target = target;
        source.KeyDown += OnKeyDown;
        source.KeyUp += OnKeyUp;
        source.LostFocus += OnLostFocus;
        releaseTimer.Tick += (_, _) => ReleaseDueKeys();
    }

    void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!Handles(e.KeyCode)) return;
        e.Handled = true;
        e.SuppressKeyPress = true; // F10 and the like do not activate the form's menu
        releaseDueAt.Remove(e.KeyCode);
        if (!pressed.Add(e.KeyCode)) return; // Windows auto-repeat changes nothing
        pressedAt[e.KeyCode] = Now();
        Update();
    }

    void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (!pressed.Contains(e.KeyCode)) return;
        e.Handled = true;

        long releaseAt = pressedAt[e.KeyCode] + (long)MinimumHold.TotalMilliseconds;
        if (Now() >= releaseAt)
        {
            Release(e.KeyCode);
            Update();
        }
        else
        {
            releaseDueAt[e.KeyCode] = releaseAt;
            releaseTimer.Start();
        }
    }

    // Releases the keys whose minimum hold time has passed (called by the timer)
    internal void ReleaseDueKeys()
    {
        long now = Now();
        var due = releaseDueAt.Where(entry => entry.Value <= now).Select(entry => entry.Key).ToList();
        foreach (var key in due) Release(key);
        if (due.Count > 0) Update();
        if (releaseDueAt.Count == 0) releaseTimer.Stop();
    }

    void Release(Keys key)
    {
        pressed.Remove(key);
        pressedAt.Remove(key);
        releaseDueAt.Remove(key);
    }

    void OnLostFocus(object? sender, EventArgs e)
    {
        pressed.Clear();
        pressedAt.Clear();
        releaseDueAt.Clear();
        releaseTimer.Stop();
        target.ReleaseAll();
    }

    void Update() => target.SetPressedKeys(PcKeyboardLayout.ToC65Keys(pressed));

    public void Dispose()
    {
        source.KeyDown -= OnKeyDown;
        source.KeyUp -= OnKeyUp;
        source.LostFocus -= OnLostFocus;
        releaseTimer.Dispose();
        target.ReleaseAll();
    }
}
