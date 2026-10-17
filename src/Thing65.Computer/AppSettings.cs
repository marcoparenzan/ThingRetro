using System.Text.Json;
using System.Text.Json.Serialization;
using Thing65.WinForms;

namespace Thing65.Computer;

// App settings kept between runs, in %AppData%\Thing65\settings.json. One shared instance (Current):
// every window changes it and saves it, so nobody overwrites the others' values
public sealed class AppSettings
{
    // Default: keypad joystick in port 2, the port almost every game uses
    public ControlPortChoice Port1 { get; set; } = ControlPortChoice.None;
    public ControlPortChoice Port2 { get; set; } = new(ControlPortSource.KeypadJoystick);
    public bool Audio { get; set; } = true;

    public static AppSettings Current { get; } = Load();

    static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Thing65", "settings.json");

    static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options) ?? new();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { }
        return new(); // missing or unreadable file: defaults
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Options));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } // never stop the emulator for this
    }
}
