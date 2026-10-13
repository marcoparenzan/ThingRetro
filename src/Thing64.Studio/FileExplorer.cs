using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic.FileIO;

namespace Thing64.Studio;

// The studio's file explorer: one folder at a time, with its subfolders and files.
// Double click (or Enter) opens a folder or a file; ".." goes up. Folders and files can be created, renamed (F2),
// deleted to the recycle bin (Del), moved with cut and paste (Ctrl+X, Ctrl+V) or by dragging them onto a folder.
// Files dragged in from Windows Explorer are copied into the current folder
public sealed class FileExplorer : UserControl
{
    const string ParentEntry = "..";

    readonly ToolStrip toolbar = new() { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
    readonly ToolStripTextBox address = new() { AutoSize = false };
    readonly ListView list = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        HeaderStyle = ColumnHeaderStyle.None,
        FullRowSelect = true,
        LabelEdit = true,
        HideSelection = false,
        AllowDrop = true,
        BorderStyle = BorderStyle.None,
    };
    readonly ImageList icons = new() { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(16, 16) };
    readonly FileSystemWatcher watcher = new() { IncludeSubdirectories = false };
    readonly List<string> cutPaths = [];
    string? newFileToOpen;   // a file just created: it opens when its name has been confirmed
    bool editing;            // a name is being edited: refreshing the list now would cancel the edit

    public FileExplorer()
    {
        list.Columns.Add("Name");
        list.SmallImageList = icons;
        list.Resize += (_, _) => list.Columns[0].Width = list.ClientSize.Width;

        toolbar.Items.Add(Button("Up", "Parent folder", (_, _) => GoUp()));
        toolbar.Items.Add(Button("New folder", "New folder", (_, _) => CreateFolder()));
        toolbar.Items.Add(Button("New .asm", "New assembly source", (_, _) => CreateAsmFile()));
        toolbar.Items.Add(Button("...", "Choose a folder", (_, _) => BrowseForFolder()));

        var addressBar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
        addressBar.Items.Add(address);
        addressBar.Resize += (_, _) => address.Width = addressBar.ClientSize.Width - 8;
        address.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            if (Directory.Exists(address.Text)) Navigate(address.Text);
            else address.Text = CurrentFolder;
        };

        Controls.Add(list);
        Controls.Add(addressBar);
        Controls.Add(toolbar);

        list.ItemActivate += (_, _) => OpenSelected();
        list.KeyDown += OnListKeyDown;
        list.BeforeLabelEdit += (_, e) =>
        {
            e.CancelEdit = list.Items[e.Item].Text == ParentEntry;
            editing = !e.CancelEdit;
        };
        list.AfterLabelEdit += OnAfterLabelEdit;
        list.ItemDrag += (_, _) => StartDrag();
        list.DragOver += OnDragOver;
        list.DragDrop += OnDragDrop;
        list.ContextMenuStrip = BuildContextMenu();

