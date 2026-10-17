namespace Thing65.Computer;

// The Thing65 icon (images/thing65.ico, embedded in this assembly) for the windows of the desktop app
public static class AppIcon
{
    static readonly Lazy<Icon?> icon = new(() =>
    {
        using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream("thing65.ico");
        return stream is null ? null : new Icon(stream);
    });

    public static Icon? Value => icon.Value;
}
