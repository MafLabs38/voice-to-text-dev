using System.Drawing;
using System.Drawing.Drawing2D;

namespace VoiceToText.Services;

/// <summary>
/// Pastilles d'état vertes/rouges (ON/OFF) des menus (zone de notification et Jump List).
/// </summary>
public static class StatusDot
{
    public static readonly Color Green = Color.FromArgb(34, 197, 94);
    public static readonly Color Red = Color.FromArgb(239, 68, 68);

    public static Bitmap CreateBitmap(Color color, int size = 16)
    {
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(color);
        var margin = size * 0.19f;
        graphics.FillEllipse(brush, margin, margin, size - 2 * margin, size - 2 * margin);
        return bitmap;
    }
}
