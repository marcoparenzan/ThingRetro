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
}
