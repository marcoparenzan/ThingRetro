using Fake64.C64;
using Fake64.Computer;

namespace Fake64.Studio;

// The studio: the app's main window. An MDI container whose children (assembly editors and C64s) are shown as
// tabs at the bottom, with the file explorer on the left.
// F5 in an editor assembles and runs the program in that editor's C64 tab (created the first time)
public sealed class StudioForm : Form
{
    readonly SystemRoms roms;
    readonly AppSettings settings = AppSettings.Current;
    readonly FileExplorer explorer = new() { Dock = DockStyle.Left };
    readonly MainFiles mainFiles = new(AppSettings.Current);
    readonly TabControl tabs = new() { Dock = DockStyle.Bottom, Alignment = TabAlignment.Bottom, Height = 26 };
    readonly Dictionary<AsmEditorForm, ComputerForm> machines = [];
    bool syncingTabs;

    public StudioForm(SystemRoms roms, string? startupFile = null)
    {
        this.roms = roms;
        Text = "Fake64";
        IsMdiContainer = true;
        KeyPreview = true;
        RestorePlacement();

        var splitter = new Splitter { Dock = DockStyle.Left, Width = 4 };
        explorer.Width = Math.Max(150, settings.ExplorerWidth);
        var menu = BuildMenu();
        MainMenuStrip = menu;

        // Docking: the MDI area (MdiClient) fills what the explorer, the splitter and the tab strip leave
        Controls.Add(tabs);
        Controls.Add(splitter);
        Controls.Add(explorer);
        Controls.Add(menu);
        Controls.OfType<MdiClient>().Single().BringToFront();

        explorer.FileOpened += OpenFile;
        explorer.IsMainFile = mainFiles.IsMain;
        explorer.MainFileToggled += path => ToggleMain(path);
        explorer.FolderChanged += folder =>
        {
            settings.LastFolder = folder;
            settings.Save();
        };
        explorer.PathMoved += (oldPath, newPath) =>
        {
            foreach (var editor in MdiChildren.OfType<AsmEditorForm>()) editor.FollowMove(oldPath, newPath);
        };
        explorer.Navigate(StartFolder());
        splitter.SplitterMoved += (_, _) =>
        {
            settings.ExplorerWidth = explorer.Width;
            settings.Save();
        };

        MdiChildActivate += (_, _) => SyncTabs();
        tabs.SelectedIndexChanged += (_, _) =>
        {
            if (!syncingTabs && tabs.SelectedTab?.Tag is Form child) child.Activate();
        };
        tabs.MouseUp += OnTabMouseUp;

        AllowDrop = true;
        DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
        DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] files) foreach (var file in files) OpenFile(file);
        };

        Shown += (_, _) =>
        {
            if (startupFile is not null) OpenFile(Path.GetFullPath(startupFile));
        };
    }

    // The last folder explored; the first time, the samples of the repository if they are there, or Documents
    string StartFolder()
    {
        if (settings.LastFolder is { } last && Directory.Exists(last)) return last;
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            var samples = Path.Combine(folder.FullName, "samples", "src");
            if (Directory.Exists(samples)) return samples;
        }
        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    #region Opening files

    // .asm (and other text files) in an editor; .prg and .crt in a new C64
    public void OpenFile(string path)
    {
        if (Directory.Exists(path)) { explorer.Navigate(path); return; }
        if (!File.Exists(path)) return;

        if (MediaLoader.IsSupported(path))
        {
            NewMachine().Run(path);
            return;
        }

        var open = MdiChildren.OfType<AsmEditorForm>()
            .FirstOrDefault(editor => string.Equals(editor.FilePath, path, StringComparison.OrdinalIgnoreCase));
        if (open is not null)
        {
            open.Activate();
            return;
        }
        if (!IsTextFile(path))
        {
            MessageBox.Show(this, $"{Path.GetFileName(path)} is not a text file.", "Fake64", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var created = new AsmEditorForm(path);
        created.RunRequested += RunInMachine;
        created.RunInNewWindowRequested += RunInNewWindow;
        created.MainFileOf = mainFiles.Resolve;
        created.SaveOthers = () =>
        {
            foreach (var other in MdiChildren.OfType<AsmEditorForm>().Where(e => e != created && e.IsDirty)) other.Save();
        };
        created.ErrorInOtherFile += (file, line, message) =>
        {
            OpenFile(file);
            MdiChildren.OfType<AsmEditorForm>()
                .FirstOrDefault(e => string.Equals(e.FilePath, file, StringComparison.OrdinalIgnoreCase))?.ShowError(line, message);
        };
        created.FormClosed += (_, _) => machines.Remove(created);
        ShowChild(created);
    }

    static bool IsTextFile(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var buffer = new byte[Math.Min(4096, stream.Length)];
            int read = stream.Read(buffer);
            return !buffer.AsSpan(0, read).Contains((byte)0);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    ComputerForm NewMachine()
    {
        var machine = new ComputerForm(roms);
        ShowChild(machine);
        return machine;
    }

    // Each editor has its own C64: F5 restarts it with the new program
    void RunInMachine(AsmEditorForm editor, string prgPath)
    {
        if (!machines.TryGetValue(editor, out var machine) || machine.IsDisposed)
        {
            machine = NewMachine();
            machines[editor] = machine;
            machine.FormClosed += (_, _) => machines.Remove(editor);
        }
        else
        {
            machine.Activate();
        }
        machine.Run(prgPath);
    }

    // Shift+F5: the program runs in a plain C64 window outside the studio, started by whoever hosts the studio
    // (the fake64 command line starts itself again in a new process: "fake64 run file.prg")
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Action<string>? LaunchComputer { get; set; }

    void RunInNewWindow(string prgPath)
    {
        if (LaunchComputer is null)
        {
            MessageBox.Show(this, "This studio cannot start separate C64 windows.", "Fake64", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            LaunchComputer(prgPath);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            MessageBox.Show(this, e.Message, "Fake64", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    void ShowChild(Form child)
    {
        child.MdiParent = this;
        child.ShowIcon = false;
        child.WindowState = FormWindowState.Maximized;
        child.TextChanged += (_, _) => SyncTabs();
        child.FormClosed += (_, _) => BeginInvoke(SyncTabs);
        child.Show();
    }

    void OpenWithDialog()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "C64 files (*.asm;*.prg;*.crt)|*.asm;*.prg;*.crt|Assembly sources (*.asm)|*.asm|Programs (*.prg)|*.prg|Cartridges (*.crt)|*.crt|All files (*.*)|*.*",
            InitialDirectory = explorer.CurrentFolder,
            Multiselect = true,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) foreach (var file in dialog.FileNames) OpenFile(file);
    }

    #endregion

    #region Tabs

    // One tab per MDI child, in opening order; the selected tab follows the active child
    void SyncTabs()
    {
        syncingTabs = true;
        try
        {
            var children = MdiChildren.Where(child => !child.IsDisposed).ToList();
            foreach (var page in tabs.TabPages.Cast<TabPage>().Where(page => !children.Contains((Form)page.Tag!)).ToList())
                tabs.TabPages.Remove(page);
            foreach (var child in children)
            {
                var page = tabs.TabPages.Cast<TabPage>().FirstOrDefault(page => page.Tag == child);
                if (page is null) tabs.TabPages.Add(page = new TabPage { Tag = child });
                if (page.Text != child.Text) page.Text = child.Text;
            }
            var active = ActiveMdiChild;
            var selected = tabs.TabPages.Cast<TabPage>().FirstOrDefault(page => page.Tag == active);
            if (selected is not null && tabs.SelectedTab != selected) tabs.SelectedTab = selected;
            tabs.Visible = tabs.TabCount > 0;
        }
        finally
        {
            syncingTabs = false;
        }
    }

    // Middle click closes a tab; right click offers to close it or the others
    void OnTabMouseUp(object? sender, MouseEventArgs e)
    {
        var page = Enumerable.Range(0, tabs.TabCount).Where(i => tabs.GetTabRect(i).Contains(e.Location))
            .Select(i => tabs.TabPages[i]).FirstOrDefault();
        if (page?.Tag is not Form child) return;

        if (e.Button == MouseButtons.Middle) child.Close();
        if (e.Button == MouseButtons.Right)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Close", null, (_, _) => child.Close());
            menu.Items.Add("Close others", null, (_, _) =>
            {
                foreach (var other in MdiChildren.Where(other => other != child)) other.Close();
            });
            menu.Show(tabs, e.Location);
        }
    }

    #endregion

    #region Menu

    MenuStrip BuildMenu()
    {
        var menu = new MenuStrip();

        var file = new ToolStripMenuItem("&File");
        file.DropDownItems.AddRange(
        [
            new ToolStripMenuItem("&New .asm file", null, (_, _) => explorer.CreateAsmFile(), Keys.Control | Keys.N),
            new ToolStripMenuItem("New &folder", null, (_, _) => explorer.CreateFolder(), Keys.Control | Keys.Shift | Keys.N),
            new ToolStripMenuItem("&Open...", null, (_, _) => OpenWithDialog(), Keys.Control | Keys.O),
            new ToolStripSeparator(),
            new ToolStripMenuItem("&Save", null, (_, _) => (ActiveMdiChild as AsmEditorForm)?.Save(), Keys.Control | Keys.S),
            new ToolStripMenuItem("Save &all", null, (_, _) => SaveAll(), Keys.Control | Keys.Shift | Keys.S),
            new ToolStripMenuItem("&Close tab", null, (_, _) => ActiveMdiChild?.Close(), Keys.Control | Keys.W),
            new ToolStripSeparator(),
            new ToolStripMenuItem("E&xit", null, (_, _) => Close()),
        ]);

        var build = new ToolStripMenuItem("&Build");
        build.DropDownItems.AddRange(
        [
            new ToolStripMenuItem("&Assemble", null, (_, _) => (ActiveMdiChild as AsmEditorForm)?.Assemble(), Keys.F7),
            new ToolStripMenuItem("Assemble and &run", null, (_, _) => (ActiveMdiChild as AsmEditorForm)?.AssembleAndRun(), Keys.F5),
            new ToolStripMenuItem("Assemble and run in a new &window", null, (_, _) => (ActiveMdiChild as AsmEditorForm)?.AssembleAndRunInNewWindow(), Keys.Shift | Keys.F5),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Set as &main file", null, (_, _) => { if (ActiveMdiChild is AsmEditorForm editor) ToggleMain(editor.FilePath); }),
        ]);

        var machine = new ToolStripMenuItem("&Machine");
        machine.DropDownItems.AddRange(
        [
            new ToolStripMenuItem("&New C64", null, (_, _) => NewMachine()),
            new ToolStripMenuItem("&Reset", null, (_, _) => (ActiveMdiChild as ComputerForm)?.Reset()),
        ]);

        var window = new ToolStripMenuItem("&Window");
        menu.MdiWindowListItem = window;

        menu.Items.AddRange([file, build, machine, window]);
        return menu;
    }

    // In a C64 tab the PC keyboard is the C64 keyboard: F5, Ctrl+S... go to the C64, not to the menu
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData) =>
        ActiveMdiChild is not ComputerForm && base.ProcessCmdKey(ref msg, keyData);

    // Makes the file the main file of its folder, or clears it if it already is
    void ToggleMain(string path)
    {
        if (mainFiles.IsMain(path)) mainFiles.ClearMain(Path.GetDirectoryName(path)!);
        else mainFiles.SetMain(path);
        explorer.RefreshList();
    }

    void SaveAll()
    {
        foreach (var editor in MdiChildren.OfType<AsmEditorForm>().Where(editor => editor.IsDirty)) editor.Save();
    }

    #endregion

    #region Window placement

    void RestorePlacement()
    {
        StartPosition = FormStartPosition.Manual;
        var placement = settings.StudioWindow;
        var bounds = placement is null ? Rectangle.Empty : new Rectangle(placement.X, placement.Y, placement.Width, placement.Height);
        if (bounds.Width < 400 || bounds.Height < 300 || !Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(bounds)))
        {
            var area = Screen.PrimaryScreen!.WorkingArea;
            bounds = new Rectangle(area.X + area.Width / 10, area.Y + area.Height / 10, area.Width * 8 / 10, area.Height * 8 / 10);
        }
        Bounds = bounds;
        if (placement?.Maximized == true) WindowState = FormWindowState.Maximized;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        settings.StudioWindow = new WindowPlacement(bounds.X, bounds.Y, bounds.Width, bounds.Height, WindowState == FormWindowState.Maximized);
        settings.Save();
        base.OnFormClosed(e);
    }

    #endregion
}
