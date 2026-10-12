using Fake64.WinForms;
using Fake64.C64;

namespace Fake64;

// Finestra dell'app: ospita un C64Monitor e collega i pezzi. Lo stesso schema (Board, monitor,
// MonitorCable, PcKeyboard, PcSpeakers con AudioCable, ControlPortSelector) funziona in qualunque form, anche insieme
// ad altri controlli. Clic destro sul monitor: porte di controllo, scambio delle porte, audio, reset
public partial class Fake64Form : Form
{
    readonly Board board;
    readonly C64Monitor monitor = new() { Dock = DockStyle.Fill };
    readonly MonitorCable monitorCable;
    readonly PcKeyboard keyboardCable;
    readonly PcSpeakers? speakers;
    readonly AudioCable? audioCable;
    readonly ControlPortSelector controlPorts;
    readonly AppSettings settings = AppSettings.Load();

    public Fake64Form(SystemRoms roms, string? startupFile = null)
    {
        board = new Board(roms);
        Text = "Fake64";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = monitor.Size;
        Controls.Add(monitor);

        // I collegamenti espliciti: uscita video -> monitor, tastiera del PC (sul monitor) -> tastiera del C64
        monitorCable = new MonitorCable(board.VideoOut, monitor);
        keyboardCable = new PcKeyboard(monitor, board.Keyboard);

        // Uscita audio -> casse del PC. Senza un dispositivo audio l'emulatore funziona lo stesso, muto
        try
        {
            speakers = new PcSpeakers { Muted = !settings.Audio };
            audioCable = new AudioCable(board.AudioOut, speakers);
        }
        catch (InvalidOperationException) { }

        // Porte di controllo: joystick dalla tastiera o USB, paddle dal mouse; i tasti del joystick
        // non arrivano alla tastiera del C64
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
        menu.Items.Add(new ToolStripMenuItem("Reset", null, (_, _) => board.Enqueue(board.Reset)));
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

        Shown += async (_, _) =>
        {
            monitor.Focus();
            if (startupFile is null) return;
            // Una cartuccia riavvia la macchina; un programma va caricato quando il BASIC è pronto
            if (!MediaLoader.IsCartridge(startupFile)) await Task.Delay(MediaLoader.BootTime);
            Open(startupFile);
        };
    }

    static string? DroppedFile(DragEventArgs e) =>
        e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length == 1 && MediaLoader.IsSupported(files[0])
            ? files[0]
            : null;

    void Open(string path)
    {
        try
        {
            var description = MediaLoader.Open(board, path);
            Text = $"Fake64 - {description}";
        }
        catch (Exception e) when (e is IOException or InvalidDataException or NotSupportedException or ArgumentException)
        {
            MessageBox.Show(this, e.Message, $"Fake64 - {Path.GetFileName(path)}", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        monitor.Focus();
    }

    void SaveControlPorts()
    {
        settings.Port1 = controlPorts[1];
        settings.Port2 = controlPorts[2];
        settings.Save();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        controlPorts.Dispose();
        keyboardCable.Dispose();
        audioCable?.Dispose();
        speakers?.Dispose();
        monitorCable.Dispose();
        base.OnFormClosed(e);
    }
}
