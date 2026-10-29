using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NAudio;

namespace ThingNES.Desktop;

internal sealed class MainWindow : Form
{
    readonly ILogger<MainWindow> logger;
    readonly VideoPanel video = new();
    readonly System.Windows.Forms.Timer timer = new() { Interval = 8 };
    readonly Stopwatch clock = new();
    readonly FramePacer pacer = new();
    readonly ToolStripButton sound = new("Enable audio") { CheckOnClick = true };
    AudioPlayback? playback;
    readonly ToolStripStatusLabel status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    readonly ToolStripButton pause = new("Pause") { Enabled = false };
    readonly ToolStripButton step = new("Step frame") { Enabled = false };
    readonly ToolStripButton restart = new("Power cycle") { Enabled = false };
    readonly ToolStripButton record = new("Record WAV") { Enabled = false };
    NesMachine? machine;
    byte[]? image;
    string romName = "";
    FileStream? recordingStream;
    WaveWriter? recording;
    bool running;

    public MainWindow(ILogger<MainWindow> logger)
    {
        this.logger = logger;
        Text = "ThingNES — NTSC / NROM · UxROM · CNROM"; ClientSize = new Size(800, 800); MinimumSize = new Size(540, 400);
        KeyPreview = true;
        var toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
        var open = new ToolStripButton("Open ROM"); open.Click += (_, _) => OpenRom();
        pause.Click += (_, _) => SetRunning(!running);
        step.Click += (_, _) => StepFrame();
        restart.Click += (_, _) => PowerCycle();
        record.Click += (_, _) => ToggleRecording();
        sound.Click += (_, _) => ToggleSound();
        toolbar.Items.AddRange([open, pause, step, restart, new ToolStripSeparator(), sound, record]);
        var help = new Label { Text = "Controller 1: Z=A · X=B · Shift=Select · Enter=Start · Arrows=D-pad | F5 pause · F10 step · F2 power cycle | Audio is opt-in", Dock = DockStyle.Bottom, Height = 38, TextAlign = ContentAlignment.MiddleCenter };
        var bar = new StatusStrip(); bar.Items.Add(status);
        Controls.Add(video); Controls.Add(help); Controls.Add(bar); Controls.Add(toolbar);
        timer.Tick += (_, _) => ScheduledFrames();
        Deactivate += (_, _) => { if (machine is not null) machine.Controller1.Pressed = Buttons.None; };
        SetRunning(false);
    }
    public void LoadRom(string path)
    {
        bool previous = running; SetRunning(false);
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            var next = new NesMachine(Cartridge.Load(bytes));
            next.RunFrame(); next.RunFrame(); // Prime pipelines and produce one completed visible frame.
            StopRecording();
            DetachPlayback();
            image = bytes; machine = next; romName = Path.GetFileName(path);
            AttachPlayback();
            restart.Enabled = pause.Enabled = record.Enabled = true;
            video.Present(next.Ppu.Framebuffer.Span);
            logger.LogInformation("Loaded {Rom} ({Prg} PRG bytes)", romName, next.Cartridge.PrgSize);
            SetRunning(true);
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UnauthorizedAccessException or ArgumentException)
        { ShowError(ex); SetRunning(previous); }
    }
    void OpenRom()
    {
        bool previous = running; SetRunning(false);
        using var dialog = new OpenFileDialog { Filter = "NES ROM (*.nes)|*.nes|All files (*.*)|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(this) == DialogResult.OK) { SetRunning(previous); LoadRom(dialog.FileName); }
        else SetRunning(previous);
    }
    void SetRunning(bool value)
    {
        running = value && machine is not null;
        pacer.Reset();
        if (running) { clock.Restart(); timer.Start(); TryStartPlayback(); }
        else { clock.Reset(); timer.Stop(); playback?.Pause(); }
        pause.Text = running ? "Pause" : "Resume";
        step.Enabled = machine is not null && !running;
        UpdateStatus();
    }
    void ScheduledFrames()
    {
        if (!running) return;
        TimeSpan elapsed = clock.Elapsed; clock.Restart();
        int due = pacer.Advance(elapsed);
        for (int i = 0; i < due && running; i++) AdvanceFrame();
    }
    void StepFrame() { SetRunning(false); if (machine is not null) AdvanceFrame(); }
    void AdvanceFrame()
    {
        if (machine is null) return;
        try { machine.RunFrame(); video.Present(machine.Ppu.Framebuffer.Span); UpdateStatus(); }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        { SetRunning(false); StopRecording(); ShowError(ex); }
    }
    void PowerCycle()
    {
        if (image is null) return;
        bool previous = running; SetRunning(false); StopRecording();
        DetachPlayback();
        machine = new NesMachine(Cartridge.Load(image)); machine.RunFrame(); machine.RunFrame();
        AttachPlayback();
        video.Present(machine.Ppu.Framebuffer.Span); SetRunning(previous);
    }
    void AttachPlayback()
    {
        if (playback is not null && machine is not null) machine.Apu.SampleProduced += FeedPlayback;
    }
    void DetachPlayback()
    {
        if (machine is not null) machine.Apu.SampleProduced -= FeedPlayback;
        playback?.Pause();
    }
    void FeedPlayback(short sample) { if (running) playback?.AddSample(sample); }
    void ToggleSound()
    {
        if (!sound.Checked)
        {
            DetachPlayback(); playback?.Dispose(); playback = null;
        }
        else
        {
            try { playback = new AudioPlayback(); AttachPlayback(); if (running) playback.Start(); }
            catch (Exception ex) when (ex is MmException or InvalidOperationException)
            { DisableFailedPlayback(ex); }
        }
        sound.Text = sound.Checked ? "Mute audio" : "Enable audio"; UpdateStatus();
    }
    void TryStartPlayback()
    {
        try { playback?.Start(); }
        catch (Exception ex) when (ex is MmException or InvalidOperationException) { DisableFailedPlayback(ex); }
    }
    void DisableFailedPlayback(Exception ex)
    {
        DetachPlayback(); playback?.Dispose(); playback = null; sound.Checked = false; sound.Text = "Enable audio";
        ShowError(ex);
    }
    void ToggleRecording()
    {
        if (machine is null) return;
        if (recording is not null) { StopRecording(); UpdateStatus(); return; }
        bool previous = running; SetRunning(false);
        using var dialog = new SaveFileDialog { Filter = "PCM WAV (*.wav)|*.wav", DefaultExt = "wav", AddExtension = true, OverwritePrompt = true };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                recordingStream = File.Create(dialog.FileName); recording = new WaveWriter(recordingStream);
                machine.Apu.SampleProduced += recording.WriteSample;
                record.Text = "Stop recording";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            { StopRecording(); ShowError(ex); }
        }
        SetRunning(previous);
    }
    void StopRecording()
    {
        if (recording is not null && machine is not null) machine.Apu.SampleProduced -= recording.WriteSample;
        try { recording?.Dispose(); }
        catch (IOException ex) { logger.LogError(ex, "Could not finalize WAV recording"); }
        finally { recordingStream?.Dispose(); recordingStream = null; recording = null; record.Text = "Record WAV"; }
    }
    void UpdateStatus() => status.Text = machine is null ? "Open a legally obtained iNES mapper-0/2/3 ROM. Game compatibility is not certified." :
        $"{romName} ({machine.Cartridge.MapperName}) | {(running ? "Running" : "Paused")} | frame {machine.Ppu.Frames:N0} | CPU {machine.CpuCycles:N0} | {(recording is null ? (playback is null ? "Audio muted" : "Audio enabled") : "Recording WAV")}";
    void ShowError(Exception ex) { logger.LogWarning(ex, "Desktop operation failed"); MessageBox.Show(this, ex.Message, "ThingNES", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    static Buttons MapKey(Keys key) => key switch
    {
        Keys.Z => Buttons.A, Keys.X => Buttons.B, Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey => Buttons.Select,
        Keys.Enter => Buttons.Start, Keys.Up => Buttons.Up, Keys.Down => Buttons.Down, Keys.Left => Buttons.Left, Keys.Right => Buttons.Right, _ => Buttons.None
    };
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.O)) { OpenRom(); return true; }
        if (keyData == Keys.F5) { SetRunning(!running); return true; }
        if (keyData == Keys.F10) { StepFrame(); return true; }
        if (keyData == Keys.F2) { PowerCycle(); return true; }
        Buttons button = MapKey(keyData & Keys.KeyCode);
        if (machine is not null && button != Buttons.None) { machine.Controller1.Pressed |= button; return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        Buttons button = MapKey(e.KeyCode);
        if (machine is not null && button != Buttons.None) { machine.Controller1.Pressed |= button; e.Handled = e.SuppressKeyPress = true; }
        base.OnKeyDown(e);
    }
    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (machine is not null) machine.Controller1.Pressed &= ~MapKey(e.KeyCode);
        base.OnKeyUp(e);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { timer.Stop(); timer.Dispose(); StopRecording(); DetachPlayback(); playback?.Dispose(); playback = null; }
        base.Dispose(disposing);
    }
}
