using Fake64.WinForms;
using Fake64.C64;

namespace Fake64;

// Finestra dell'app: ospita un C64Monitor e collega i pezzi. Lo stesso schema (Board, monitor,
// MonitorCable, PcKeyboard) funziona in qualunque form, anche insieme ad altri controlli
public partial class Fake64Form : Form
{
    readonly Board board;
    readonly C64Monitor monitor = new() { Dock = DockStyle.Fill };
    readonly MonitorCable monitorCable;
    readonly PcKeyboard keyboardCable;

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

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        keyboardCable.Dispose();
        monitorCable.Dispose();
        base.OnFormClosed(e);
    }
}
