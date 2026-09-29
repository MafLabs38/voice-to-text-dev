namespace VoiceToText.Services;

/// <summary>
/// Choisit, parmi les périphériques de capture actifs à l'instant présent, le favori à utiliser :
/// le prioritaire s'il est branché, sinon le secondaire, sinon aucun (le périphérique par défaut
/// du système sera utilisé). Réévalué à chaque enregistrement plutôt que mémorisé, pour que le
/// favori prioritaire reprenne automatiquement la main dès qu'il est rebranché.
/// </summary>
public static class PreferredDeviceResolver
{
    public static string? Resolve(string? primaryDeviceId, string? secondaryDeviceId)
    {
        if (string.IsNullOrEmpty(primaryDeviceId) && string.IsNullOrEmpty(secondaryDeviceId))
        {
            return null;
        }

        var activeIds = AudioDeviceEnumerator.GetInputDevices().Select(d => d.Id).ToHashSet();

        if (!string.IsNullOrEmpty(primaryDeviceId) && activeIds.Contains(primaryDeviceId))
        {
            return primaryDeviceId;
        }

        if (!string.IsNullOrEmpty(secondaryDeviceId) && activeIds.Contains(secondaryDeviceId))
        {
            return secondaryDeviceId;
        }

        return null;
    }
}
