using Thing65.WinForms;
using Thing65.C65;

namespace Thing65.Computer;

// A C64: a window hosting a C65Monitor and wiring the pieces together. It works on its own or inside another
// form (MDI child). The same wiring (Board, monitor, MonitorCable, PcKeyboard, PcSpeakers with AudioCable,
// ControlPortSelector) works in any form, next to other controls too.
// Right click on the monitor: control ports, port swap, audio, reset
public class ComputerForm : Form
{
    readonly Board board;
    readonly C65Monitor monitor = new() { Dock = DockStyle.Fill };
    readonly MonitorCable monitorCable;
    readonly PcKeyboard keyboardCable;
    readonly PcSpeakers? speakers;
    readonly AudioCable? audioCable;
    readonly ControlPortSelector controlPorts;
    readonly AppSettings settings = AppSettings.Current;

    public ComputerForm(SystemRoms roms, string? startupFile = null)
    {
        board = new Board(roms);
        Text = "C64";
        Icon = AppIcon.Value;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = monitor.Size;
        Controls.Add(monitor);

        // The explicit connections: video output -> monitor, PC keyboard (on the monitor) -> C64 keyboard
        monitorCable = new MonitorCable(board.VideoOut, monitor);
        keyboardCable = new PcKeyboard(monitor, board.Keyboard);

        // Audio output -> PC speakers. Without an audio device the emulator still works, silently
        try
        {
            speakers = new PcSpeakers { Muted = !settings.Audio };
            audioCable = new AudioCable(board.AudioOut, speakers);
        }
        catch (InvalidOperationException) { }

        // Control ports: joystick from the keyboard or USB, paddles from the mouse; the joystick keys
        // do not reach the C64 keyboard
        controlPorts = new ControlPortSelector(board, monitor);
        controlPorts.Select(1, settings.Port1);
        controlPorts.Select(2, settings.Port2);
        controlPorts.Changed += SaveControlPorts;
        keyboardCable.Excluded = controlPorts.UsesKey;

        var menu = new ContextMenuStrip();
        controlPorts.AddMenuItems(menu.Items);
        menu.Items.Add(new ToolStripSeparator());
        var audio = new ToolStripMenuItem("Audio") { CheckOnClick = true, Checked = settings.Audio, Enabled = speakers is not null };
        audio.CheckedChanged += (_, _) =>
        {
            if (speakers is not null) speakers.Muted = !audio.Checked;
            settings.Audio = audio.Checked;
            settings.Save();
        };
        menu.Items.Add(audio);
        menu.Items.Add(new ToolStripMenuItem("Reset", null, (_, _) => Reset()));
        monitor.ContextMenuStrip = menu;

        monitor.AllowDrop = true;
        monitor.DragEnter += (_, e) =>
        {
            if (DroppedFile(e) is not null) e.Effect = DragDropEffects.Copy;
        };
        monitor.DragDrop += (_, e) =>
        {
            if (DroppedFile(e) is string path) Open(path);
        };

        Shown += (_, _) =>
        {
            monitor.Focus();
            if (startupFile is not null) Run(startupFile);
        };

        // As a tab: whenever the tab comes to the front the keyboard goes to the C64
        Activated += (_, _) => monitor.Focus();
    }

    public void Reset() => board.Enqueue(board.Reset);

    // Restarts the machine and runs a .prg (or inserts a .crt)
    public void Run(string path) => LoadFile(path, () => MediaLoader.Run(board, path));

    // Loads a file into the running machine, as when it is dropped on the monitor
    public void Open(string path) => LoadFile(path, () => MediaLoader.Open(board, path));

    void LoadFile(string path, Func<string> load)
    {
        try
        {
            Text = $"C64 - {load()}";
        }
        catch (Exception e) when (e is IOException or InvalidDataException or NotSupportedException or ArgumentException)
        {
            MessageBox.Show(this, e.Message, $"Thing65 - {Path.GetFileName(path)}", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        monitor.Focus();
    }

    static string? DroppedFile(DragEventArgs e) =>
        e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length == 1 && MediaLoader.IsSupported(files[0])
            ? files[0]
            : null;

    void SaveControlPorts()
    {
        settings.Port1 = controlPorts[1];
        settings.Port2 = controlPorts[2];
        settings.Save();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        board.Stop();
        controlPorts.Dispose();
        keyboardCable.Dispose();
        audioCable?.Dispose();
        speakers?.Dispose();
        monitorCable.Dispose();
        base.OnFormClosed(e);
    }
}
