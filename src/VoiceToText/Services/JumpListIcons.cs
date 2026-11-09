using System.Drawing;
using System.Drawing.Text;
using System.IO;
using Microsoft.Win32;

namespace VoiceToText.Services;

/// <summary>Chemins des icônes .ico utilisées par la Jump List de la barre des tâches (null = repli sur l'icône de l'appli).</summary>
public sealed record JumpListIconSet(string? Green, string? Red, string? Settings, string? Copy, string? Exit);

/// <summary>
/// Génère les icônes de la Jump List : pastilles ON/OFF et pictogrammes d'actions (roue crantée,
/// copie, croix) tirés de la police d'icônes de Windows. Windows n'accepte là qu'un chemin de
/// fichier d'icône, d'où l'écriture de fichiers .ico dans le dossier de données de l'appli.
/// </summary>
public static class JumpListIcons
{
    private const string GlyphSettings = "";
    private const string GlyphCopy = "";
    private const string GlyphExit = "";

    private static readonly string[] GlyphFonts = ["Segoe Fluent Icons", "Segoe MDL2 Assets"];
    private static readonly int[] IcoSizes = [16, 24, 32, 48];

    /// <summary>Réécrit toutes les icônes (la couleur des pictogrammes suit le thème système au moment du lancement).</summary>
    public static JumpListIconSet Generate()
    {
        string? directory = null;
        try
        {
            directory = Path.Combine(AppPaths.RootDirectory, "icons");
            Directory.CreateDirectory(directory);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        if (directory is null)
        {
            return new JumpListIconSet(null, null, null, null, null);
        }

        var glyphFont = FindGlyphFont();
        var glyphColor = SystemUsesLightTheme() ? Color.FromArgb(31, 31, 31) : Color.FromArgb(243, 243, 243);

        return new JumpListIconSet(
            Write(directory, "dot-green.ico", size => StatusDot.CreateBitmap(StatusDot.Green, size)),
            Write(directory, "dot-red.ico", size => StatusDot.CreateBitmap(StatusDot.Red, size)),
            WriteGlyph(directory, "action-settings.ico", glyphFont, GlyphSettings, glyphColor),
            WriteGlyph(directory, "action-copy.ico", glyphFont, GlyphCopy, glyphColor),
            WriteGlyph(directory, "action-exit.ico", glyphFont, GlyphExit, glyphColor));
    }

    private static string? WriteGlyph(string directory, string fileName, string? fontName, string glyph, Color color) =>
        fontName is null ? null : Write(directory, fileName, size => CreateGlyphBitmap(fontName, glyph, color, size));

    private static string? Write(string directory, string fileName, Func<int, Bitmap> createBitmap)
    {
        var bitmaps = IcoSizes.Select(createBitmap).ToList();
        try
        {
            var path = Path.Combine(directory, fileName);
            File.WriteAllBytes(path, IcoWriter.Build(bitmaps));
            return path;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        finally
        {
            bitmaps.ForEach(b => b.Dispose());
        }
    }

    private static Bitmap CreateGlyphBitmap(string fontName, string glyph, Color color, int size)
    {
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using var font = new Font(fontName, size * 0.72f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        graphics.DrawString(glyph, font, brush, new RectangleF(0, 0, size, size), format);
        return bitmap;
    }

    private static string? FindGlyphFont()
    {
        using var installed = new InstalledFontCollection();
        var names = installed.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return GlyphFonts.FirstOrDefault(names.Contains);
    }

    /// <summary>La Jump List suit le thème « système » (clair/sombre) de Windows, pas celui des applis.</summary>
    private static bool SystemUsesLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("SystemUsesLightTheme") is int value && value != 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }
}
