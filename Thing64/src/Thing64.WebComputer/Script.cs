using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace Thing64.WebComputer;

// The functions of wwwroot/js/computer.js called from .NET (module "computer", imported by App.razor)
[SupportedOSPlatform("browser")]
static partial class Script
{
    // Starts the animation loop and the keyboard on the canvas
    [JSImport("start", "computer")]
    public static partial Task Start(string canvasId);

    [JSImport("stop", "computer")]
    public static partial void Stop();

    [JSImport("focus", "computer")]
    public static partial void Focus(string elementId);

    // ROMs kept by the browser (localStorage) between visits
    [JSImport("storedRom", "computer")]
    public static partial byte[]? StoredRom(string name);

    [JSImport("storeRom", "computer")]
    public static partial void StoreRom(string name, byte[] content);

    // Small things the page remembers between visits (which drive is plugged in)
    [JSImport("setting", "computer")]
    public static partial string? Setting(string name);

    [JSImport("setSetting", "computer")]
    public static partial void SetSetting(string name, string value);

    // A file for the person to keep (a disk that was written to)
    [JSImport("download", "computer")]
    public static partial void Download(string name, byte[] content);
}
