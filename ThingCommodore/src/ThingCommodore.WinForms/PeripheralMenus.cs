using ThingCommodore.Hosting;
using ThingCommodore.Tape;

namespace ThingCommodore.WinForms;

/// <summary>
/// The Disk, Tape and Printer menus of a computer with a <see cref="PeripheralRack"/>, and the routing of dropped .d64/.d81/.tap files to the right device.
/// <c>firstDevice</c> is the lowest device number the external drives may take (9 for a computer with its own drive 8)
/// </summary>
public sealed class PeripheralMenus
{
    readonly PeripheralRack rack;
    readonly IWin32Window owner;
    readonly string title;
    readonly int firstDevice;
    readonly string romFolder;
    PeripheralForm? window;

    public PeripheralMenus(PeripheralRack rack, IWin32Window owner, string title, string romFolder, int firstDevice = 8)
    {
        this.rack = rack;
        this.owner = owner;
        this.title = title;
        this.romFolder = romFolder;
        this.firstDevice = firstDevice;
    }

    /// <summary>Adds the Disk, Tape (if the computer has a cassette connector) and Printer items to a menu.</summary>
    public void AddTo(ToolStripItemCollection items)
    {
        items.Add(DriveMenu("1541 drive", rack.Disk1541, "1541 disks (*.d64)|*.d64|All files|*.*", ".d64"));
        items.Add(DriveMenu("1581 drive", rack.Disk1581, "1581 disks (*.d81)|*.d81|All files|*.*", ".d81"));
        if (rack.HasCassette) items.Add(TapeMenu());
        items.Add(PrinterMenu());
        items.Add(new ToolStripMenuItem("Peripherals window", null, (_, _) => ShowWindow()));
    }

    /// <summary>True for the files these menus open: disks (.d64, .d81) and tapes (.tap).</summary>
    public static bool IsPeripheralFile(string path) => Path.GetExtension(path).ToLowerInvariant() is ".d64" or ".d81" or (".tap");

