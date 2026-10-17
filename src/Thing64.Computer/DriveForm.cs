namespace Thing64.Computer;

// The window of the 1541: the LED, the track under the head, the motor and the disk in it
public sealed class DriveForm : Form
{
    readonly Drive1541Host drive;
    readonly Panel led = new() { Width = 22, Height = 22, BackColor = Color.DarkRed };
    readonly Label track = new() { AutoSize = true };
    readonly Label motor = new() { AutoSize = true };
    readonly Label disk = new() { AutoSize = true };
    readonly System.Windows.Forms.Timer refresh = new() { Interval = 100 };

    public DriveForm(Drive1541Host drive, Action insertRequested, Action saveAsRequested)
    {
        this.drive = drive;
        Text = $"1541 - device {drive.DeviceNumber}";
        Icon = AppIcon.Value;
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        ClientSize = new Size(300, 120);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(10), AutoSize = false };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(led, 0, 0);
        layout.Controls.Add(track, 1, 0);
        layout.Controls.Add(new Label { Text = "Motor", AutoSize = true }, 0, 1);
        layout.Controls.Add(motor, 1, 1);
        layout.Controls.Add(new Label { Text = "Disk", AutoSize = true }, 0, 2);
        layout.Controls.Add(disk, 1, 2);
        Controls.Add(layout);

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Insert disk...", null, (_, _) => insertRequested()));
        menu.Items.Add(new ToolStripMenuItem("Save disk", null, (_, _) => drive.RequestSave()));
        menu.Items.Add(new ToolStripMenuItem("Save disk as...", null, (_, _) => saveAsRequested()));
        menu.Items.Add(new ToolStripMenuItem("Eject disk", null, (_, _) => drive.Eject()));
        ContextMenuStrip = menu;
        layout.ContextMenuStrip = menu;

        refresh.Tick += (_, _) => UpdateState();
        refresh.Start();
        UpdateState();
    }

    // The values are read from another thread without a lock: they are single fields that only change one at a time
    void UpdateState()
    {
        var mechanism = drive.Mechanism;
        led.BackColor = mechanism.Led ? Color.Red : Color.DarkRed;
        int half = mechanism.HalfTrack;
        track.Text = $"Track {half / 2 + 1}{(half % 2 == 1 ? " + 1/2" : "")}";
        motor.Text = mechanism.Writing ? "writing" : mechanism.Motor ? "on" : "off";
        disk.Text = drive.Image is null ? "none" : (drive.Path is null ? "(not saved)" : System.IO.Path.GetFileName(drive.Path)) + (drive.Modified ? " *" : "");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) refresh.Dispose();
        base.Dispose(disposing);
    }
}
