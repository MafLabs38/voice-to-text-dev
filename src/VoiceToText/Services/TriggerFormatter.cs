using System.Windows.Input;
using VoiceToText.Models;

namespace VoiceToText.Services;

/// <summary>Libellés lisibles des déclencheurs (Paramètres, notifications).</summary>
public static class TriggerFormatter
{
    public static string FormatKeyboard(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Maj");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(key == Key.Space ? "Espace" : key.ToString());
        return string.Join("+", parts);
    }

    public static string FormatMouse(MouseTriggerButton button) => button switch
    {
        MouseTriggerButton.Middle => "Bouton milieu de la souris",
        MouseTriggerButton.XButton1 => "Bouton latéral 1 de la souris",
        MouseTriggerButton.XButton2 => "Bouton latéral 2 de la souris",
        _ => button.ToString(),
    };

    public static string FormatPrimary(AppSettings s) => s.TriggerKind == TriggerKind.Keyboard
        ? FormatKeyboard(s.HotkeyModifiers, s.HotkeyKey)
        : FormatMouse(s.MouseButton);

    public static string FormatSecondary(AppSettings s) => s.SecondaryTriggerKind == TriggerKind.Keyboard
        ? FormatKeyboard(s.SecondaryHotkeyModifiers, s.SecondaryHotkeyKey)
        : FormatMouse(s.SecondaryMouseButton);

    /// <summary>Vrai si le déclencheur secondaire est identique au principal (il ne ferait alors que doubler chaque pression).</summary>
    public static bool SecondaryDuplicatesPrimary(AppSettings s)
    {
        if (s.TriggerKind != s.SecondaryTriggerKind)
        {
            return false;
        }

        return s.TriggerKind == TriggerKind.Keyboard
            ? s.HotkeyModifiers == s.SecondaryHotkeyModifiers && s.HotkeyKey == s.SecondaryHotkeyKey
            : s.MouseButton == s.SecondaryMouseButton;
    }
}
