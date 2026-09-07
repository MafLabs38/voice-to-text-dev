using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace VoiceToText.Services;

/// <summary>
/// Assemble les icônes (zone de notification + exe) à partir de deux images sources haute
/// résolution (Assets/mic-*-source.png, un micro en dégradé dessiné à la main sur lucide.dev/icons
/// puis recoloré) rééchantillonnées à chaque taille cible, plutôt qu'un dessin procédural GDI+.
/// </summary>
internal static class TrayIconFactory
{
    private static readonly int[] TraySizes = { 16, 20, 24, 32, 40, 48, 64 };
    private static readonly int[] ApplicationIconSizes = { 16, 24, 32, 48, 64, 128, 256 };

    private static string IdleSourcePath => Path.Combine(AppContext.BaseDirectory, "Assets", "eq-idle-source.png");
    private static string ActiveSourcePath => Path.Combine(AppContext.BaseDirectory, "Assets", "eq-active-source.png");

    public static (Icon Idle, Icon Active) CreateTrayIcons()
    {
        using var idleSource = new Bitmap(IdleSourcePath);
        using var activeSource = new Bitmap(ActiveSourcePath);
        return (BuildMultiResolutionIcon(idleSource, TraySizes), BuildMultiResolutionIcon(activeSource, TraySizes));
    }

    /// <summary>Fichier .ico multi-résolution destiné à `&lt;ApplicationIcon&gt;` (icône de l'exe / futur installeur).</summary>
    public static byte[] BuildApplicationIcoBytes()
    {
        using var idleSource = new Bitmap(IdleSourcePath);
        var bitmaps = ApplicationIconSizes.Select(size => Resize(idleSource, size)).ToList();
        try
        {
            return IcoWriter.Build(bitmaps);
        }
        finally
        {
            foreach (var bitmap in bitmaps)
            {
                bitmap.Dispose();
            }
        }
    }

    private static Icon BuildMultiResolutionIcon(Bitmap source, IReadOnlyList<int> sizes)
    {
        var bitmaps = sizes.Select(size => Resize(source, size)).ToList();
        try
        {
            var icoBytes = IcoWriter.Build(bitmaps);
            using var stream = new MemoryStream(icoBytes);
            return new Icon(stream);
        }
        finally
        {
            foreach (var bitmap in bitmaps)
            {
                bitmap.Dispose();
            }
        }
    }

    private static Bitmap Resize(Bitmap source, int size)
    {
        var resized = new Bitmap(size, size);
        using var g = Graphics.FromImage(resized);
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.DrawImage(source, 0, 0, size, size);
        return resized;
    }
}
