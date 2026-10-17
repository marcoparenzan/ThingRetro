using Thing65.C65;

namespace Thing65.WinForms;

// Simulator: the PC mouse turns the knob of paddle A of a pair of Paddles.
// A left click on the control captures the mouse (cursor hidden and confined); horizontal movement turns
// the knob, the left button is the paddle's button. A right click (which opens the menu) or losing the
// focus releases the mouse.
// Direction: moving right turns clockwise, and on the C64 paddles the value read decreases
public sealed class MousePaddles : IDisposable
{
    readonly Control source;
    readonly Paddles paddles;
    double position;
    bool captured;
    Point center;

    public MousePaddles(Control source, Paddles paddles)
    {
        this.source = source;
        this.paddles = paddles;
        position = paddles.PositionA;
        source.MouseDown += OnMouseDown;
        source.MouseUp += OnMouseUp;
        source.MouseMove += OnMouseMove;
        source.LostFocus += OnLostFocus;
    }

    // Paddle units per pixel of mouse movement
    public double Sensitivity { get; set; } = 1.0;

    public bool IsCaptured => captured;

    // Horizontal mouse movement in pixels (positive = to the right)
    internal void MoveBy(int dx)
    {
        position = Math.Clamp(position - dx * Sensitivity, 0, 255);
        paddles.PositionA = (byte)Math.Round(position);
    }

    void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right) { Release(); return; }
        if (e.Button != MouseButtons.Left) return;
        if (!captured) { Capture(); return; } // the first click only captures
        paddles.FireA = true;
    }

    void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) paddles.FireA = false;
    }

    void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (!captured) return;
        var screen = source.PointToScreen(e.Location);
        int dx = screen.X - center.X;
        if (dx == 0) return;
        MoveBy(dx);
        Cursor.Position = center; // relative movement: the cursor goes back to the center
    }

    void OnLostFocus(object? sender, EventArgs e) => Release();

    void Capture()
    {
        captured = true;
        center = source.PointToScreen(new Point(source.ClientSize.Width / 2, source.ClientSize.Height / 2));
        Cursor.Clip = source.RectangleToScreen(source.ClientRectangle);
        Cursor.Position = center;
        Cursor.Hide();
    }

    void Release()
    {
        paddles.FireA = false;
        if (!captured) return;
        captured = false;
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
