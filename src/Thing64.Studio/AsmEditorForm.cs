using System.Text.Json;
using System.Text.Json.Nodes;
using Thing64.Asm;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Thing64.Studio;

// An editor tab: Monaco in a WebView2 (Studio/Editor/editor.html), with the 6502 language built from the assembler.
// Ctrl+S saves, F7 assembles (the .prg is written next to the source), F5 assembles and runs in a C64 tab.
// The page sends the whole text at every change, so the form always has it and saving never waits for the page
public sealed class AsmEditorForm : Form
{
    const string HostName = "thing64.editor";

    readonly WebView2 webView = new() { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.FromArgb(0x1E, 0x1E, 0x1E) };
    readonly ToolStripStatusLabel status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    string text = "";
    bool dirty;
    bool pageReady;

    public AsmEditorForm(string path)
    {
        FilePath = path;
        text = File.ReadAllText(path);
        ShowIcon = false;

        var toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
        toolbar.Items.Add(new ToolStripButton("Save", null, (_, _) => Save()) { ToolTipText = "Save (Ctrl+S)" });
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(new ToolStripButton("Assemble", null, (_, _) => Assemble()) { ToolTipText = "Assemble to .prg (F7)" });
        toolbar.Items.Add(new ToolStripButton("Run", null, (_, _) => AssembleAndRun()) { ToolTipText = "Assemble and run in a C64 tab (F5)" });
        toolbar.Items.Add(new ToolStripButton("Run in window", null, (_, _) => AssembleAndRunInNewWindow()) { ToolTipText = "Assemble and run in a new Thing64 window (Shift+F5)" });

        var statusBar = new StatusStrip { SizingGrip = false };
        statusBar.Items.Add(status);

        Controls.Add(webView);
        Controls.Add(toolbar);
        Controls.Add(statusBar);
        UpdateTitle();

        Load += async (_, _) => await InitializeWebViewAsync();
    }

    public string FilePath { get; private set; }

    public bool IsDirty => dirty;

    // F5: the .prg is ready, the studio runs it in this editor's C64 tab
    public event Action<AsmEditorForm, string>? RunRequested;

    // Shift+F5: the .prg is ready, the studio runs it in a new Thing64 process
    public event Action<string>? RunInNewWindowRequested;

    // The error is in another file (included with !source): the studio opens it there
    public event Action<string, int, string>? ErrorInOtherFile;

    // Before assembling, the other open sources are saved: the assembler reads the included files from disk
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Action? SaveOthers { get; set; }

    // The file to assemble for this one (the project's main file, see MainFiles); by default the file itself
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<string, string>? MainFileOf { get; set; }

    // Error to mark as soon as the page is ready (the editor may have just been opened to show it)
    (int Line, string Message)? pendingError;

    public void ShowError(int line, string message)
    {
        if (!pageReady) { pendingError = (line, message); return; }
        Post(new { type = "markers", markers = new[] { new { line, message } } });
        Post(new { type = "reveal", line });
        SetStatus($"Line {line}: {message}", error: true);
    }

    bool IsAssembly => string.Equals(Path.GetExtension(FilePath), ".asm", StringComparison.OrdinalIgnoreCase);

