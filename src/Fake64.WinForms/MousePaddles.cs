using Fake64.C64;

namespace Fake64.WinForms;

// Simulatore: il mouse del PC gira la manopola del paddle A di una coppia di Paddles.
// Un clic sinistro sul controllo cattura il mouse (cursore nascosto e confinato); il movimento orizzontale
// gira la manopola, il pulsante sinistro è il pulsante del paddle. Il clic destro (che apre il menu)
// o la perdita del focus liberano il mouse.
// Verso: muovere verso destra gira in senso orario, e sui paddle del C64 il valore letto diminuisce
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

    // Unità del paddle per pixel di movimento del mouse
    public double Sensitivity { get; set; } = 1.0;

    public bool IsCaptured => captured;

    // Movimento orizzontale del mouse in pixel (positivo = verso destra)
    internal void MoveBy(int dx)
    {
        position = Math.Clamp(position - dx * Sensitivity, 0, 255);
        paddles.PositionA = (byte)Math.Round(position);
    }

    void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right) { Release(); return; }
        if (e.Button != MouseButtons.Left) return;
        if (!captured) { Capture(); return; } // il primo clic cattura soltanto
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
        Cursor.Position = center; // movimento relativo: il cursore torna al centro
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
