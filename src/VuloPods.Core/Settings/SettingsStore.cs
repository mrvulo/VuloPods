using System.Text.Json;
using System.Text.Json.Serialization;
using VuloPods.Core.Logging;

namespace VuloPods.Core.Settings;

/// <summary>Lädt und speichert <see cref="AppSettings"/> als JSON. Fehler beim Laden führen zu Defaults, nie zu Ausnahmen.</summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;

    public SettingsStore(string filePath) => _filePath = filePath;

    public static string DefaultPath => Path.Combine(Log.DefaultDirectory, "settings.json");

    public AppSettings Current { get; private set; } = new();

    public event Action<AppSettings>? Changed;

    /// <summary>Fehlt die Datei oder ist sie kaputt → Defaults. Kaputte Datei wird nach settings.json.bad verschoben.</summary>
    public void Load()
    {
        Current = new AppSettings();
        if (!File.Exists(_filePath)) return;

        try
        {
            var json = File.ReadAllText(_filePath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (loaded is null) throw new JsonException("Einstellungsdatei enthält null");
            Current = loaded.Normalized();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            Log.Error("Einstellungen konnten nicht gelesen werden, Defaults werden verwendet", ex);
            try
            {
                File.Move(_filePath, _filePath + ".bad", overwrite: true);
            }
            catch (Exception moveEx) when (moveEx is IOException or UnauthorizedAccessException)
            {
                Log.Error("Kaputte Einstellungsdatei konnte nicht verschoben werden", moveEx);
            }
            Current = new AppSettings();
        }
    }

    /// <summary>Normalisiert, schreibt atomar und feuert <see cref="Changed"/> nur bei echter Änderung.</summary>
    public void Save(AppSettings next)
    {
        var normalized = next.Normalized();
        if (normalized == Current) return;

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var tmp = _filePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(normalized, JsonOptions));
        File.Move(tmp, _filePath, overwrite: true);

        Current = normalized;
        Changed?.Invoke(normalized);
    }
}
