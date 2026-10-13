using System.Text.Json;
using System.Text.Json.Serialization;
using Fake64.WinForms;

namespace Fake64.Computer;

// App settings kept between runs, in %AppData%\Fake64\settings.json. One shared instance (Current):
// every window changes it and saves it, so nobody overwrites the others' values
public sealed class AppSettings
{
    // Default: keypad joystick in port 2, the port almost every game uses
    public ControlPortChoice Port1 { get; set; } = ControlPortChoice.None;
    public ControlPortChoice Port2 { get; set; } = new(ControlPortSource.KeypadJoystick);
    public bool Audio { get; set; } = true;

    // Studio window: last folder shown by the file explorer, window position, explorer width
    public string? LastFolder { get; set; }
    public WindowPlacement? StudioWindow { get; set; }
    public int ExplorerWidth { get; set; } = 280;

    // Main file of each project folder (lower-case folder -> file), see Studio.MainFiles
    public Dictionary<string, string> MainFiles { get; set; } = [];

    public static AppSettings Current { get; } = Load();

    static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Fake64", "settings.json");

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

public sealed record WindowPlacement(int X, int Y, int Width, int Height, bool Maximized);
