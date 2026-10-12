using Fake64.C64;

namespace Fake64.WinForms;

// Il cavo della tastiera: porta i tasti premuti su un controllo WinForms (di solito il C64Monitor)
// alla tastiera del C64. I tasti arrivano solo quando quel controllo ha il focus, così il C64 può
// stare in una form con altri controlli; perdendo il focus tutti i tasti vengono rilasciati
public sealed class PcKeyboard : IDisposable
{
    // Il KERNAL legge la tastiera 60 volte al secondo: un tasto premuto e rilasciato più in fretta
    // (SendKeys, incolla, digitazione molto veloce) andrebbe perso, quindi resta premuto almeno così
    public static readonly TimeSpan MinimumHold = TimeSpan.FromMilliseconds(40);

    readonly Control source;
    readonly Keyboard target;
    readonly HashSet<Keys> pressed = [];
    readonly Dictionary<Keys, long> pressedAt = [];
    readonly Dictionary<Keys, long> releaseDueAt = [];
    readonly System.Windows.Forms.Timer releaseTimer = new() { Interval = 10 };

    // Orologio in millisecondi, sostituibile nei test
    internal Func<long> Now { get; set; } = () => Environment.TickCount64;

    // Tasti riservati ad altro (per esempio al joystick da tastiera): non arrivano alla tastiera del C64
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
        e.SuppressKeyPress = true; // F10 e simili non attivano il menu della form
        releaseDueAt.Remove(e.KeyCode);
        if (!pressed.Add(e.KeyCode)) return; // la ripetizione automatica di Windows non cambia nulla
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

    // Rilascia i tasti la cui durata minima è trascorsa (chiamato dal timer)
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

    void Update() => target.SetPressedKeys(PcKeyboardLayout.ToC64Keys(pressed));

    public void Dispose()
    {
        source.KeyDown -= OnKeyDown;
        source.KeyUp -= OnKeyUp;
        source.LostFocus -= OnLostFocus;
        releaseTimer.Dispose();
        target.ReleaseAll();
    }
}
