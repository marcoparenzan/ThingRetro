using ThingRetroEmu.Ports;
using ThingRetroEmu.Audio;

namespace ThingRetroEmu.WinForms;

public enum ControlPortSource
{
    None,
    KeypadJoystick,    // joystick from the numeric keypad
    ArrowsJoystick,    // joystick from arrows + Ctrl
    UsbJoystick,       // USB joystick or gamepad
    MousePaddles,      // paddles from the PC mouse
    Mouse1351,         // 1351 mouse (proportional mode) from the PC mouse
}

// What is connected to a port; for the USB joystick also which device
public readonly record struct ControlPortChoice(ControlPortSource Source, int UsbJoystickId = 0)
{
    public static ControlPortChoice None => new(ControlPortSource.None);
}

// Decides what is connected to the two control ports of a machine: creates the device (Joystick, Paddles),
// plugs it into the socket and hands it to the simulator that drives it (keyboard, USB joystick, mouse).
// Each source can be in one port only: choosing it for a port removes it from the other.
// AddMenuItems puts the choices into a menu (usually the monitor's context menu)
public sealed class ControlPortSelector : IDisposable
{
    readonly ControlPort port1, port2;
    readonly Control input;
    readonly bool allowAnalogDevices;
    readonly bool twoButtons;
    readonly Connection[] ports = [new(), new()];

    sealed class Connection
    {
        public ControlPortChoice Choice = ControlPortChoice.None;
        public IDisposable? Simulator;
    }

    // input: the control that receives keyboard and mouse (usually the MonitorControl)
    public ControlPortSelector(ControlPort port1, ControlPort port2, Control input, bool allowAnalogDevices = true, bool twoButtons = false)
    {
        this.port1 = port1;
        this.port2 = port2;
        this.input = input;
        this.allowAnalogDevices = allowAnalogDevices;
        this.twoButtons = twoButtons;
    }

    public event Action? Changed;

    public ControlPortChoice this[int port] => Port(port).Choice;

    // PC keys used by a keyboard joystick: they must be kept away from the machine keyboard
    public bool UsesKey(Keys key) =>
        ports.Any(connection => connection.Simulator is KeyboardJoystick keyboard && keyboard.UsesKey(key));

    public void Select(int port, ControlPortChoice choice)
    {
        if (!allowAnalogDevices && choice.Source is ControlPortSource.MousePaddles or ControlPortSource.Mouse1351)
            throw new NotSupportedException("This machine only supports digital joystick sources.");
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

    ControlPort Socket(int port) => port == 1 ? port1 : port2;

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
                    connection.Simulator = new KeyboardJoystick(input, joystick, keySet, twoButtons);
                    Socket(port).Plug(joystick);
                    break;
                }
            case ControlPortSource.UsbJoystick:
                {
                    var device = UsbJoystick.Connected().FirstOrDefault(d => d.Id == choice.UsbJoystickId);
                    if (device is null) { connection.Choice = ControlPortChoice.None; break; } // no longer connected
                    var joystick = new Joystick();
                    connection.Simulator = new UsbJoystick(device, joystick, twoButtons);
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
            case ControlPortSource.Mouse1351:
                {
                    var mouse = new Mouse1351();
                    connection.Simulator = new Mouse1351Simulator(input, mouse);
                    Socket(port).Plug(mouse);
                    break;
                }
        }
    }

    // "Control port 1/2" items (with the choices, refreshed at every opening) and "Swap ports" (Ctrl+F12)
    public void AddMenuItems(ToolStripItemCollection items)
    {
        for (int port = 1; port <= 2; port++)
        {
            var menu = new ToolStripMenuItem($"Control port {port}");
            int current = port;
            menu.DropDownItems.Add(new ToolStripMenuItem("(updating)"));
            menu.DropDownOpening += (_, _) => FillPortMenu(menu, current);
            items.Add(menu);
        }
        items.Add(new ToolStripMenuItem("Swap ports", null, (_, _) => SwapPorts()) { ShortcutKeys = Keys.Control | Keys.F12 });
    }

    internal void FillPortMenu(ToolStripMenuItem menu, int port)
    {
        menu.DropDownItems.Clear();
        void Add(string text, ControlPortChoice choice) =>
            menu.DropDownItems.Add(new ToolStripMenuItem(text, null, (_, _) => Select(port, choice)) { Checked = this[port] == choice });

        Add("Nothing", ControlPortChoice.None);
        Add("Joystick: numeric keypad (8 2 4 6, fire 0 or 5)", new(ControlPortSource.KeypadJoystick));
        Add("Joystick: arrows, fire Ctrl", new(ControlPortSource.ArrowsJoystick));
        var usb = UsbJoystick.Connected();
        if (usb.Count == 0)
            menu.DropDownItems.Add(new ToolStripMenuItem("USB joystick: none connected") { Enabled = false });
        foreach (var device in usb)
            Add($"USB joystick: {device.Name}", new(ControlPortSource.UsbJoystick, device.Id));
        if (!allowAnalogDevices) return;
        Add("Paddles: mouse (click to capture it, right click to release it)", new(ControlPortSource.MousePaddles));
        Add("Mouse 1351: mouse (click to capture it, middle click or Ctrl + right click to release it)", new(ControlPortSource.Mouse1351));
    }

    public void Dispose()
    {
        for (int port = 1; port <= 2; port++) Connect(port, ControlPortChoice.None);
    }
}
