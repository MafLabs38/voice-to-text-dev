using System.Windows.Media;
using VoiceToText.Models;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace VoiceToText.Services;

/// <summary>
/// Pousse les couleurs du thème courant dans les ressources globales de l'application, sous des
/// clés que seules les fenêtres "classiques" (ex. Paramètres) référencent via DynamicResource —
/// voir Themes/AppStyles.xaml. L'overlay ne merge jamais ce dictionnaire de styles et n'est donc
/// jamais affecté, par choix : c'est un chrome minimal, pas un écran de l'application.
/// </summary>
public static class ThemeManager
{
    public static void Apply(AppTheme theme, string primaryColorHex)
    {
        var resources = System.Windows.Application.Current.Resources;

        var (background, surface, border, textPrimary, textSecondary) = theme == AppTheme.Dark
            ? (Color.FromRgb(0x1E, 0x1E, 0x1E), Color.FromRgb(0x2D, 0x2D, 0x30), Color.FromRgb(0x3F, 0x3F, 0x46),
               Color.FromRgb(0xF0, 0xF0, 0xF0), Color.FromRgb(0xA0, 0xA0, 0xA0))
            : (Color.FromRgb(0xF5, 0xF5, 0xF5), Color.FromRgb(0xFF, 0xFF, 0xFF), Color.FromRgb(0xD0, 0xD0, 0xD0),
               Color.FromRgb(0x1A, 0x1A, 0x1A), Color.FromRgb(0x60, 0x60, 0x60));

        var primary = (Color)ColorConverter.ConvertFromString(primaryColorHex)!;
        var onPrimary = BestTextColorFor(primary);

        resources["Theme.Background"] = new SolidColorBrush(background);
        resources["Theme.Surface"] = new SolidColorBrush(surface);
        resources["Theme.Border"] = new SolidColorBrush(border);
        resources["Theme.TextPrimary"] = new SolidColorBrush(textPrimary);
        resources["Theme.TextSecondary"] = new SolidColorBrush(textSecondary);
        resources["Theme.Primary"] = new SolidColorBrush(primary);
        resources["Theme.OnPrimary"] = new SolidColorBrush(onPrimary);
    }

    /// <summary>
    /// Certaines couleurs d'accent (le vert notamment) sont trop claires pour du texte blanc —
    /// contraste WCAG mesuré à 2,28:1, bien en dessous du minimum lisible. On choisit noir ou
    /// blanc selon celui qui offre le meilleur contraste face à la couleur primaire choisie.
    /// </summary>
    private static Color BestTextColorFor(Color primary)
    {
        var contrastWithWhite = ContrastRatio(Colors.White, primary);
        var contrastWithBlack = ContrastRatio(Colors.Black, primary);
        return contrastWithWhite >= contrastWithBlack ? Colors.White : Colors.Black;
    }

    private static double ContrastRatio(Color a, Color b)
    {
        var l1 = RelativeLuminance(a) + 0.05;
        var l2 = RelativeLuminance(b) + 0.05;
        return l1 > l2 ? l1 / l2 : l2 / l1;
    }

    private static double RelativeLuminance(Color c)
    {
        double Channel(byte v)
        {
            var s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
    }

    public static readonly IReadOnlyList<(string Name, string Hex)> PrimaryColorChoices = new List<(string, string)>
    {
        ("Bleu", "#3B82F6"),
        ("Vert", "#22C55E"),
        ("Violet", "#A855F7"),
        ("Orange", "#F97316"),
        ("Rouge", "#EF4444"),
    };
}
