using System.IO;

namespace VoiceToText.Services;

public static class AppPaths
{
    public static string RootDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VoiceToTextDictation");

    public static string SettingsFilePath => Path.Combine(RootDirectory, "settings.json");
    public static string ApiKeyFilePath => Path.Combine(RootDirectory, "apikey.dat");
    public static string ModelsFilePath => Path.Combine(RootDirectory, "models.json");
    public static string TempRecordingsDirectory => Path.Combine(RootDirectory, "temp");
    public static string HistoryDirectory => Path.Combine(RootDirectory, "history");
}
