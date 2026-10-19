using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using ThingCommodore.Graphics;

namespace Thing64.WebStudio.Components.Memory;

// What the VIC-II is set to show, as memory-view.js reads it from the snapshot
public sealed record VicInfo(int Bank, int Screen, int Charset, bool CharsetIsRom, int Bitmap, bool BitmapMode, bool Multicolor, int Background,
    int Multicolor1, int Multicolor2, int SpriteMulticolor1, int SpriteMulticolor2, VicSprite[] Sprites);

public sealed record VicSprite(bool Enabled, int Color, bool Multicolor, int Address);

// The base of the viewers of the memory. A viewer is a div that memory-view.js fills (the bar to move, the bytes or the picture, the status
// line) and a side panel of settings in Razor. Moving, hovering and drawing happen in the browser; a setting that changes is sent over with
// PushAsync
public abstract class MemoryViewBase : ComponentBase, IAsyncDisposable
{
    [Inject] protected IJSRuntime JS { get; set; } = null!;

    // Whether the tab of this viewer is the one in front
    [Parameter] public bool Active { get; set; }

    protected ElementReference Host;
    protected bool Mounted;
    protected string Message = "";

    // The "kind" of view memory-view.js makes: dump, charset, sprites, screen or bitmap
    protected abstract string Kind { get; }

    // The settings memory-view.js knows (see its defaults)
    protected abstract Dictionary<string, object?> Settings();

    // The settings that follow what the VIC-II has now (called when the viewer starts and on request)
    protected virtual void UseVic(VicInfo vic) { }

    protected int Zoom = 3;
    protected bool Grid = true;
    protected int Background = 6, Multicolor1 = 14, Multicolor2 = 1, Ink = 7;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || Mounted) return;
        Mounted = true;
        if (await GetVic() is { } vic) UseVic(vic);
        await JS.InvokeVoidAsync("thing64Mem.mount", Host, Kind, Settings());
        StateHasChanged();
    }

    protected Task<VicInfo?> GetVic() => JS.InvokeAsync<VicInfo?>("thing64Mem.info").AsTask();

    protected async Task PushAsync()
    {
        if (Mounted) await JS.InvokeVoidAsync("thing64Mem.update", Host, Settings());
    }

    // The settings, the offset and the colors of the VIC-II, in one go
    protected async Task AsVicAsync()
    {
        if (await GetVic() is not { } vic) return;
        UseVic(vic);
        await JS.InvokeVoidAsync("thing64Mem.update", Host, Settings());
    }

    protected async Task CopyAsync(string what) =>
        Message = await JS.InvokeAsync<string>("thing64Mem.copy", Host, what);

    protected static string Swatch(int color) => "#" + C64Palette.Pepto.Rgb[color].ToString("X6");

    public async ValueTask DisposeAsync()
    {
        if (!Mounted) return;
        try { await JS.InvokeVoidAsync("thing64Mem.dispose", Host); }
        catch (JSDisconnectedException) { }
    }
}
