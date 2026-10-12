using System.Text.Json;
using System.Text.Json.Serialization;
using Fake64.WinForms;

namespace Fake64;

// Impostazioni dell'app che restano fra un avvio e l'altro, in %AppData%\Fake64\settings.json
sealed class AppSettings
{
    // Predefinito: joystick dal tastierino nella porta 2, quella usata da quasi tutti i giochi
    public ControlPortChoice Port1 { get; set; } = ControlPortChoice.None;
    public ControlPortChoice Port2 { get; set; } = new(ControlPortSource.KeypadJoystick);

    static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Fake64", "settings.json");

    static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options) ?? new();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { }
        return new(); // file mancante o illeggibile: valori predefiniti
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Options));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } // non bloccare l'emulatore
    }
}
