using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace VoiceToText.Services;

/// <summary>
/// Dessine les icônes en GDI+ (forme de micro) à plusieurs résolutions et les assemble en .ico
/// multi-frame : Windows choisit alors la meilleure résolution selon le DPI/la taille d'affichage
/// (zone de notification, Explorateur, barre des tâches) au lieu d'agrandir une seule petite image.
/// </summary>
internal static class TrayIconFactory
{
    private static readonly int[] TraySizes = { 16, 20, 24, 32, 40, 48, 64 };
    private static readonly int[] ApplicationIconSizes = { 16, 24, 32, 48, 64, 128, 256 };

    public static (Icon Idle, Icon Active) CreateTrayIcons()
    {
        return (BuildMultiResolutionIcon(TraySizes, active: false), BuildMultiResolutionIcon(TraySizes, active: true));
    }

    /// <summary>Fichier .ico multi-résolution destiné à `&lt;ApplicationIcon&gt;` (icône de l'exe / futur installeur).</summary>
    public static byte[] BuildApplicationIcoBytes()
    {
        var bitmaps = ApplicationIconSizes.Select(size => DrawMicBitmap(size, active: false)).ToList();
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

    private static Icon BuildMultiResolutionIcon(IReadOnlyList<int> sizes, bool active)
    {
        var bitmaps = sizes.Select(size => DrawMicBitmap(size, active)).ToList();
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

    private static Bitmap DrawMicBitmap(int size, bool active)
    {
        var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        var s = size / 32f;
        var micColor = active ? Color.FromArgb(255, 225, 45, 45) : Color.FromArgb(255, 230, 230, 230);
        using var micBrush = new SolidBrush(micColor);
        using var outlinePen = new Pen(Color.FromArgb(255, 40, 40, 40), Math.Max(1f, 1.5f * s));
        using var standPen = new Pen(Color.FromArgb(255, 60, 60, 60), Math.Max(1f, 2f * s));

        var headRect = new RectangleF(11 * s, 3 * s, 10 * s, 15 * s);
        g.FillEllipse(micBrush, headRect);
        g.DrawEllipse(outlinePen, headRect);

        g.DrawArc(standPen, 7 * s, 9 * s, 18 * s, 16 * s, 0, 180);
        g.DrawLine(standPen, 16 * s, 25 * s, 16 * s, 28 * s);
        g.DrawLine(standPen, 11 * s, 28 * s, 21 * s, 28 * s);

        if (active)
        {
            using var dotBrush = new SolidBrush(Color.FromArgb(255, 255, 20, 20));
            g.FillEllipse(dotBrush, 21 * s, 1 * s, 9 * s, 9 * s);
        }

        return bitmap;
    }
}