        watcher.Created += OnFolderChangedOnDisk;
        watcher.Deleted += OnFolderChangedOnDisk;
        watcher.Renamed += OnFolderChangedOnDisk;
    }

    public string CurrentFolder { get; private set; } = "";

    // A file was activated (double click, Enter, menu)
    public event Action<string>? FileOpened;

    // The current folder changed (to remember it)
    public event Action<string>? FolderChanged;

    // A file or folder was moved or renamed: whoever has it open can follow it
    public event Action<string, string>? PathMoved;

    // The project's main file (shown in bold) and the menu item that sets or clears it
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<string, bool>? IsMainFile { get; set; }
    public event Action<string>? MainFileToggled;

    public void Navigate(string folder)
    {
        folder = Path.GetFullPath(folder);
        if (!Directory.Exists(folder)) return;
        bool changed = !string.Equals(folder, CurrentFolder, StringComparison.OrdinalIgnoreCase);
        CurrentFolder = folder;
        address.Text = folder;
        watcher.EnableRaisingEvents = false;
        watcher.Path = folder;
        watcher.EnableRaisingEvents = true;
        RefreshList();
        if (changed) FolderChanged?.Invoke(folder);
    }

    public void RefreshList(string? select = null)
    {
        var selected = select ?? SelectedPaths().FirstOrDefault();
        list.BeginUpdate();
        list.Items.Clear();

        if (Directory.GetParent(CurrentFolder) is not null)
            list.Items.Add(new ListViewItem(ParentEntry, IconFor(null)) { Tag = Directory.GetParent(CurrentFolder)!.FullName });

        try
        {
            var folders = Directory.EnumerateDirectories(CurrentFolder).Select(path => new DirectoryInfo(path))
                .Where(IsListed).OrderBy(info => info.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var folder in folders)
                list.Items.Add(new ListViewItem(folder.Name, IconFor(null)) { Tag = folder.FullName });

            var files = Directory.EnumerateFiles(CurrentFolder).Select(path => new FileInfo(path))
                .Where(IsListed).OrderBy(info => info.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                var item = new ListViewItem(file.Name, IconFor(file.Extension)) { Tag = file.FullName };
                if (IsMainFile?.Invoke(file.FullName) == true) item.Font = new Font(list.Font, FontStyle.Bold);
                list.Items.Add(item);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            list.Items.Add(new ListViewItem($"({e.Message})"));
        }

        foreach (ListViewItem item in list.Items)
        {
            if (cutPaths.Contains(item.Tag as string ?? "", StringComparer.OrdinalIgnoreCase)) item.ForeColor = SystemColors.GrayText;
            if (selected is not null && string.Equals(item.Tag as string, selected, StringComparison.OrdinalIgnoreCase))
            {
                item.Selected = item.Focused = true;
                item.EnsureVisible();
            }
        }
        list.EndUpdate();
    }

    static bool IsListed(FileSystemInfo info) => (info.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0;

    #region Commands

    void GoUp()
    {
        if (Directory.GetParent(CurrentFolder) is { } parent)
        {
            var previous = CurrentFolder;
            Navigate(parent.FullName);
            RefreshList(previous);
        }
    }

    void BrowseForFolder()
    {
        using var dialog = new FolderBrowserDialog { InitialDirectory = CurrentFolder, UseDescriptionForTitle = true, Description = "Folder to explore" };
        if (dialog.ShowDialog(this) == DialogResult.OK) Navigate(dialog.SelectedPath);
    }

    void OpenSelected()
    {
        if (list.SelectedItems.Count != 1 || list.SelectedItems[0].Tag is not string path) return;
        if (Directory.Exists(path))
        {
            // Navigate reloads the list: the selected item is gone afterwards
            bool up = list.SelectedItems[0].Text == ParentEntry;
            var previous = CurrentFolder;
            Navigate(path);
            if (up) RefreshList(previous);   // back in the parent, the folder we came from stays selected
        }
        else if (File.Exists(path))
        {
            FileOpened?.Invoke(path);
        }
    }

    public void CreateFolder()
    {
        var path = UniquePath(CurrentFolder, "New folder", "");
        if (!Try(() => Directory.CreateDirectory(path))) return;
        RefreshList(path);
        EditSelectedName();
    }

    // A new source with a minimal program; it opens in the editor once its name is confirmed
    public void CreateAsmFile()
    {
        var path = UniquePath(CurrentFolder, "program", ".asm");
        if (!Try(() => File.WriteAllText(path, NewAsmTemplate(Path.GetFileNameWithoutExtension(path))))) return;
        newFileToOpen = path;
        RefreshList(path);
        EditSelectedName();
    }

    static string NewAsmTemplate(string name) => $"""
        ; {name}

        BORDER      = $D020

                    * = $0801
                    .basic                      ; 10 SYS2061: RUN starts the code below

                    INC BORDER
                    RTS

        """;

    void EditSelectedName()
    {
        if (list.SelectedItems.Count != 1) return;
        editing = true;
        list.SelectedItems[0].BeginEdit();
    }

    void DeleteSelected()
    {
        var paths = SelectedPaths().ToList();
        if (paths.Count == 0) return;
        string what = paths.Count == 1 ? $"\"{Path.GetFileName(paths[0])}\"" : $"{paths.Count} items";
        if (MessageBox.Show(this, $"Move {what} to the recycle bin?", "Delete", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            return;

        foreach (var path in paths)
        {
            Try(() =>
            {
                if (Directory.Exists(path)) FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                else FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            });
        }
        RefreshList();
    }

    void Cut()
    {
        cutPaths.Clear();
        cutPaths.AddRange(SelectedPaths());
        RefreshList();
    }

    // Paste moves the cut items into the selected folder, or into the current one
    void Paste()
    {
        if (cutPaths.Count == 0) return;
        var target = SelectedPaths().FirstOrDefault(Directory.Exists) ?? CurrentFolder;
        MoveInto(cutPaths.ToList(), target);
        cutPaths.Clear();
        RefreshList();
    }

    void MoveInto(IEnumerable<string> paths, string folder)
    {
        foreach (var path in paths)
        {
            var destination = Path.Combine(folder, Path.GetFileName(path));
            if (string.Equals(Path.GetDirectoryName(path), folder, StringComparison.OrdinalIgnoreCase)) continue;
            if (folder.StartsWith(path + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(folder, path, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, $"Cannot move \"{Path.GetFileName(path)}\" into itself.", "Move", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                continue;
            }
            if (File.Exists(destination) || Directory.Exists(destination))
            {
                MessageBox.Show(this, $"\"{Path.GetFileName(path)}\" already exists in {folder}.", "Move", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                continue;
            }
            if (Try(() => { if (Directory.Exists(path)) Directory.Move(path, destination); else File.Move(path, destination); }))
                PathMoved?.Invoke(path, destination);
        }
    }

    void CopyInto(IEnumerable<string> paths, string folder)
    {
        foreach (var path in paths)
        {
            var name = Path.GetFileName(path);
            var destination = File.Exists(Path.Combine(folder, name)) || Directory.Exists(Path.Combine(folder, name))
                ? UniquePath(folder, Path.GetFileNameWithoutExtension(name), Path.GetExtension(name))
                : Path.Combine(folder, name);
            Try(() =>
            {
                if (Directory.Exists(path)) FileSystem.CopyDirectory(path, destination, UIOption.OnlyErrorDialogs);
                else File.Copy(path, destination);
            });
        }
        RefreshList();
    }

    void OnAfterLabelEdit(object? sender, LabelEditEventArgs e)
    {
        editing = false;
        var item = list.Items[e.Item];
        var path = (string)item.Tag!;
        string? opened = null;

        if (e.Label is { } label && label != item.Text)
        {
            var destination = Path.Combine(CurrentFolder, label.Trim());
            if (label.Trim().Length == 0 || label.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                MessageBox.Show(this, $"\"{label}\" is not a valid name.", "Rename", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                e.CancelEdit = true;
            }
            else if ((File.Exists(destination) || Directory.Exists(destination)) &&
                     !string.Equals(destination, path, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, $"\"{label}\" already exists.", "Rename", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                e.CancelEdit = true;
            }
            else if (Try(() => { if (Directory.Exists(path)) Directory.Move(path, destination); else File.Move(path, destination); }))
            {
                item.Tag = destination;
                PathMoved?.Invoke(path, destination);
                if (path == newFileToOpen) opened = destination;
            }
            else e.CancelEdit = true;
        }
        else if (path == newFileToOpen) opened = path;

        newFileToOpen = null;
        BeginInvoke(() =>
        {
            RefreshList((string)item.Tag!);
            if (opened is not null) FileOpened?.Invoke(opened);
        });
    }

    static string UniquePath(string folder, string name, string extension)
    {
        var path = Path.Combine(folder, name + extension);
        for (int n = 2; File.Exists(path) || Directory.Exists(path); n++)
            path = Path.Combine(folder, $"{name} {n}{extension}");
        return path;
    }

    bool Try(Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            if (e is not OperationCanceledException)
                MessageBox.Show(this, e.Message, "Thing64", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }

    IEnumerable<string> SelectedPaths() =>
        list.SelectedItems.Cast<ListViewItem>().Where(item => item.Text != ParentEntry).Select(item => item.Tag).OfType<string>();

    #endregion

    #region Keyboard, menu, drag and drop

    void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyData)
        {
            case Keys.F2: EditSelectedName(); break;
            case Keys.Delete: DeleteSelected(); break;
            case Keys.Back: GoUp(); break;
            case Keys.F5: RefreshList(); break;
            case Keys.Control | Keys.X: Cut(); break;
            case Keys.Control | Keys.V: Paste(); break;
            default: return;
        }
        e.Handled = e.SuppressKeyPress = true;
    }

    ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();
        var open = new ToolStripMenuItem("Open", null, (_, _) => OpenSelected());
        var rename = new ToolStripMenuItem("Rename", null, (_, _) => EditSelectedName()) { ShortcutKeyDisplayString = "F2" };
        var delete = new ToolStripMenuItem("Delete", null, (_, _) => DeleteSelected()) { ShortcutKeyDisplayString = "Del" };
        var cut = new ToolStripMenuItem("Cut", null, (_, _) => Cut()) { ShortcutKeyDisplayString = "Ctrl+X" };
        var paste = new ToolStripMenuItem("Paste", null, (_, _) => Paste()) { ShortcutKeyDisplayString = "Ctrl+V" };
        var main = new ToolStripMenuItem("Set as main file", null, (_, _) =>
        {
            if (SelectedPaths().FirstOrDefault() is string path) MainFileToggled?.Invoke(path);
        });
        menu.Items.AddRange(
        [
            open,
            new ToolStripSeparator(),
            new ToolStripMenuItem("New folder", null, (_, _) => CreateFolder()),
            new ToolStripMenuItem("New .asm file", null, (_, _) => CreateAsmFile()),
            new ToolStripSeparator(),
            rename, delete, cut, paste,
            new ToolStripSeparator(),
            main,
            new ToolStripMenuItem("Show in Windows Explorer", null, (_, _) => ShowInExplorer()),
            new ToolStripMenuItem("Refresh", null, (_, _) => RefreshList()) { ShortcutKeyDisplayString = "F5" },
        ]);
        menu.Opening += (_, _) =>
        {
            var selected = SelectedPaths().ToList();
            bool asm = selected.Count == 1 && selected[0].EndsWith(".asm", StringComparison.OrdinalIgnoreCase);
            main.Visible = asm;
            if (asm) main.Text = IsMainFile?.Invoke(selected[0]) == true ? "Clear main file" : "Set as main file";
        };
        menu.Opening += (_, _) =>
        {
            bool any = SelectedPaths().Any();
            open.Enabled = list.SelectedItems.Count == 1;
            rename.Enabled = list.SelectedItems.Count == 1 && any;
            delete.Enabled = cut.Enabled = any;
            paste.Enabled = cutPaths.Count > 0;
        };
        return menu;
    }

    void ShowInExplorer()
    {
        var path = SelectedPaths().FirstOrDefault();
        Process.Start(new ProcessStartInfo("explorer.exe", path is null ? $"\"{CurrentFolder}\"" : $"/select,\"{path}\"") { UseShellExecute = true });
    }

    // Dragging items inside the list moves them; their paths also go out as files (to Windows Explorer, or to the
    // studio, which opens them)
    void StartDrag()
    {
        var paths = SelectedPaths().ToArray();
        if (paths.Length == 0) return;
        var data = new DataObject(DataFormats.FileDrop, paths);
        data.SetData(typeof(FileExplorer), this);
        list.DoDragDrop(data, DragDropEffects.Move | DragDropEffects.Copy);
    }

    string? DropTarget(DragEventArgs e)
    {
        var point = list.PointToClient(new Point(e.X, e.Y));
        var item = list.GetItemAt(point.X, point.Y);
        return item?.Tag is string path && Directory.Exists(path) ? path : null;
    }

    void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] paths) { e.Effect = DragDropEffects.None; return; }
        bool internalDrag = e.Data.GetData(typeof(FileExplorer)) == this;
        var target = DropTarget(e);
        if (internalDrag)
            e.Effect = target is not null && !paths.Contains(target, StringComparer.OrdinalIgnoreCase) ? DragDropEffects.Move : DragDropEffects.None;
        else
            e.Effect = DragDropEffects.Copy;
    }

    void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] paths) return;
        if (e.Data.GetData(typeof(FileExplorer)) == this)
        {
            if (DropTarget(e) is string target) MoveInto(paths, target);
            RefreshList();
        }
        else
        {
            CopyInto(paths, DropTarget(e) ?? CurrentFolder);
        }
    }

    void OnFolderChangedOnDisk(object sender, FileSystemEventArgs e)
    {
        if (IsHandleCreated) BeginInvoke(() => { if (!editing) RefreshList(); });
    }

    #endregion

    #region Icons

    // Shell icons, one per extension (and one for folders), so that the list looks like Windows Explorer
    int IconFor(string? extension)
    {
        string key = extension?.ToLowerInvariant() ?? "<folder>";
        if (icons.Images.ContainsKey(key)) return icons.Images.IndexOfKey(key);

        var info = new SHFILEINFO();
        uint attributes = extension is null ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;
        IntPtr result = SHGetFileInfo(extension is null ? "folder" : "file" + extension, attributes, ref info,
            (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES);
        if (result != IntPtr.Zero && info.hIcon != IntPtr.Zero)
        {
            using (var icon = Icon.FromHandle(info.hIcon)) icons.Images.Add(key, icon.ToBitmap());
            DestroyIcon(info.hIcon);
        }
        else
        {
            icons.Images.Add(key, new Bitmap(16, 16));
        }
        return icons.Images.IndexOfKey(key);
    }

    const uint SHGFI_ICON = 0x100, SHGFI_SMALLICON = 0x1, SHGFI_USEFILEATTRIBUTES = 0x10;
    const uint FILE_ATTRIBUTE_DIRECTORY = 0x10, FILE_ATTRIBUTE_NORMAL = 0x80;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SHGetFileInfo(string path, uint attributes, ref SHFILEINFO info, uint size, uint flags);

    [DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr icon);

    #endregion

    static ToolStripButton Button(string text, string tip, EventHandler click) =>
        new(text, null, click) { ToolTipText = tip, DisplayStyle = ToolStripItemDisplayStyle.Text };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            watcher.Dispose();
            icons.Dispose();
        }
        base.Dispose(disposing);
    }
}
