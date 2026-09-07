using System.Windows.Input;

namespace VoiceToText.Models;

public sealed class AppSettings
{
    public TriggerKind TriggerKind { get; set; } = TriggerKind.Keyboard;

    public ModifierKeys HotkeyModifiers { get; set; } = ModifierKeys.Control | ModifierKeys.Alt;

    public Key HotkeyKey { get; set; } = Key.Space;

    public MouseTriggerButton MouseButton { get; set; } = MouseTriggerButton.Middle;

    public ModifierKeys OverlayToggleModifiers { get; set; } = ModifierKeys.Control | ModifierKeys.Alt;

    public Key OverlayToggleKey { get; set; } = Key.O;

    public int MaxRecordingSeconds { get; set; } = 60;

    public string? MicrophoneDeviceId { get; set; }

    public string TranscriptionModel { get; set; } = "gpt-transcribe";

    public string Language { get; set; } = "fr";

    public bool ShowRecIndicator { get; set; } = true;

    public bool ShowLastTranscription { get; set; } = true;

    public bool HistoryEnabled { get; set; }

    public bool ShowHistoryModule { get; set; } = true;

    public double? OverlayLeft { get; set; }

    public double? OverlayTop { get; set; }

    public bool AlwaysOnTop { get; set; } = true;

    public double OverlayOpacity { get; set; } = 0.92;

    public bool LaunchAtStartup { get; set; }

    public AppTheme Theme { get; set; } = AppTheme.Dark;

    /// <summary>Couleur d'accent, en #RRGGBB. Ne s'applique qu'aux fenêtres classiques (Paramètres…), jamais à l'overlay.</summary>
    public string PrimaryColorHex { get; set; } = "#3B82F6";
}
