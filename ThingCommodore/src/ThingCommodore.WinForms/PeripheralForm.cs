using ThingCommodore.Hosting;

namespace ThingCommodore.WinForms;

/// <summary>A window that shows what the peripherals are doing: the LED and the head of the 1541, the 1581, the recorder and the printer.</summary>
public sealed class PeripheralForm : Form
{
    readonly PeripheralRack rack;
    readonly Panel led = new() { Width = 16, Height = 16, BackColor = Color.DarkRed };
    readonly Label drive1541 = new() { AutoSize = true };
    readonly Label drive1581 = new() { AutoSize = true };
    readonly Label tape = new() { AutoSize = true };
    readonly Label printer = new() { AutoSize = true };
    readonly System.Windows.Forms.Timer refresh = new() { Interval = 100 };

    public PeripheralForm(PeripheralRack rack, string title)
    {
        this.rack = rack;
        Text = $"{title} - peripherals";
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        ClientSize = new Size(380, 130);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(10) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 26));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = "1541", AutoSize = true }, 0, 0);
        layout.Controls.Add(led, 1, 0);
        layout.Controls.Add(drive1541, 2, 0);
        layout.Controls.Add(new Label { Text = "1581", AutoSize = true }, 0, 1);
        layout.Controls.Add(drive1581, 2, 1);
        layout.Controls.Add(new Label { Text = "Tape", AutoSize = true }, 0, 2);
        layout.Controls.Add(tape, 2, 2);
        layout.Controls.Add(new Label { Text = "Printer", AutoSize = true }, 0, 3);
        layout.Controls.Add(printer, 2, 3);
        Controls.Add(layout);

        refresh.Tick += (_, _) => UpdateState();
        refresh.Start();
        UpdateState();
    }

    // The values are read from another thread without a lock: they are single fields that only change one at a time
    void UpdateState()
    {
        var d64 = rack.Disk1541;
        var mechanism = d64.Mechanism;
        led.BackColor = mechanism?.Led == true ? Color.Red : Color.DarkRed;
        drive1541.Text = d64.Device == 0 ? "not connected" : $"device {d64.Device}, {Mechanism(mechanism)}, {d64.DiskName}{(d64.DiskModified ? " *" : "")}";
        var d81 = rack.Disk1581;
        drive1581.Text = d81.Device == 0 ? "not connected" : $"device {d81.Device}, {d81.DiskName}{(d81.DiskModified ? " *" : "")}";
        if (!rack.HasCassette) tape.Text = "no cassette port";
        else if (rack.Tape.Tape is null) tape.Text = "no tape";
        else
        {
            int seconds = (int)rack.Tape.Seconds;
            tape.Text = $"{(rack.TapePath is null ? "(not saved)" : Path.GetFileName(rack.TapePath))}: {rack.Tape.Key}, {seconds / 60}:{seconds % 60:D2}{(rack.TapeModified ? " *" : "")}";
        }
        printer.Text = rack.PrinterDevice == 0 ? "not connected" : $"device {rack.PrinterDevice}, {rack.PrinterSheets} sheet(s)";
    }

    static string Mechanism(ThingCommodore.Drives.Mechanism1541? mechanism)
    {
        if (mechanism is null) return "";
        int half = mechanism.HalfTrack;
        return $"track {half / 2 + 1}{(half % 2 == 1 ? " + 1/2" : "")}, motor {(mechanism.Writing ? "writing" : mechanism.Motor ? "on" : "off")}";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) refresh.Dispose();
        base.Dispose(disposing);
    }
}