    /// <summary>Inserts a dropped disk or tape; the drive is plugged in on the way if it was not. False if the file is not for these menus.</summary>
    public bool Open(string path)
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".d64": InsertDisk(rack.Disk1541, path); return true;
            case ".d81": InsertDisk(rack.Disk1581, path); return true;
            case ".tap" when rack.HasCassette: Guard(() => rack.InsertTape(path), path); return true;
            default: return false;
        }
    }

    public void ShowWindow()
    {
        if (window is null || window.IsDisposed) window = new PeripheralForm(rack, title);
        window.Show(owner);
        window.BringToFront();
    }

    // ---- disks ------------------------------------------------------------------------------------------------------------

    ToolStripMenuItem DriveMenu<T>(string name, DriveSlot<T> slot, string filter, string extension) where T : class
    {
        var menu = new ToolStripMenuItem(name);
        var devices = new ToolStripMenuItem("Device");
        for (int device = firstDevice; device <= 11; device++)
        {
            int number = device;
            var item = new ToolStripMenuItem($"{number}") { Tag = number };
            item.Click += (_, _) => Plug(slot, number);
            devices.DropDownItems.Add(item);
        }
        var unplug = new ToolStripMenuItem("Not connected", null, (_, _) => slot.Detach());
        devices.DropDownItems.Add(unplug);
        var insert = new ToolStripMenuItem("Insert disk...", null, (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = filter };
            if (dialog.ShowDialog(owner) == DialogResult.OK) InsertDisk(slot, dialog.FileName);
        });
        var save = new ToolStripMenuItem("Save disk", null, (_, _) => slot.Save());
        var saveAs = new ToolStripMenuItem("Save disk as...", null, (_, _) =>
        {
            using var dialog = new SaveFileDialog { Filter = filter.Split('|')[0] + "|" + filter.Split('|')[1], FileName = "disk" + extension };
            if (dialog.ShowDialog(owner) == DialogResult.OK) slot.SaveAs(dialog.FileName, Report);
        });
        var eject = new ToolStripMenuItem("Eject disk", null, (_, _) => slot.Eject());
        menu.DropDownItems.AddRange([devices, new ToolStripSeparator(), insert, save, saveAs, eject]);
        menu.DropDownOpening += (_, _) =>
        {
            foreach (ToolStripItem item in devices.DropDownItems)
                if (item is ToolStripMenuItem { Tag: int number } checkable) checkable.Checked = slot.Device == number;
            unplug.Checked = slot.Device == 0;
            insert.Enabled = true;
            bool disk = slot.HasDisk;
            save.Enabled = disk && slot.Path is not null;
            saveAs.Enabled = eject.Enabled = disk;
            menu.Text = slot.Device == 0 ? name : $"{name} (device {slot.Device})";
        };
        return menu;
    }

    bool Plug<T>(DriveSlot<T> slot, int device) where T : class
    {
        if (slot.Attach(device)) return true;
        MessageBox.Show(owner,
            $"The {slot.Model} ROM ({slot.RomFileName}) was not found in {romFolder}.\n\n" +
            "Copy it from the Shared\\rom folder of C64 Forever Free Express Edition and start again.",
            title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        return false;
    }

    void InsertDisk<T>(DriveSlot<T> slot, string path) where T : class
    {
        // A drive that is not on the cable yet is plugged in as the first free device
        if (slot.Device == 0 && !Plug(slot, FirstFreeDevice(slot))) return;
        Guard(() => slot.Insert(path), path);
    }

    int FirstFreeDevice(object slot)
    {
        for (int device = firstDevice; device <= 11; device++)
            if (rack.Disk1541.Device != device && rack.Disk1581.Device != device) return device;
        return firstDevice;
    }

    // ---- tape -------------------------------------------------------------------------------------------------------------

    ToolStripMenuItem TapeMenu()
    {
        var menu = new ToolStripMenuItem("Tape");
        var keys = new (string Name, DatasetteKey Key)[]
        {
            ("Play", DatasetteKey.Play), ("Record", DatasetteKey.Record), ("Rewind", DatasetteKey.Rewind),
            ("Fast forward", DatasetteKey.FastForward), ("Stop", DatasetteKey.Stop),
        };
        var insert = new ToolStripMenuItem("Insert tape...", null, (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = "Tapes (*.tap)|*.tap|All files|*.*" };
            if (dialog.ShowDialog(owner) == DialogResult.OK) Guard(() => rack.InsertTape(dialog.FileName), dialog.FileName);
        });
        var blank = new ToolStripMenuItem("New blank tape", null, (_, _) => rack.NewTape());
        var save = new ToolStripMenuItem("Save tape", null, (_, _) => rack.SaveTape());
        var saveAs = new ToolStripMenuItem("Save tape as...", null, (_, _) =>
        {
            using var dialog = new SaveFileDialog { Filter = "Tapes (*.tap)|*.tap", FileName = "tape.tap" };
            if (dialog.ShowDialog(owner) == DialogResult.OK) rack.SaveTapeAs(dialog.FileName, Report);
        });
        var eject = new ToolStripMenuItem("Eject tape", null, (_, _) => rack.EjectTape());
        menu.DropDownItems.AddRange([insert, blank, save, saveAs, eject, new ToolStripSeparator()]);
        var keyItems = new List<(ToolStripMenuItem Item, DatasetteKey Key)>();
        foreach (var (name, key) in keys)
        {
            var item = new ToolStripMenuItem(name, null, (_, _) => rack.PressTape(key));
            keyItems.Add((item, key));
            menu.DropDownItems.Add(item);
        }
        menu.DropDownOpening += (_, _) =>
        {
            bool tape = rack.Tape.Tape is not null;
            save.Enabled = tape && rack.TapePath is not null;
            saveAs.Enabled = eject.Enabled = tape;
            foreach (var (item, key) in keyItems)
            {
                item.Enabled = tape;
                item.Checked = rack.Tape.Key == key;
            }
        };
        return menu;
    }

    // ---- printer ----------------------------------------------------------------------------------------------------------

    ToolStripMenuItem PrinterMenu()
    {
        var menu = new ToolStripMenuItem("Printer (MPS-803)");
        var off = new ToolStripMenuItem("Not connected", null, (_, _) => rack.SetPrinter(0));
        var four = new ToolStripMenuItem("Device 4", null, (_, _) => rack.SetPrinter(4));
        var five = new ToolStripMenuItem("Device 5", null, (_, _) => rack.SetPrinter(5));
        var pdf = new ToolStripMenuItem("Save printout as PDF...", null, (_, _) =>
        {
            using var dialog = new SaveFileDialog { Filter = "PDF (*.pdf)|*.pdf", FileName = "printout.pdf" };
            if (dialog.ShowDialog(owner) == DialogResult.OK) rack.SavePrinterPdf(dialog.FileName, Report);
        });
        var paper = new ToolStripMenuItem("New paper", null, (_, _) => rack.NewPaper());
        menu.DropDownItems.AddRange([off, four, five, new ToolStripSeparator(), pdf, paper]);
        menu.DropDownOpening += (_, _) =>
        {
            off.Checked = rack.PrinterDevice == 0;
            four.Checked = rack.PrinterDevice == 4;
            five.Checked = rack.PrinterDevice == 5;
            pdf.Enabled = paper.Enabled = rack.PrinterDevice != 0;
            menu.Text = rack.PrinterSheets > 0 ? $"Printer (MPS-803, {rack.PrinterSheets} sheet(s))" : "Printer (MPS-803)";
        };
        return menu;
    }

    // ---- errors -----------------------------------------------------------------------------------------------------------

    void Guard(Action action, string path)
    {
        try { action(); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            MessageBox.Show(owner, e.Message, $"{title} - {Path.GetFileName(path)}", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // Errors of the emulation thread reach the window of the form
    void Report(Exception e)
    {
        void Show() => MessageBox.Show(owner, e.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        if (owner is Control { IsHandleCreated: true } control) control.BeginInvoke(Show);
    }
}
