using Thing64.WinForms;
using Thing64.C64;
using ThingCommodore.Hosting;
using ThingCommodore.WinForms;

namespace Thing64.Computer;

// A C64: a window hosting a MonitorControl and wiring the pieces together. It works on its own or inside another
// form (MDI child). The same wiring (Board, monitor, MonitorCable, PcKeyboard, PcSpeakers with AudioCable,
// ControlPortSelector) works in any form, next to other controls too.
// Right click on the monitor: control ports, port swap, audio, reset
public class ComputerForm : Form
{
    readonly Board board;
    readonly MonitorControl monitor = new() { Dock = DockStyle.Fill };
    readonly VideoMenu video;
    readonly PcKeyboard keyboardCable;
    readonly PcSpeakers? speakers;
    readonly AudioCable? audioCable;
    readonly ControlPortSelector controlPorts;
    readonly AppSettings settings = AppSettings.Current;
    readonly PeripheralRack rack;
    readonly PeripheralMenus peripherals;

    public ComputerForm(SystemRoms roms, string? startupFile = null)
    {
        board = new Board(roms);
        Text = "C64";
        Icon = AppIcon.Value;
        StartPosition = FormStartPosition.CenterScreen;
        Controls.Add(monitor);
        video = new VideoMenu(board.VideoOut, monitor);

        // The peripherals on the cables: disk drives and printer on the IEC cable, the recorder on the cassette connector
        rack = new PeripheralRack(board.Iec, board.Cassette, board.Enqueue, cycle => board.AfterCycle = cycle,
            name => File.Exists(Path.Combine(RomFolder.DefaultPath, name)) ? File.ReadAllBytes(Path.Combine(RomFolder.DefaultPath, name)) : null,
            roms.Chargen);
        peripherals = new PeripheralMenus(rack, this, "Thing64", RomFolder.DefaultPath);
        var menuBar = new MenuStrip();
        var peripheralMenu = new ToolStripMenuItem("Peripherals");
        peripherals.AddTo(peripheralMenu.DropDownItems);
        menuBar.Items.Add(peripheralMenu);
        menuBar.Items.Add(video.CreateItem());
        MainMenuStrip = menuBar;
        Controls.Add(menuBar);
        ClientSize = new Size(monitor.Width, monitor.Height + menuBar.PreferredSize.Height);

        // The explicit connections: video output -> monitor, PC keyboard (on the monitor) -> C64 keyboard
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
        controlPorts = new ControlPortSelector(board.ControlPort1, board.ControlPort2, monitor);
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
        peripherals.AddTo(menu.Items);
        menu.Items.Add(video.CreateItem());
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
        if (PeripheralMenus.IsPeripheralFile(path))
        {
            peripherals.Open(path);
            return;
        }
        try
        {
            Text = $"C64 - {load()}";
        }
        catch (Exception e) when (e is IOException or InvalidDataException or NotSupportedException or ArgumentException)
        {
            MessageBox.Show(this, e.Message, $"Thing64 - {Path.GetFileName(path)}", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        try
        {
            rack.SaveAll();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"A disk or tape could not be saved: {ex.Message}", "Thing64", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        controlPorts.Dispose();
        keyboardCable.Dispose();
        audioCable?.Dispose();
        speakers?.Dispose();
        video.Dispose();
        base.OnFormClosed(e);
    }
}
