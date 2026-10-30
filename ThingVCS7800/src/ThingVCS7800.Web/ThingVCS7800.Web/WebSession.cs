using ThingVCS;
using ThingVCS7800.A7800;
using ThingRetroEmu.Video;
using ThingRetroEmu.Ports;
namespace ThingVCS7800.Web;

public sealed class WebSession : IDisposable
{
    public DevelopmentComputer Computer { get; } = new();
    readonly BufferedMonitor monitor = new();
    readonly MonitorCable cable;
    readonly Joystick joystick = new();
    public string Title { get; private set; } = "No cartridge";
    public byte[] Frame => monitor.Rgba();
    public int Width => monitor.Width;
    public int Height => monitor.Height;
    public WebSession() { cable = new(Computer.Board.Video, monitor); Computer.Board.ControlPort1.Plug(joystick); }
    public void Load(byte[] bytes, string name, bool corrections)
    {
        if (bytes.Length > 1024*1024*16+128) throw new ArgumentException("Cartridge exceeds 16 MiB.");
        bool a78 = bytes.Length >= 128 && bytes.AsSpan(1,9).SequenceEqual("ATARI7800"u8);
        // Only fingerprint-confirmed RAM correction is enabled; no broad unknown-header fallback.
        Computer.InsertImage(bytes,a78,allowKnownRamCorrection: corrections && CartridgeImage.HasKnownMissingRam(bytes));
        Title=name; Release(); Present();
    }
    public void Demo() { Computer.Insert(DevelopmentRoms.DirectObjects()); Title="Direct-object demo"; Present(); }
    public void Step()
    {
        if (!Computer.HasCartridge) return;
        var maria=Computer.Board.MariaSocket.Chip!; long target=maria.Frames+1;
        int limit=200000;
        while(maria.Frames<target && limit-->0) Computer.Board.Tick();
        if(limit<=0) throw new InvalidOperationException("Frame watchdog reached (MARIA disabled or 2600 mode).");
    }
    public void Present() => Computer.Board.Present(Computer.Board.MariaSocket.Chip!);
    public void Key(string code,bool down)
    {
        switch(code) { case "ArrowUp":joystick.Up=down;break;case "ArrowDown":joystick.Down=down;break;
        case "ArrowLeft":joystick.Left=down;break;case "ArrowRight":joystick.Right=down;break;
        case "ControlLeft":case "ControlRight":joystick.Fire=down;break;case "ShiftLeft":case "ShiftRight":joystick.Fire2=down;break; }
    }
    public void Release()=>joystick.ReleaseAll();
    public void Dispose() { cable.Dispose(); monitor.Dispose(); Release(); }
}
