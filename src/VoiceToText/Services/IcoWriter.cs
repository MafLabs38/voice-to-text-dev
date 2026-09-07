using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace VoiceToText.Services;

/// <summary>Construit un fichier .ico multi-résolution (frames PNG) à partir de plusieurs bitmaps.</summary>
internal static class IcoWriter
{
    public static byte[] Build(IReadOnlyList<Bitmap> images)
    {
        var pngFrames = new List<byte[]>(images.Count);
        foreach (var image in images)
        {
            using var ms = new MemoryStream();
            image.Save(ms, ImageFormat.Png);
            pngFrames.Add(ms.ToArray());
        }

        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output);

        writer.Write((ushort)0); // reserved
        writer.Write((ushort)1); // type = icon
        writer.Write((ushort)images.Count);

        var offset = 6 + images.Count * 16;
        for (var i = 0; i < images.Count; i++)
        {
            var image = images[i];
            writer.Write((byte)(image.Width >= 256 ? 0 : image.Width));
            writer.Write((byte)(image.Height >= 256 ? 0 : image.Height));
            writer.Write((byte)0); // palette
            writer.Write((byte)0); // reserved
            writer.Write((ushort)1); // planes
            writer.Write((ushort)32); // bits per pixel
            writer.Write((uint)pngFrames[i].Length);
            writer.Write((uint)offset);
            offset += pngFrames[i].Length;
        }

        foreach (var frame in pngFrames)
        {
            writer.Write(frame);
        }

        writer.Flush();
        return output.ToArray();
    }
}
