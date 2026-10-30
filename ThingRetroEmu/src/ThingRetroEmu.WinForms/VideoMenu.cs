using ThingRetroEmu.Video;

namespace ThingRetroEmu.WinForms;

/// <summary>
/// The monitor cable of a window, with a menu to change it: RGB (the picture as it is made) or composite PAL / NTSC (the picture goes out as a signal and the
/// monitor decodes it, with its colour bleeding)
/// </summary>
public sealed class VideoMenu : IDisposable
{
    readonly VideoPort port;
    readonly IMonitor monitor;
    MonitorCable cable;
    string mode = "RGB";

    readonly bool allowSystemSelection;
    public VideoMenu(VideoPort port, IMonitor monitor, bool allowSystemSelection = false)
    {
        this.allowSystemSelection = allowSystemSelection;
        this.port = port;
        this.monitor = monitor;
        cable = new MonitorCable(port, monitor);
    }

    /// <summary>"RGB", "PAL" or "NTSC".</summary>
    public string Mode => mode;

    public void Select(string newMode)
    {
        if (newMode == mode) return;
        cable.Dispose();
        switch (newMode)
        {
            case "AUTO":
                port.Standard = null;
                cable = new MonitorCable(port, new CompositeTuner(monitor));
                break;
            case "PAL":
                port.Standard = VideoStandard.Pal;
                cable = new MonitorCable(port, new CompositeTuner(monitor));
                break;
            case "NTSC":
                port.Standard = VideoStandard.Ntsc;
                cable = new MonitorCable(port, new CompositeTuner(monitor));
                break;
            default:
                newMode = "RGB";
                port.Standard = null;
                cable = new MonitorCable(port, monitor);
                break;
        }
        mode = newMode;
    }

    public ToolStripMenuItem CreateItem()
    {
        var menu = new ToolStripMenuItem("Video");
        foreach (var (label, value) in new[] { ("RGB", "RGB"), ("Composite Auto", "AUTO"), ("Composite PAL", "PAL"), ("Composite NTSC", "NTSC") })
        {
            var item = new ToolStripMenuItem(label, null, (_, _) => Select(value)) { Tag = value };
            menu.DropDownItems.Add(item);
        }
        menu.DropDownOpening += (_, _) =>
        {
            foreach (var item in menu.DropDownItems.OfType<ToolStripMenuItem>())
                if (item.Tag is string value) item.Checked = value == mode;
        };
        if (allowSystemSelection)
        {
            menu.DropDownItems.Add(new ToolStripSeparator());
            var systems = new ToolStripMenuItem("Console standard (change resets machine)");
            foreach (var system in Enum.GetValues<VideoSystem>())
            {
                var item = new ToolStripMenuItem(system.ToString().ToUpperInvariant(), null,
                    (_, _) => port.SystemSelection = system) { Tag = system };
                systems.DropDownItems.Add(item);
            }
            systems.DropDownOpening += (_, _) =>
            {
                foreach (ToolStripMenuItem item in systems.DropDownItems)
                    item.Checked = (VideoSystem)item.Tag! == port.SystemSelection;
            };
            menu.DropDownItems.Add(systems);
        }
        return menu;
    }

    public void Dispose() => cable.Dispose();
}
