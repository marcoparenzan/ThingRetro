using Thing64.WinForms;
using Thing64.C64;

namespace Thing64.Computer;

// A C64: a window hosting a MonitorControl and wiring the pieces together. It works on its own or inside another
// form (MDI child). The same wiring (Board, monitor, MonitorCable, PcKeyboard, PcSpeakers with AudioCable,
// ControlPortSelector) works in any form, next to other controls too.
// Right click on the monitor: control ports, port swap, audio, reset
public class ComputerForm : Form
{
    readonly Board board;
    readonly MonitorControl monitor = new() { Dock = DockStyle.Fill };
    readonly MonitorCable monitorCable;
    readonly PcKeyboard keyboardCable;
    readonly PcSpeakers? speakers;
    readonly AudioCable? audioCable;
    readonly ControlPortSelector controlPorts;
    readonly AppSettings settings = AppSettings.Current;
    Drive1541Host? drive1541;
    DriveForm? driveForm;

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
        menu.Items.Add(new ToolStripMenuItem("1541 drive (device 8)...", null, (_, _) => ShowDrive()));
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
        if (MediaLoader.IsD64(path))
        {
            OpenD64(path);
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

    // The 1541 on the IEC cable (device 8). It is plugged in the first time it is asked for
    bool EnsureDrive()
    {
        if (drive1541 is not null) return true;
        string? rom = Drive1541Host.FindRom();
        if (rom is null)
        {
            MessageBox.Show(this,
                $"The 1541 ROM ({Drive1541Host.RomName}, 16 KB) was not found in {RomFolder.DefaultPath}.\n\n" +
                "Copy it from the Shared\\rom folder of C64 Forever Free Express Edition and start again.",
                "Thing64", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }
        drive1541 = new Drive1541Host(board, File.ReadAllBytes(rom), 8);
        return true;
    }

    void ShowDrive()
    {
        if (!EnsureDrive()) return;
        if (driveForm is null || driveForm.IsDisposed)
        {
            driveForm = new DriveForm(drive1541!, BrowseD64, SaveD64As) { Owner = this };
            driveForm.Location = new Point(Right + 8, Top);
        }
        driveForm.Show();
        driveForm.BringToFront();
    }

    void BrowseD64()
    {
        using var dialog = new OpenFileDialog { Filter = "1541 disks (*.d64)|*.d64|All files|*.*" };
        if (dialog.ShowDialog(this) == DialogResult.OK) OpenD64(dialog.FileName);
    }

    void OpenD64(string path)
    {
        try
        {
            var image = new ThingRetroEmu.Media.D64Image(File.ReadAllBytes(path));
            if (!EnsureDrive()) return;
            drive1541!.Insert(image, path);
            ShowDrive();
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, e.Message, $"Thing64 - {Path.GetFileName(path)}", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    void SaveD64As()
    {
        using var dialog = new SaveFileDialog { Filter = "1541 disk (*.d64)|*.d64", FileName = "disk.d64" };
        if (dialog.ShowDialog(this) != DialogResult.OK || drive1541 is null) return;
        try
        {
            drive1541.SaveAs(dialog.FileName);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            MessageBox.Show(this, e.Message, "Thing64", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
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
            drive1541?.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"The disk could not be saved: {ex.Message}", "Thing64", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        controlPorts.Dispose();
        keyboardCable.Dispose();
        audioCable?.Dispose();
        speakers?.Dispose();
        monitorCable.Dispose();
        base.OnFormClosed(e);
    }
}
