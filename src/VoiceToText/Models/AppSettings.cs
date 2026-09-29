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

    /// <summary>Périphérique de capture prioritaire (favori n°1). Utilisé s'il est actif au moment de l'enregistrement.</summary>
    public string? MicrophoneDeviceId { get; set; }

    /// <summary>Périphérique de capture secondaire (favori n°2, utilisé si le prioritaire est absent/débranché).</summary>
    public string? SecondaryMicrophoneDeviceId { get; set; }

    public string TranscriptionModel { get; set; } = "gpt-transcribe";

    public string Language { get; set; } = "fr";

    public bool ShowRecIndicator { get; set; } = true;

    public bool ShowTaskbarIndicator { get; set; } = true;

    public bool ShowLastTranscription { get; set; } = true;

    public bool HistoryEnabled { get; set; }

    public bool ShowHistoryModule { get; set; } = true;

    public double? OverlayLeft { get; set; }

    public double? OverlayTop { get; set; }

    /// <summary>DeviceName Windows (ex. "\\.\DISPLAY2") de l'écran sur lequel OverlayLeft/Top ont été enregistrés.</summary>
    public string? OverlayScreenDeviceName { get; set; }

    public bool AlwaysOnTop { get; set; } = true;

    public double OverlayOpacity { get; set; } = 0.92;

    public bool LaunchAtStartup { get; set; }

    public AppTheme Theme { get; set; } = AppTheme.Dark;

    /// <summary>Couleur d'accent, en #RRGGBB. Ne s'applique qu'aux fenêtres classiques (Paramètres…), jamais à l'overlay.</summary>
    public string PrimaryColorHex { get; set; } = "#3B82F6";
}
