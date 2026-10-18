using System.Text.Json;
using System.Text.Json.Serialization;
using Thing64.WinForms;

namespace Thing64.Computer;

// App settings kept between runs, in %AppData%\Thing64\settings.json. One shared instance (Current):
// every window changes it and saves it, so nobody overwrites the others' values
public sealed class AppSettings
{
    // Default: keypad joystick in port 2, the port almost every game uses
    public ControlPortChoice Port1 { get; set; } = ControlPortChoice.None;
    public ControlPortChoice Port2 { get; set; } = new(ControlPortSource.KeypadJoystick);
    public bool Audio { get; set; } = true;

    public static AppSettings Current { get; } = Load();

    static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Thing64", "settings.json");

    static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    // Settings of the project's previous name (Fake64), read when there are none yet
    static readonly string OldFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Fake64", "settings.json");

    static AppSettings Load()
    {
        try
        {
            string path = File.Exists(FilePath) ? FilePath : OldFilePath;
            if (File.Exists(path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Options) ?? new();
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
