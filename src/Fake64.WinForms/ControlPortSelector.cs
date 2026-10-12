using Fake64.C64;

namespace Fake64.WinForms;

public enum ControlPortSource
{
    None,
    KeypadJoystick,    // joystick dal tastierino numerico
    ArrowsJoystick,    // joystick da frecce + Ctrl
    UsbJoystick,       // joystick o gamepad USB
    MousePaddles,      // paddle dal mouse del PC
}

// Cosa è collegato a una porta; per il joystick USB anche quale dispositivo
public readonly record struct ControlPortChoice(ControlPortSource Source, int UsbJoystickId = 0)
{
    public static ControlPortChoice None => new(ControlPortSource.None);
}

// Decide cosa è collegato alle due porte di controllo di una Board: crea il dispositivo (Joystick, Paddles),
// lo collega alla presa e lo affida al simulatore che lo comanda (tastiera, joystick USB, mouse).
// Ogni origine può essere in una sola porta: sceglierla per una porta la toglie dall'altra.
// AddMenuItems mette le scelte in un menu (di solito il menu contestuale del monitor)
public sealed class ControlPortSelector : IDisposable
{
    readonly Board board;
    readonly Control input;
    readonly Connection[] ports = [new(), new()];

    sealed class Connection
    {
        public ControlPortChoice Choice = ControlPortChoice.None;
        public IDisposable? Simulator;
    }

    // input: il controllo che riceve tastiera e mouse (di solito il C64Monitor)
    public ControlPortSelector(Board board, Control input)
    {
        this.board = board;
        this.input = input;
    }

    public event Action? Changed;

    public ControlPortChoice this[int port] => Port(port).Choice;

    // Tasti del PC usati da un joystick da tastiera: vanno esclusi dalla tastiera del C64
    public bool UsesKey(Keys key) =>
        ports.Any(connection => connection.Simulator is KeyboardJoystick keyboard && keyboard.UsesKey(key));

    public void Select(int port, ControlPortChoice choice)
    {
        int other = port == 1 ? 2 : 1;
        if (choice.Source != ControlPortSource.None && Port(other).Choice == choice)
            Connect(other, ControlPortChoice.None);
        Connect(port, choice);
        Changed?.Invoke();
    }

    public void SwapPorts()
    {
        var (first, second) = (Port(1).Choice, Port(2).Choice);
        Connect(1, ControlPortChoice.None);
        Connect(2, ControlPortChoice.None);
        Connect(1, second);
        Connect(2, first);
        Changed?.Invoke();
    }

    Connection Port(int port) => port is 1 or 2 ? ports[port - 1] : throw new ArgumentOutOfRangeException(nameof(port));

    ControlPort Socket(int port) => port == 1 ? board.ControlPort1 : board.ControlPort2;

    void Connect(int port, ControlPortChoice choice)
    {
        var connection = Port(port);
        connection.Simulator?.Dispose();
        connection.Simulator = null;
        Socket(port).Unplug();

        connection.Choice = choice;
        switch (choice.Source)
        {
            case ControlPortSource.KeypadJoystick:
            case ControlPortSource.ArrowsJoystick:
                {
                    var joystick = new Joystick();
                    var keySet = choice.Source == ControlPortSource.KeypadJoystick ? JoystickKeySet.NumericKeypad : JoystickKeySet.ArrowsAndCtrl;
                    connection.Simulator = new KeyboardJoystick(input, joystick, keySet);
                    Socket(port).Plug(joystick);
                    break;
                }
            case ControlPortSource.UsbJoystick:
                {
                    var device = UsbJoystick.Connected().FirstOrDefault(d => d.Id == choice.UsbJoystickId);
                    if (device is null) { connection.Choice = ControlPortChoice.None; break; } // non più collegato
                    var joystick = new Joystick();
                    connection.Simulator = new UsbJoystick(device, joystick);
                    Socket(port).Plug(joystick);
                    break;
                }
            case ControlPortSource.MousePaddles:
                {
                    var paddles = new Paddles();
                    connection.Simulator = new MousePaddles(input, paddles);
                    Socket(port).Plug(paddles);
                    break;
                }
        }
    }

    // Voci "Porta di controllo 1/2" (con le scelte, aggiornate a ogni apertura) e "Scambia le porte" (Ctrl+F12)
    public void AddMenuItems(ToolStripItemCollection items)
    {
        for (int port = 1; port <= 2; port++)
        {
            var menu = new ToolStripMenuItem($"Porta di controllo {port}");
            int current = port;
            menu.DropDownItems.Add(new ToolStripMenuItem("(aggiornamento)"));
            menu.DropDownOpening += (_, _) => FillPortMenu(menu, current);
            items.Add(menu);
        }
        items.Add(new ToolStripMenuItem("Scambia le porte", null, (_, _) => SwapPorts()) { ShortcutKeys = Keys.Control | Keys.F12 });
    }

    internal void FillPortMenu(ToolStripMenuItem menu, int port)
    {
        menu.DropDownItems.Clear();
        void Add(string text, ControlPortChoice choice) =>
            menu.DropDownItems.Add(new ToolStripMenuItem(text, null, (_, _) => Select(port, choice)) { Checked = this[port] == choice });

        Add("Niente", ControlPortChoice.None);
        Add("Joystick: tastierino numerico (8 2 4 6, fuoco 0 o 5)", new(ControlPortSource.KeypadJoystick));
        Add("Joystick: frecce, fuoco Ctrl", new(ControlPortSource.ArrowsJoystick));
        var usb = UsbJoystick.Connected();
        if (usb.Count == 0)
            menu.DropDownItems.Add(new ToolStripMenuItem("Joystick USB: nessuno collegato") { Enabled = false });
        foreach (var device in usb)
            Add($"Joystick USB: {device.Name}", new(ControlPortSource.UsbJoystick, device.Id));
        Add("Paddle: mouse (clic per catturarlo, clic destro per liberarlo)", new(ControlPortSource.MousePaddles));
    }

    public void Dispose()
    {
        for (int port = 1; port <= 2; port++) Connect(port, ControlPortChoice.None);
    }
}
