using System.Drawing;
using System.Linq;

namespace VoiceToText.Services;

/// <summary>
/// Logique pure de résolution de la position de l'overlay, indépendante de WPF/WinForms pour
/// rester testable unitairement. Mémorise non seulement des coordonnées brutes mais aussi
/// l'écran (DeviceName Windows, ex. "\\.\DISPLAY2") sur lequel elles ont été prises, pour pouvoir
/// reprendre la même configuration quand cet écran est débranché puis rebranché — un simple
/// repli "coin de l'écran principal" ferait perdre la position préférée à chaque déconnexion.
/// </summary>
public static class OverlayPositioning
{
    public sealed record ScreenInfo(string DeviceName, Rectangle Bounds);

    public static (double Left, double Top) ResolvePosition(
        double? savedLeft,
        double? savedTop,
        string? savedScreenDeviceName,
        IReadOnlyList<ScreenInfo> currentScreens,
        Rectangle primaryWorkArea)
    {
        if (savedLeft is not null && savedTop is not null)
        {
            var point = new Point((int)savedLeft.Value, (int)savedTop.Value);

            // 1) L'écran mémorisé est bien reconnecté et la position tombe dedans : cas nominal
            //    d'un retour à une configuration d'écrans connue (ex. redocking au bureau).
            if (!string.IsNullOrEmpty(savedScreenDeviceName))
            {
                var rememberedScreen = currentScreens.FirstOrDefault(s => s.DeviceName == savedScreenDeviceName);
                if (rememberedScreen is not null && rememberedScreen.Bounds.Contains(point))
                {
                    return (savedLeft.Value, savedTop.Value);
                }
            }

            // 2) Pas d'écran mémorisé (retrouvé), mais la position tombe quand même sur un écran
            //    actuellement connecté : on la garde telle quelle (ex. config mono-écran simple).
            if (currentScreens.Any(s => s.Bounds.Contains(point)))
            {
                return (savedLeft.Value, savedTop.Value);
            }
        }

        // 3) Repli : l'écran préféré n'est pas connecté (ex. écrans externes débranchés en
        //    déplacement) — coin bas-droit de l'écran principal plutôt que de rester invisible.
        return (primaryWorkArea.Right - 160, primaryWorkArea.Bottom - 80);
    }
}
