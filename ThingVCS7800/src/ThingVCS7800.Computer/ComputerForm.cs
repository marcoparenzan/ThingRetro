using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ThingRetroEmu.Audio;
using ThingRetroEmu.WinForms;
using ThingVCS7800.A7800;

namespace ThingVCS7800.Computer;

// Same window organization as Thing64.Computer: monitor Fill, Peripherals/Video menu,
// right-click menu on monitor, explicit video/audio cables, startup file and drag/drop.
public sealed class ComputerForm : Form
{
    readonly DevelopmentComputer computer = new();
    readonly MonitorControl monitor = new() { Dock = DockStyle.Fill };
    readonly VideoMenu video;
    readonly ControlPortSelector controlPorts;
    readonly PcSpeakers? speakers;
    readonly AudioCable audioCable;
    readonly AudioSplitter audioSplitter = new();
    WaveRecorder? recorder;
    string? recordingPath;
    readonly List<ToolStripMenuItem> recordStartItems = [];
    readonly List<ToolStripMenuItem> recordStopItems = [];
    readonly System.Windows.Forms.Timer timer = new() { Interval = 10 };
    readonly Stopwatch clock = new();
    readonly ILogger<ComputerForm> logger;
    readonly List<ToolStripMenuItem> pauseItems = [];
    TimeSpan last;
    string? cartridgeName;

    public ComputerForm(ILogger<ComputerForm> logger, string? startupFile = null)
    {
        this.logger = logger;
        Text = "ThingVCS7800 — development board (MARIA direct 160A / TIA audio pending)";
        StartPosition = FormStartPosition.CenterScreen;
        Controls.Add(monitor);
        video = new VideoMenu(computer.Board.Video, monitor, allowSystemSelection: true);
        computer.Board.Video.SystemChanged += () => { StopRecording(); last = clock.Elapsed; UpdateTitle(); };
        try
        {
            speakers = new PcSpeakers { Muted = true };
            audioSplitter.Connect(speakers);
        }
        catch (InvalidOperationException ex) { logger.LogWarning(ex, "Audio device unavailable"); }
        audioCable = new AudioCable(computer.Board.Audio, audioSplitter);

        var menuBar = new MenuStrip();
        var peripheralMenu = new ToolStripMenuItem("Peripherals");
        AddPeripherals(peripheralMenu.DropDownItems);
        menuBar.Items.Add(peripheralMenu);
        menuBar.Items.Add(video.CreateItem());
        MainMenuStrip = menuBar;
        Controls.Add(menuBar);
        ClientSize = new Size(monitor.Width, monitor.Height + menuBar.PreferredSize.Height);

        var context = new ContextMenuStrip();
        controlPorts = new ControlPortSelector(computer.Board.ControlPort1, computer.Board.ControlPort2, monitor, allowAnalogDevices: false, twoButtons: true);
        controlPorts.Select(1, new(ControlPortSource.ArrowsJoystick));
        controlPorts.AddMenuItems(context.Items);
        context.Items.Add(new ToolStripMenuItem("7800 joystick — Ctrl/Shift fire 1/2; keypad 0/Decimal; USB buttons 1/2") { Enabled = false });
        context.Items.Add(new ToolStripSeparator());
        var audioItem = new ToolStripMenuItem("Enable audio — TIA / POKEY functional") { Enabled = speakers is not null };
        audioItem.Click += (_, _) => { if (speakers is not null) { speakers.Muted = !speakers.Muted; audioItem.Checked = !speakers.Muted; } };
        context.Items.Add(audioItem);
        AddPeripherals(context.Items);
        context.Items.Add(video.CreateItem());
        AddPause(context.Items);
        context.Items.Add(new ToolStripMenuItem("Reset", null, (_, _) => Reset()) { ShortcutKeys = Keys.F2 });
        monitor.ContextMenuStrip = context;

        monitor.AllowDrop = true;
        monitor.DragEnter += (_, e) => { if (DroppedFile(e) is not null) e.Effect = DragDropEffects.Copy; };
        monitor.DragDrop += (_, e) => { if (DroppedFile(e) is string path) Open(path); };
        timer.Tick += (_, _) =>
        {
            var now = clock.Elapsed;
            try { computer.Advance(now - last); }
            catch (Exception ex) when (recorder is not null && ex is IOException or UnauthorizedAccessException)
            {
                ex.Data["WAV path"] = recordingPath;
                StopRecording(); ReportRecordingError(ex);
            }
            last = now;
        };
        Shown += (_, _) =>
        {
            monitor.Focus(); clock.Start(); last = clock.Elapsed; timer.Start();
            if (startupFile is not null) Open(startupFile);
        };
        Activated += (_, _) => monitor.Focus();
        KeyPreview = true;
        KeyDown += (_, e) => SetConsoleKey(e, true);
        KeyUp += (_, e) => SetConsoleKey(e, false);
        Deactivate += (_, _) => computer.Board.Console.ReleaseButtons();
    }

