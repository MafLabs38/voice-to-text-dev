using System.Diagnostics;
using Microsoft.Win32;

namespace VoiceToText.Services;

public static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "VoiceToTextDictation";

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

        if (!enabled)
        {
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrEmpty(exePath))
        {
            key?.SetValue(ValueName, $"\"{exePath}\"");
        }
    }
}
