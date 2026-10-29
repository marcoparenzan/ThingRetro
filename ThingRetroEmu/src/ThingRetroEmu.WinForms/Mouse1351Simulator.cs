using ThingRetroEmu.Ports;

namespace ThingRetroEmu.WinForms;

// Simulator: the PC mouse moves a 1351 mouse. A left click on the control captures the mouse (cursor hidden and
// confined); then the movement goes to the 1351 and both buttons are its buttons, so the control's context menu
// is put aside. A middle click, Ctrl + right click or losing the focus releases the mouse (and gives the menu back).
// Direction: the 1351 counts up when moved away from the user, the PC screen counts down
public sealed class Mouse1351Simulator : IDisposable
{
    readonly Control source;
    readonly Mouse1351 mouse;
    double pendingX, pendingY;
    bool captured;
    Point center;
    ContextMenuStrip? menu;

    public Mouse1351Simulator(Control source, Mouse1351 mouse)
    {
        this.source = source;
        this.mouse = mouse;
        source.MouseDown += OnMouseDown;
        source.MouseUp += OnMouseUp;
        source.MouseMove += OnMouseMove;
        source.LostFocus += OnLostFocus;
    }

    // Mickeys (C64 pixels, for a driver that moves one pixel per mickey) per PC pixel
    public double Sensitivity { get; set; } = 0.5;

    public bool IsCaptured => captured;

    // Mouse movement in PC pixels (x to the right, y downwards)
    internal void MoveBy(int dx, int dy)
    {
        pendingX += dx * Sensitivity;
        pendingY += dy * Sensitivity;
        int mickeysX = (int)Math.Truncate(pendingX), mickeysY = (int)Math.Truncate(pendingY);
        pendingX -= mickeysX;
        pendingY -= mickeysY;
        if (mickeysX != 0 || mickeysY != 0) mouse.Move(mickeysX, -mickeysY);
    }

    void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (!captured)
        {
            if (e.Button == MouseButtons.Left) Capture(); // the first click only captures
            return;
        }
        if (e.Button == MouseButtons.Middle || e.Button == MouseButtons.Right && Control.ModifierKeys.HasFlag(Keys.Control))
        {
            Release();
            return;
        }
        if (e.Button == MouseButtons.Left) mouse.LeftButton = true;
        if (e.Button == MouseButtons.Right) mouse.RightButton = true;
    }

    void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) mouse.LeftButton = false;
        if (e.Button == MouseButtons.Right) mouse.RightButton = false;
    }

    void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (!captured) return;
        var screen = source.PointToScreen(e.Location);
        int dx = screen.X - center.X, dy = screen.Y - center.Y;
        if (dx == 0 && dy == 0) return;
        MoveBy(dx, dy);
        Cursor.Position = center; // relative movement: the cursor goes back to the center
    }

    void OnLostFocus(object? sender, EventArgs e) => Release();

    void Capture()
    {
        captured = true;
        menu = source.ContextMenuStrip;
        source.ContextMenuStrip = null;
        center = source.PointToScreen(new Point(source.ClientSize.Width / 2, source.ClientSize.Height / 2));
        Cursor.Clip = source.RectangleToScreen(source.ClientRectangle);
        Cursor.Position = center;
        Cursor.Hide();
    }

    void Release()
    {
        mouse.LeftButton = false;
        mouse.RightButton = false;
        if (!captured) return;
        captured = false;
        source.ContextMenuStrip ??= menu;
        menu = null;
        Cursor.Clip = Rectangle.Empty;
        Cursor.Show();
    }

    public void Dispose()
    {
        Release();
        source.MouseDown -= OnMouseDown;
        source.MouseUp -= OnMouseUp;
        source.MouseMove -= OnMouseMove;
        source.LostFocus -= OnLostFocus;
    }
}