    // The file was moved or renamed in the explorer
    public void FollowMove(string oldPath, string newPath)
    {
        if (string.Equals(FilePath, oldPath, StringComparison.OrdinalIgnoreCase)) FilePath = newPath;
        else if (FilePath.StartsWith(oldPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            FilePath = newPath + FilePath[oldPath.Length..];
        else return;
        UpdateTitle();
    }

    void UpdateTitle() => Text = Path.GetFileName(FilePath) + (dirty ? " *" : "");

    #region WebView2

    // One WebView2 environment for all the editors, with its data in %LocalAppData%\Thing64\WebView2
    static Task<CoreWebView2Environment>? environment;

    static Task<CoreWebView2Environment> Environment() => environment ??= CoreWebView2Environment.CreateAsync(
        userDataFolder: Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Thing64", "WebView2"));

    async Task InitializeWebViewAsync()
    {
        try
        {
            await webView.EnsureCoreWebView2Async(await Environment());
        }
        catch (Exception e) when (e is WebView2RuntimeNotFoundException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            SetStatus($"WebView2 is not available: {e.Message}", error: true);
            return;
        }

        var core = webView.CoreWebView2;
        core.Settings.AreBrowserAcceleratorKeysEnabled = false;   // F5 must not reload the page: the editor uses it
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = true;
        core.SetVirtualHostNameToFolderMapping(HostName, Path.Combine(AppContext.BaseDirectory, "Studio", "Editor"),
            CoreWebView2HostResourceAccessKind.Allow);
        core.WebMessageReceived += OnWebMessage;
        core.Navigate($"https://{HostName}/editor.html");
    }

    void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var message = JsonNode.Parse(e.WebMessageAsJson);
        switch ((string?)message?["type"])
        {
            case "ready":
                pageReady = true;
                Post(new
                {
                    type = "init",
                    text,
                    language = IsAssembly ? "asm6502" : "plaintext",
                    mnemonics = Assembler.Mnemonics,
                    directives = Assembler.Directives,
                    descriptions = InstructionSet.Descriptions,
                });
                if (pendingError is var (line, error))
                {
                    pendingError = null;
                    ShowError(line, error);
                }
                break;
            case "changed":
                text = (string?)message["text"] ?? "";
                if (!dirty)
                {
                    dirty = true;
                    UpdateTitle();
                }
                break;
            case "command":
                switch ((string?)message["name"])
                {
                    case "save": Save(); break;
                    case "assemble": Assemble(); break;
                    case "run": AssembleAndRun(); break;
                    case "close": Close(); break;
                    case "runwindow": AssembleAndRunInNewWindow(); break;
                }
                break;
        }
    }

    void Post(object message)
    {
        if (pageReady) webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(message));
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        Post(new { type = "focus" });
    }

    #endregion

    #region Commands

    public bool Save()
    {
        try
        {
            File.WriteAllText(FilePath, text);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            SetStatus($"Cannot save: {e.Message}", error: true);
            return false;
        }
        dirty = false;
        UpdateTitle();
        SetStatus($"Saved {Path.GetFileName(FilePath)}");
        return true;
    }

    // Saves, assembles and writes the .prg next to the source; errors are marked on their line
    public string? Assemble()
    {
        if (!IsAssembly)
        {
            SetStatus("Only .asm files can be assembled", error: true);
            return null;
        }
        if (dirty && !Save()) return null;
        SaveOthers?.Invoke();

        try
        {
            string main = MainFileOf?.Invoke(FilePath) ?? FilePath;
            bool itself = string.Equals(main, FilePath, StringComparison.OrdinalIgnoreCase);
            string source = itself ? text : File.ReadAllText(main);
            var result = Assembler.Assemble(source, Path.GetFileName(main), Path.GetDirectoryName(main));
            var prgPath = Path.ChangeExtension(main, ".prg");
            File.WriteAllBytes(prgPath, result.Prg);
            Post(new { type = "markers", markers = Array.Empty<object>() });
            int end = result.Origin + result.Bytes.Length - 1;
            string which = itself ? "" : $"{Path.GetFileName(main)} (main file): ";
            SetStatus($"Assembled {which}${result.Origin:X4}-${end:X4} ({result.Bytes.Length} bytes) -> {Path.GetFileName(prgPath)}");
            return prgPath;
        }
        catch (AssemblerException e) when (e.FilePath is not null &&
                                           !string.Equals(e.FilePath, FilePath, StringComparison.OrdinalIgnoreCase))
        {
            Post(new { type = "markers", markers = Array.Empty<object>() });
            SetStatus($"{e.SourceName}, line {e.Line}: {e.Reason}", error: true);
            ErrorInOtherFile?.Invoke(e.FilePath, e.Line, e.Reason);
        }
        catch (AssemblerException e)
        {
            ShowError(e.Line, e.Reason);
        }
        catch (Exception e) when (e is FormatException or OverflowException or IOException or UnauthorizedAccessException)
        {
            SetStatus(e.Message, error: true);
        }
        return null;
    }

    public void AssembleAndRunInNewWindow()
    {
        if (Assemble() is string prgPath) RunInNewWindowRequested?.Invoke(prgPath);
    }

    public void AssembleAndRun()
    {
        if (Assemble() is string prgPath) RunRequested?.Invoke(this, prgPath);
    }

    void SetStatus(string message, bool error = false)
    {
        status.Text = message;
        status.ForeColor = error ? Color.Firebrick : SystemColors.ControlText;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (dirty && !e.Cancel)
        {
            var answer = MessageBox.Show(this, $"Save the changes to {Path.GetFileName(FilePath)}?", "Thing64",
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer == DialogResult.Cancel || (answer == DialogResult.Yes && !Save())) e.Cancel = true;
        }
        base.OnFormClosing(e);
    }

    #endregion
}
