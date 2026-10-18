namespace Thing64.Computer;

// The Thing64 icon (images/thing64.ico, embedded in this assembly) for the windows of the desktop app
public static class AppIcon
{
    static readonly Lazy<Icon?> icon = new(() =>
    {
        using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream("thing64.ico");
        return stream is null ? null : new Icon(stream);
    });

    public static Icon? Value => icon.Value;
}
