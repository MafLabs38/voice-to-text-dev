using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using VoiceToText.Models;

namespace VoiceToText.Services;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _settingsFilePath;

    public SettingsStore()
    {
        Directory.CreateDirectory(AppPaths.RootDirectory);
        _settingsFilePath = AppPaths.SettingsFilePath;
    }

    public AppSettings Load()
    {
        if (!File.Exists(_settingsFilePath))
        {
            return new AppSettings();
        }

        var json = File.ReadAllText(_settingsFilePath);
        return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        File.WriteAllText(_settingsFilePath, json);
    }
}