    void AddPeripherals(ToolStripItemCollection items)
    {
        var recording = new ToolStripMenuItem("Audio recording");
        var start = new ToolStripMenuItem("Start WAV recording…", null, (_, _) => StartRecording());
        var stop = new ToolStripMenuItem("Stop and finalize WAV", null, (_, _) => StopRecording());
        recordStartItems.Add(start); recordStopItems.Add(stop);
        recording.DropDownItems.Add(start); recording.DropDownItems.Add(stop);
        recording.DropDownOpening += (_, _) => SyncRecording();
        items.Add(recording); SyncRecording();
        var bios = new ToolStripMenuItem("BIOS / boot");
        bios.DropDownItems.Add(new ToolStripMenuItem("Install BIOS (4/16 KiB)…", null, (_, _) => ChooseBios()));
        bios.DropDownItems.Add(new ToolStripMenuItem("Remove BIOS / direct cartridge boot", null, (_, _) =>
        { StopRecording(); computer.RemoveBios(); last = clock.Elapsed; UpdateTitle(); }));
        items.Add(bios);
        var cartridge = new ToolStripMenuItem("Cartridge");
        cartridge.DropDownItems.Add(new ToolStripMenuItem("Insert cartridge…", null, (_, _) => ChooseRom()) { ShortcutKeys = Keys.Control | Keys.O });
        cartridge.DropDownItems.Add(new ToolStripMenuItem("Insert TIA tone/noise test ROM", null, (_, _) =>
        {
            StopRecording();
            computer.Insert(ThingVCS.DevelopmentRoms.TiaSound());
            cartridgeName = "TIA tone/noise test ROM"; last = clock.Elapsed;
            UpdateTitle(); SyncPause(); monitor.Focus();
        }));
        cartridge.DropDownItems.Add(new ToolStripMenuItem("Insert backdrop test ROM", null, (_, _) =>
        {
            StopRecording();
            computer.Insert(ThingVCS.DevelopmentRoms.Backdrop());
            cartridgeName = "Backdrop test ROM"; last = clock.Elapsed;
            UpdateTitle(); SyncPause(); monitor.Focus();
        }));
        cartridge.DropDownItems.Add(new ToolStripMenuItem("Insert direct-object test ROM", null, (_, _) =>
        {
            StopRecording();
            computer.Insert(ThingVCS.DevelopmentRoms.DirectObjects());
            cartridgeName = "Direct-object test ROM"; last = clock.Elapsed;
            UpdateTitle(); SyncPause(); monitor.Focus();
        }));
        cartridge.DropDownItems.Add(new ToolStripMenuItem("Insert indirect-character test ROM", null, (_, _) =>
        {
            StopRecording();
            computer.Insert(ThingVCS.DevelopmentRoms.IndirectObjects());
            cartridgeName = "Indirect-character test ROM"; last = clock.Elapsed;
            UpdateTitle(); SyncPause(); monitor.Focus();
        }));
        cartridge.DropDownItems.Add(new ToolStripMenuItem("Insert 160B test ROM", null, (_, _) =>
        {
            StopRecording();
            computer.Insert(ThingVCS.DevelopmentRoms.WriteModeObjects());
            cartridgeName = "160B test ROM"; last = clock.Elapsed;
            UpdateTitle(); SyncPause(); monitor.Focus();
        }));
        cartridge.DropDownItems.Add(new ToolStripMenuItem("Insert DLI test ROM", null, (_, _) =>
        {
            StopRecording();
            computer.Insert(ThingVCS.DevelopmentRoms.DisplayListInterrupts());
            cartridgeName = "DLI test ROM"; last = clock.Elapsed;
            UpdateTitle(); SyncPause(); monitor.Focus();
        }));
        cartridge.DropDownItems.Add(new ToolStripMenuItem("Insert 320C test ROM", null, (_, _) =>
        {
            StopRecording();
            computer.Insert(ThingVCS.DevelopmentRoms.WriteModeObjects(3));
            cartridgeName = "320C test ROM"; last = clock.Elapsed;
            UpdateTitle(); SyncPause(); monitor.Focus();
        }));
        var eject = new ToolStripMenuItem("Eject", null, (_, _) =>
        {
            StopRecording(); computer.Eject(); cartridgeName = null; UpdateTitle(); SyncPause(); monitor.Focus();
        });
        cartridge.DropDownItems.Add(eject);
        cartridge.DropDownOpening += (_, _) => eject.Enabled = computer.HasCartridge;
        items.Add(cartridge);
        AddConsole(items);
    }
    void SetConsoleKey(KeyEventArgs e, bool pressed)
    {
        var contacts = computer.Board.Console;
        switch (e.KeyCode)
        {
            case Keys.F6: contacts.ResetPressed = pressed; break;
            case Keys.F7: contacts.SelectPressed = pressed; break;
            case Keys.F8: contacts.PausePressed = pressed; break;
            default: return;
        }
        e.Handled = true; e.SuppressKeyPress = true;
    }
    void AddConsole(ToolStripItemCollection items)
    {
        var menu = new ToolStripMenuItem("Console switches");
        var contacts = computer.Board.Console;
        void AddContact(string label, Func<bool> read, Action<bool> write)
        {
            var item = new ToolStripMenuItem(label, null, (_, _) => write(!read()));
            menu.DropDownItems.Add(item);
            menu.DropDownOpening += (_, _) => item.Checked = read();
        }
        // Menu entries deliberately hold contacts until toggled off; keyboard contacts are momentary.
        AddContact("Hold Reset (F6 while held)", () => contacts.ResetPressed, value => contacts.ResetPressed = value);
        AddContact("Hold Select (F7 while held)", () => contacts.SelectPressed, value => contacts.SelectPressed = value);
        AddContact("Hold Pause (F8 while held)", () => contacts.PausePressed, value => contacts.PausePressed = value);
        menu.DropDownItems.Add(new ToolStripMenuItem("Release buttons", null, (_, _) => contacts.ReleaseButtons()));
        menu.DropDownItems.Add(new ToolStripSeparator());
        AddContact("Left difficulty A (unchecked: B)", () => contacts.LeftDifficultyA, value => contacts.LeftDifficultyA = value);
        AddContact("Right difficulty A (unchecked: B)", () => contacts.RightDifficultyA, value => contacts.RightDifficultyA = value);
        items.Add(menu);
    }
    void AddPause(ToolStripItemCollection items)
    {
        var pause = new ToolStripMenuItem("Pause", null, (_, _) =>
        {
            computer.Paused = !computer.Paused; last = clock.Elapsed; SyncPause();
        }) { ShortcutKeys = Keys.F5 };
        pauseItems.Add(pause); items.Add(pause); SyncPause();
    }
    void SyncPause()
    {
        foreach (var item in pauseItems) { item.Checked = computer.Paused; item.Enabled = computer.HasCartridge; }
    }
    void SyncRecording()
    {
        foreach (var item in recordStartItems) item.Enabled = recorder is null && computer.HasCartridge;
        foreach (var item in recordStopItems) item.Enabled = recorder is not null;
    }
    void StartRecording()
    {
        if (recorder is not null || !computer.HasCartridge) return;
        timer.Stop();
        try
        {
            using var dialog = new SaveFileDialog { Filter = "PCM WAV (*.wav)|*.wav", DefaultExt = "wav", AddExtension = true, OverwritePrompt = true };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            string path = Path.GetFullPath(dialog.FileName);
            Stream? stream = null;
            try
            {
                stream = File.Create(path);
                var pending = new WaveRecorder(stream, audioSplitter.SampleRate);
                stream = null; // Recorder owns the stream from here.
                audioSplitter.Connect(pending); recorder = pending; recordingPath = path;
                logger.LogInformation("Recording WAV {Path}", path); UpdateTitle(); SyncRecording();
            }
            finally { stream?.Dispose(); }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { ReportRecordingError(ex); }
        finally { last = clock.Elapsed; timer.Start(); }
    }
    void StopRecording()
    {
        var active = recorder;
        if (active is null) return;
        recorder = null; audioSplitter.Disconnect(active);
        try { active.Dispose(); logger.LogInformation("Finalized WAV {Path}", recordingPath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { ReportRecordingError(ex); }
        finally { recordingPath = null; SyncRecording(); UpdateTitle(); }
    }
    void ReportRecordingError(Exception ex)
    {
        if (!ex.Data.Contains("WAV path")) ex.Data["WAV path"] = recordingPath ?? "(not opened)";
        logger.LogWarning(ex, "WAV recording failed");
        bool copied = ExceptionClipboard.TryCopy(ex);
        MessageBox.Show(this, ex.Message + (copied ? "\n\nDettagli copiati negli appunti." : ""),
            "ThingVCS7800 — WAV recording", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
    void ChooseBios()
    {
        using var dialog = new OpenFileDialog { Filter = "BIOS ROM (*.bin;*.rom)|*.bin;*.rom", CheckFileExists = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        string path = Path.GetFullPath(dialog.FileName);
        try
        {
            if (new FileInfo(path).Length is not (4096 or 16384)) throw new InvalidDataException("BIOS must be 4 or 16 KiB.");
            byte[] bytes = File.ReadAllBytes(path);
            _ = new ThingVCS.BootRom(bytes); // Validate before stopping recording/replacing firmware.
            StopRecording(); computer.InstallBios(bytes); last = clock.Elapsed; UpdateTitle(); monitor.Focus();
            logger.LogInformation("Installed BIOS {Path}; boot enabled on reset", path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            ex.Data["BIOS absolute path"] = path;
            bool copied = ExceptionClipboard.TryCopy(ex);
            MessageBox.Show(this, $"BIOS: {path}\n\n{ex.Message}" + (copied ? "\n\nDettagli copiati negli appunti." : ""),
                "ThingVCS7800 — BIOS", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
    void ChooseRom()
    {
        using var dialog = new OpenFileDialog { Filter = "Atari cartridges (*.a78;*.bin;*.rom)|*.a78;*.bin;*.rom", CheckFileExists = true };
        if (dialog.ShowDialog(this) == DialogResult.OK) Open(dialog.FileName);
    }
    public void Open(string path)
    {
        try
        {
            bool a78 = Path.GetExtension(path).Equals(".a78", StringComparison.OrdinalIgnoreCase);
            long size = new FileInfo(path).Length;
            // Allocation safety only. Cartridge layout/size validation belongs to CartridgeImage.
            const long maximumFileBytes = 16 * 1024 * 1024 + 128;
            if (size > maximumFileBytes)
                throw new NotSupportedException($"Cartridge file is {size} bytes; the file-reading safety limit is {maximumFileBytes} bytes (16 MiB plus header).");
            var bytes = File.ReadAllBytes(path);
            bool legacyFallback = false;
            if (a78 && bytes.Length >= 128 && bytes[53] == 5 && bytes[54] == 2)
            {
                if (MessageBox.Show(this,
                    "Header A78 $0502 non riconosciuto. Tentare SuperGame $0002 ignorando i bit alti, come fallback legacy MAME?\n\nIl file non verrà modificato. Non garantisce la corretta mappatura o il funzionamento del gioco.",
                    "ThingVCS7800 — compatibilità header", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
                legacyFallback = true;
            }
            bool unknownControllers = a78 && bytes.Length >= 128 && (bytes[55] > 2 || bytes[56] > 2);
            if (unknownControllers && MessageBox.Show(this,
                $"Controller non riconosciuti: porta 1 = ${bytes[55]:X2}, porta 2 = ${bytes[56]:X2}.\nTentare l'avvio con queste porte scollegate?\n\nIl file non verrà modificato. Non garantisce la giocabilità e non associa i valori alla Light Gun.",
                "ThingVCS7800 — controller sconosciuti", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            bool absoluteOverride = a78 && bytes.Length >= 128 && bytes[53] == 3 && bytes[54] == 0;
            if (absoluteOverride && MessageBox.Show(this,
                "Header ambiguo $0300: entrambi i bit dei mapper speciali sono attivi. Tentare Absolute ($0200)?\n\nIl file non verrà modificato. La scelta non viene dedotta dal nome della ROM e potrebbe essere errata.",
                "ThingVCS7800 — override Absolute", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            bool unknownSave = a78 && bytes.Length >= 128 && bytes[58] > 2;
            if (unknownSave && MessageBox.Show(this,
                $"Campo salvataggio non riconosciuto: byte 58 = ${bytes[58]:X2}.\nTentare l'avvio senza periferiche di salvataggio?\n\nIl file non verrà modificato. Nessuna memoria persistente o periferica verrà simulata.",
                "ThingVCS7800 — metadati salvataggio", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            bool mirroredRamOverride = a78 && bytes.Length == 32768 + 128 && bytes[53] == 0 && bytes[54] == 6;
            if (mirroredRamOverride && MessageBox.Show(this,
                "ROM da 32 KiB con flag SuperGame RAM $0006. Tentare ROM lineare con RAM specchiata ($0080, A8 scollegata)?\n\nÈ un override diagnostico: il file non verrà modificato e la scelta potrebbe non corrispondere alla scheda originale.",
                "ThingVCS7800 — override RAM specchiata", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            bool knownRamCorrection = a78 && ThingVCS.CartridgeImage.HasKnownMissingRam(bytes);
            if (knownRamCorrection && MessageBox.Show(this,
                "Il payload coincide esattamente con una ROM nel catalogo MAME (SHA1: Summer Games o Winter Games). La scheda richiede 16 KiB RAM, ma l'header indica SuperGame senza RAM ($0002).\n\nApplicare SuperGame RAM ($0006) per questa sessione? Il file non verrà modificato. Scegliendo No si conserva la cartuccia precedente.",
                "ThingVCS7800 — correzione scheda verificata", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            var image = ThingVCS.CartridgeImage.Parse(bytes, a78, legacyFallback, unknownControllers, absoluteOverride, unknownSave, mirroredRamOverride, knownRamCorrection);
            StopRecording();
            string title = computer.InsertImage(bytes, a78, legacyFallback, unknownControllers, absoluteOverride, unknownSave, mirroredRamOverride, knownRamCorrection);
            if (unknownSave) logger.LogWarning("Unknown save metadata ${Value:X2} preserved; no save peripheral connected", image.SavePeripheral);
            if (unknownControllers)
            {
                // Only detach the affected ports after successful image validation/insertion.
                if (image.Controller1Type > 2) computer.Board.ControlPort1.Unplug();
                if (image.Controller2Type > 2) computer.Board.ControlPort2.Unplug();
                logger.LogWarning("Unknown controller override: ports ${Port1:X2}/${Port2:X2}; affected ports disconnected", image.Controller1Type, image.Controller2Type);
            }
            if (image.UsesLegacyFallback)
                logger.LogWarning("Legacy A78 override: ${Original:X4} -> ${Effective:X4}", image.OriginalMapperFlags, image.MapperFlags);
            cartridgeName = $"{(string.IsNullOrWhiteSpace(title) ? Path.GetFileName(path) : title)} — {image.Size / 1024} KiB / {image.MapperName}";
            logger.LogInformation("Cartridge metadata: {Description}", image.Description);
            last = clock.Elapsed;
            UpdateTitle(); SyncPause();
            logger.LogInformation("Inserted {Cartridge}", cartridgeName);
            if (image.RequiresLightGun)
            {
                logger.LogWarning("Light Gun requested on ports {Port1}/{Port2}; raster adapter is not connected", image.Controller1Type, image.Controller2Type);
                MessageBox.Show(this, $"Controller: porta 1 = ${image.Controller1Type:X2}, porta 2 = ${image.Controller2Type:X2}.\nLa ROM usa una Light Gun. Il dispositivo ottico comune è disponibile, ma non è ancora collegato al raster Atari: mira e grilletto non funzionano nel Computer.",
                    "ThingVCS7800 — Light Gun non collegata", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            if (image.HighScoreCartridgeAbsent)
            {
                logger.LogWarning("High Score Cartridge metadata detected; peripheral is absent, scores will not be persisted");
                MessageBox.Show(this, "La ROM indica supporto High Score Cartridge. La periferica non è collegata: i punteggi non verranno salvati. Il gioco viene avviato senza questa periferica.",
                    "ThingVCS7800 — High Score Cartridge assente", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            logger.LogWarning(ex, "Cannot insert cartridge {Path}", path);
            try { ex.Data["ROM absolute path"] = Path.GetFullPath(path); }
            catch (Exception pathError) when (pathError is ArgumentException or NotSupportedException or IOException)
            {
                ex.Data["ROM absolute path"] = $"{path} (impossibile risolvere il percorso assoluto)";
            }
            bool copied = ExceptionClipboard.TryCopy(ex);
            MessageBox.Show(this, $"ROM file: {Path.GetFileName(path)}\nROM: {ex.Data["ROM absolute path"]}\n\n{ex.Message}" + (copied ? "\n\nDettagli completi copiati negli appunti." : "\n\nImpossibile copiare i dettagli negli appunti."), "ThingVCS7800", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        monitor.Focus();
    }
    public void Reset() { StopRecording(); computer.Reset(); last = clock.Elapsed; monitor.Focus(); }
    void UpdateTitle() => Text = $"ThingVCS7800{(cartridgeName is null ? "" : " — " + cartridgeName)} — {computer.Board.Timing.System.ToString().ToUpperInvariant()} ({computer.Board.Video.SystemSelection}) — development board (TIA audio functional){(recorder is null ? "" : " — REC WAV")}";
    static string? DroppedFile(DragEventArgs e) => e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length == 1 &&
        Path.GetExtension(files[0]).ToLowerInvariant() is ".a78" or ".bin" or ".rom" ? files[0] : null;

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        timer.Stop(); StopRecording(); timer.Dispose(); computer.Eject();
        controlPorts.Dispose();
        audioCable?.Dispose(); speakers?.Dispose(); video.Dispose();
        base.OnFormClosed(e);
    }
}
