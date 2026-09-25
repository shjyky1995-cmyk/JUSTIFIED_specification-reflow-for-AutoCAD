using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Justified.SpecificationReflow.AutoCAD.Setup;

internal static class UiFont
{
    private static readonly PrivateFontCollection PrivateFonts = new PrivateFontCollection();

    public static FontFamily Family { get; } = LoadFamily();

    public static Font Pixel(float size, bool bold = false)
    {
        return new Font(Family, size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
    }

    private static FontFamily LoadFamily()
    {
        try
        {
            var path = Path.Combine(BundleLocator.BundleRoot, "Contents", "Windows", "fonts", "NotoSansSC-VF.ttf");
            if (File.Exists(path))
            {
                PrivateFonts.AddFontFile(path);
                var bundled = PrivateFonts.Families.FirstOrDefault(family =>
                    string.Equals(family.Name, "Noto Sans SC", StringComparison.Ordinal));
                if (bundled != null) return bundled;
            }
        }
        catch (System.Exception error) when (error is ArgumentException || error is IOException || error is System.Runtime.InteropServices.ExternalException)
        {
        }

        foreach (var name in new[] { "Microsoft YaHei", "微软雅黑" })
        {
            var installed = FontFamily.Families.FirstOrDefault(family => string.Equals(family.Name, name, StringComparison.Ordinal));
            if (installed != null) return installed;
        }
        return FontFamily.GenericSansSerif;
    }
}

internal static class BundleLocator
{
    public static string BundleRoot { get; } =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, KnownPaths.BundleDirectoryName);
}

internal static class LogoLoader
{
    public static Image? Load()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
                "Justified.SpecificationReflow.AutoCAD.Setup.Assets.note-logo.png");
            if (stream == null) return null;
            using var original = Image.FromStream(stream);
            return new Bitmap(original);
        }
        catch (System.Exception error) when (error is ArgumentException || error is IOException)
        {
            return null;
        }
    }
}
