using Thing65.WinForms;
using Thing65.C65;
using Thing65.Mos;

namespace Thing65.Computer;

// A C65: a window hosting a C65Monitor and wiring the pieces together. It works on its own or inside another
// form (MDI child). The same wiring (BoardC65, monitor, MonitorCable, PcKeyboard, PcSpeakers with AudioCable,
// ControlPortSelector) works in any form, next to other controls too.
// Right click on the monitor: control ports, audio, disk, reset
public class ComputerForm : Form
{
    readonly BoardC65 board;
    readonly C65Monitor monitor = new() { Dock = DockStyle.Fill };
    readonly MonitorCable monitorCable;
    readonly PcKeyboard keyboardCable;
    readonly PcSpeakers? speakers;
    readonly AudioCable? audioCable;
    readonly ControlPortSelector controlPorts;
    readonly AppSettings settings = AppSettings.Current;
    readonly DiskSlot disk = new();
    Drive1541Host? drive1541;
    DriveForm? driveForm;

    public ComputerForm(SystemRom128 rom, string? startupFile = null)
    {
        board = new BoardC65(rom);
        Text = "C65";
        Icon = AppIcon.Value;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = monitor.Size;
        Controls.Add(monitor);

        // The explicit connections: video output -> monitor, PC keyboard (on the monitor) -> C65 keyboard
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
        // do not reach the C65 keyboard
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
        menu.Items.Add(new ToolStripMenuItem("Insert disk or run program...", null, (_, _) => Browse()));
        menu.Items.Add(new ToolStripMenuItem("Save disk", null, (_, _) => board.Enqueue(() => disk.Save())));
        menu.Items.Add(new ToolStripMenuItem("Save disk as...", null, (_, _) => SaveDiskAs()));
        menu.Items.Add(new ToolStripMenuItem("Eject disk", null, (_, _) => board.Enqueue(() => { disk.Set(null, null); board.Disk = null; })));
        var expansion = new ToolStripMenuItem("RAM expansion (1 MB)") { CheckOnClick = true };
        expansion.CheckedChanged += (_, _) => SetRamExpansion(expansion.Checked);
        menu.Items.Add(expansion);
        menu.Items.Add(new ToolStripMenuItem("Insert cartridge...", null, (_, _) => InsertCartridge()));
        menu.Items.Add(new ToolStripMenuItem("Remove cartridge", null, (_, _) => board.Enqueue(() => { board.CartridgeSocket.Remove(); board.Reset(); })));
        menu.Items.Add(new ToolStripMenuItem("1541 drive (device 9)...", null, (_, _) => ShowDrive()));
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

        // As a tab: whenever the tab comes to the front the keyboard goes to the C65
        Activated += (_, _) => monitor.Focus();
    }

    public void Reset() => board.Enqueue(board.Reset);

    // Cards and cartridges are changed with the machine off, as on the real one: the board is reset
    void SetRamExpansion(bool fitted) => board.Enqueue(() =>
    {
        board.RamExpansionSocket.Remove();
        if (fitted) board.RamExpansionSocket.Insert(new RamExpansion());
        board.Reset();
    });

    void InsertCartridge()
    {
        using var dialog = new OpenFileDialog { Filter = "ROM cartridge (*.bin;*.rom)|*.bin;*.rom|All files|*.*" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var cartridge = new RomCartridge(File.ReadAllBytes(dialog.FileName));
            board.Enqueue(() =>
            {
                board.CartridgeSocket.Remove();
                board.CartridgeSocket.Insert(cartridge);
                board.Reset();
            });
        }
        catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, e.Message, "Thing65", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // The external 1541 on the IEC cable (device 9: 8 is the C65's own drive). It is plugged in the first time it is asked for
    bool EnsureDrive()
    {
        if (drive1541 is not null) return true;
        string? rom = Drive1541Host.FindRom();
        if (rom is null)
        {
            MessageBox.Show(this,
                $"The 1541 ROM ({Drive1541Host.RomName}, 16 KB) was not found in {RomFolder.DefaultPath}.\n\n" +
                "Copy it from the Shared\\rom folder of C64 Forever Free Express Edition and start again.",
                "Thing65", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }
        drive1541 = new Drive1541Host(board, File.ReadAllBytes(rom), 9);
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
            MessageBox.Show(this, e.Message, $"Thing65 - {Path.GetFileName(path)}", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            MessageBox.Show(this, e.Message, "Thing65", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // The disk is copied on the emulation thread, between two cycles, so the DOS is not writing while it is saved
    void SaveDiskAs()
    {
        using var dialog = new SaveFileDialog { Filter = "C65 disk (*.d81)|*.d81", FileName = "disk.d81" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        string path = dialog.FileName;
        board.Enqueue(() =>
        {
            try { disk.SaveAs(path); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                BeginInvoke(() => MessageBox.Show(this, e.Message, "Thing65", MessageBoxButtons.OK, MessageBoxIcon.Error));
            }
        });
    }

    void Browse()
    {
        using var dialog = new OpenFileDialog { Filter = "C65 disks and programs (*.d81;*.prg)|*.d81;*.prg|All files|*.*" };
        if (dialog.ShowDialog(this) == DialogResult.OK) Open(dialog.FileName);
    }

    // Restarts the machine and runs a .prg (or boots a .d81)
    public void Run(string path) => LoadFile(path, () => MediaLoader.Run(board, path, disk));

    // Puts a disk in the running machine, or starts a program, as when it is dropped on the monitor
    public void Open(string path) => LoadFile(path, () => MediaLoader.Open(board, path, disk));

    void LoadFile(string path, Func<string> load)
    {
        if (MediaLoader.IsD64(path))
        {
            OpenD64(path);
            return;
        }
        try
        {
            Text = $"C65 - {load()}";
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
        try
        {
            disk.Save();
            drive1541?.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"The disk could not be saved: {ex.Message}", "Thing65", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        controlPorts.Dispose();
        keyboardCable.Dispose();
        audioCable?.Dispose();
        speakers?.Dispose();
        monitorCable.Dispose();
        base.OnFormClosed(e);
    }
}
