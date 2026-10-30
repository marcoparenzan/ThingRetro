using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ThingRetroEmu.Audio;
using ThingRetroEmu.Video;
using ThingRetroEmu.WinForms;
namespace ThingNES.Computer;

/// <summary>Thing64-style Computer: common monitor/cables, Peripherals/Video and screen context menu.</summary>
public sealed class ComputerForm : Form
{
    readonly ILogger<ComputerForm> logger;
    readonly MonitorControl monitor = new() { Dock = DockStyle.Fill };
    readonly MenuStrip menuBar = new();
    readonly System.Windows.Forms.Timer timer = new() { Interval = 8 };
    readonly Stopwatch clock = new();
    readonly FramePacer pacer = new();
    readonly AudioSplitter splitter = new();
    readonly PcSpeakers? speakers;
    VideoMenu video;
    AudioCable? audioCable;
    WaveRecorder? recorder;
    NesMachine? machine;
    byte[]? image;
    string romName = "";
    bool running;

    public ComputerForm(ILogger<ComputerForm> logger)
    {
        this.logger=logger; Text="ThingNES"; StartPosition=FormStartPosition.CenterScreen; KeyPreview=true;
        Controls.Add(monitor); MainMenuStrip=menuBar; Controls.Add(menuBar);
        video=new VideoMenu(new VideoPort(),monitor);
        try { speakers=new PcSpeakers { Muted=true }; splitter.Connect(speakers); }
        catch(InvalidOperationException ex) { logger.LogWarning(ex,"Audio device unavailable"); }
        BuildMenus(); ClientSize=new Size(monitor.Width,monitor.Height+menuBar.PreferredSize.Height);
        monitor.AllowDrop=true;
        monitor.DragEnter+=(_,e)=>{ if(DroppedFile(e) is not null)e.Effect=DragDropEffects.Copy; };
        monitor.DragDrop+=(_,e)=>{ if(DroppedFile(e) is string path)Open(path); };
        timer.Tick+=(_,_)=>ScheduledFrames();
        Deactivate+=(_,_)=>Release(); monitor.LostFocus+=(_,_)=>Release();
        Shown+=(_,_)=>monitor.Focus(); Activated+=(_,_)=>monitor.Focus();
    }
    void BuildMenus()
    {
        foreach(ToolStripItem item in menuBar.Items.Cast<ToolStripItem>().ToArray())item.Dispose(); menuBar.Items.Clear();
        var peripherals=new ToolStripMenuItem("Peripherals"); AddPeripherals(peripherals.DropDownItems);
        menuBar.Items.Add(peripherals);menuBar.Items.Add(video.CreateItem());
        monitor.ContextMenuStrip?.Dispose(); var context=new ContextMenuStrip();
        context.Items.Add(new ToolStripMenuItem("NES controller 1: Z=A, X=B, Shift=Select, Enter=Start, arrows") { Enabled=false });
        var sound=new ToolStripMenuItem("Audio") { CheckOnClick=true, Enabled=speakers is not null, Checked=speakers is not null&&!speakers.Muted };
        sound.CheckedChanged+=(_,_)=>{ if(speakers is not null)speakers.Muted=!sound.Checked; }; context.Items.Add(sound);
        AddPeripherals(context.Items);context.Items.Add(video.CreateItem());
        var pause=new ToolStripMenuItem("Pause",null,(_,_)=>SetRunning(!running)) { ShortcutKeys=Keys.F5 };
        context.Items.Add(pause);
        context.Items.Add(new ToolStripMenuItem("Step frame",null,(_,_)=>StepFrame()) { ShortcutKeys=Keys.F10 });
        context.Items.Add(new ToolStripMenuItem("Reset / power cycle",null,(_,_)=>Reset()) { ShortcutKeys=Keys.F2 });
        context.Opening+=(_,_)=>{ pause.Checked=!running;pause.Enabled=machine is not null; };
        monitor.ContextMenuStrip=context;
    }
    void AddPeripherals(ToolStripItemCollection items)
    {
        var cartridge=new ToolStripMenuItem("Cartridge");
        cartridge.DropDownItems.Add(new ToolStripMenuItem("Insert cartridge…",null,(_,_)=>ChooseRom()) { ShortcutKeys=Keys.Control|Keys.O });
        cartridge.DropDownItems.Add(new ToolStripMenuItem("Eject",null,(_,_)=>Eject()));items.Add(cartridge);
        var recording=new ToolStripMenuItem("Audio recording");
        var start=new ToolStripMenuItem("Start WAV recording…",null,(_,_)=>StartRecording());
        var stop=new ToolStripMenuItem("Stop and finalize WAV",null,(_,_)=>StopRecording());
        recording.DropDownItems.Add(start);recording.DropDownItems.Add(stop);
        recording.DropDownOpening+=(_,_)=>{start.Enabled=machine is not null&&recorder is null;stop.Enabled=recorder is not null;};items.Add(recording);
    }
    public void Open(string path)
    {
        bool previous=running;SetRunning(false);
        try
        {
            if(new FileInfo(path).Length>16*1024*1024)throw new InvalidDataException("ROM exceeds the 16 MiB safety limit.");
            byte[] bytes=File.ReadAllBytes(path);var next=new NesMachine(Cartridge.Load(bytes));
            next.RunFrame();next.RunFrame();
            StopRecording();audioCable?.Dispose();video.Dispose();
            machine=next;image=bytes;romName=Path.GetFileName(path);
            video=new VideoMenu(next.Video,monitor);audioCable=new AudioCable(next.Audio,splitter);
            BuildMenus();next.Video.Present(next.VideoSource);SetRunning(true);
            logger.LogInformation("Inserted {Rom} ({Mapper})",romName,next.Cartridge.MapperName);
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or NotSupportedException or UnauthorizedAccessException or ArgumentException)
        { ShowError(ex,path);SetRunning(previous); }
        monitor.Focus();
    }
    public void LoadRom(string path)=>Open(path);
    void ChooseRom()
    {
        bool previous=running;SetRunning(false);using var dialog=new OpenFileDialog { Filter="NES ROM (*.nes)|*.nes",CheckFileExists=true };
        if(dialog.ShowDialog(this)==DialogResult.OK) {SetRunning(previous);Open(dialog.FileName);} else SetRunning(previous);
    }
    void SetRunning(bool value)
    {
        running=value&&machine is not null;pacer.Reset();
        if(running){clock.Restart();timer.Start();}else{clock.Reset();timer.Stop();}
        UpdateTitle();
    }
    void ScheduledFrames()
    {
        if(!running)return;TimeSpan elapsed=clock.Elapsed;clock.Restart();
        int due=pacer.Advance(elapsed);for(int i=0;i<due&&running;i++)AdvanceFrame();
    }
    void StepFrame(){SetRunning(false);if(machine is not null)AdvanceFrame();}
    void AdvanceFrame()
    {
        try {machine?.RunFrame();UpdateTitle();}
        catch(Exception ex) when(ex is IOException or InvalidOperationException){SetRunning(false);StopRecording();ShowError(ex);}
    }
    public void Reset()
    {
        if(image is null)return;bool previous=running;SetRunning(false);StopRecording();
        audioCable?.Dispose();video.Dispose();
        machine=new NesMachine(Cartridge.Load(image));video=new VideoMenu(machine.Video,monitor);audioCable=new AudioCable(machine.Audio,splitter);
        machine.RunFrame();machine.RunFrame();BuildMenus();machine.Video.Present(machine.VideoSource);SetRunning(previous);monitor.Focus();
    }
    void Eject()
    {
        SetRunning(false);StopRecording();Release();audioCable?.Dispose();audioCable=null;video.Dispose();machine=null;image=null;romName="";
        video=new VideoMenu(new VideoPort(),monitor);BuildMenus();UpdateTitle();
    }
    void StartRecording()
    {
        if(machine is null||recorder is not null)return;bool previous=running;SetRunning(false);
        using var dialog=new SaveFileDialog {Filter="PCM WAV (*.wav)|*.wav",DefaultExt="wav",AddExtension=true,OverwritePrompt=true};
        if(dialog.ShowDialog(this)==DialogResult.OK)
        {
            Stream? stream=null;
            try {stream=File.Create(dialog.FileName);recorder=new WaveRecorder(stream);stream=null;splitter.Connect(recorder);}
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException){StopRecording();ShowError(ex,dialog.FileName);}
            finally{stream?.Dispose();}
        }
        SetRunning(previous);
    }
    void StopRecording()
    {
        var active=recorder;recorder=null;if(active is null)return;splitter.Disconnect(active);
        try{active.Dispose();}catch(IOException ex){ShowError(ex);}UpdateTitle();
    }
    void UpdateTitle()=>Text=machine is null?"ThingNES — insert cartridge":$"ThingNES — {romName} / {machine.Cartridge.MapperName} — {(running?"Running":"Paused")} — frame {machine.Ppu.Frames}{(recorder is null?"":" — REC WAV")}";
    void ShowError(Exception ex,string? path=null)
    {
        logger.LogWarning(ex,"Computer operation failed");
        string? fullPath=path;
        try { if(path is not null) fullPath=Path.GetFullPath(path); }
        catch(Exception pathError) when(pathError is ArgumentException or NotSupportedException or IOException) { }
        string details=(fullPath is null?"":$"File: {fullPath}\n\n")+ex;
        try{Clipboard.SetText(details);}catch(System.Runtime.InteropServices.ExternalException){}
        MessageBox.Show(this,ex.Message,"ThingNES",MessageBoxButtons.OK,MessageBoxIcon.Warning);
    }
    static string? DroppedFile(DragEventArgs e)=>e.Data?.GetData(DataFormats.FileDrop) is string[] files&&files.Length==1&&Path.GetExtension(files[0]).Equals(".nes",StringComparison.OrdinalIgnoreCase)?files[0]:null;
    static Buttons MapKey(Keys key)=>key switch{Keys.Z=>Buttons.A,Keys.X=>Buttons.B,Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey=>Buttons.Select,Keys.Enter=>Buttons.Start,Keys.Up=>Buttons.Up,Keys.Down=>Buttons.Down,Keys.Left=>Buttons.Left,Keys.Right=>Buttons.Right,_=>Buttons.None};
    void Release(){if(machine is not null)machine.Controller1.Pressed=Buttons.None;}
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData)
    {
        if(keyData==(Keys.Control|Keys.O)){ChooseRom();return true;}if(keyData==Keys.F5){SetRunning(!running);return true;}if(keyData==Keys.F10){StepFrame();return true;}if(keyData==Keys.F2){Reset();return true;}
        var button=MapKey(keyData&Keys.KeyCode);if(machine is not null&&button!=Buttons.None){machine.Controller1.Pressed|=button;return true;}
        return base.ProcessCmdKey(ref msg,keyData);
    }
    protected override void OnKeyDown(KeyEventArgs e){var button=MapKey(e.KeyCode);if(machine is not null&&button!=Buttons.None){machine.Controller1.Pressed|=button;e.Handled=e.SuppressKeyPress=true;}base.OnKeyDown(e);}
    protected override void OnKeyUp(KeyEventArgs e){if(machine is not null)machine.Controller1.Pressed&=~MapKey(e.KeyCode);base.OnKeyUp(e);}
    protected override void Dispose(bool disposing)
    {
        if(disposing){timer.Stop();timer.Dispose();StopRecording();Release();audioCable?.Dispose();video.Dispose();speakers?.Dispose();}
        base.Dispose(disposing);
    }
}
