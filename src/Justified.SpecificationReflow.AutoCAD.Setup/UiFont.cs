using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.IO.Compression;
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
    private const string PayloadResource = "Justified.SpecificationReflow.AutoCAD.Setup.InstallerPayload.zip";
    private static readonly Lazy<string> Root = new Lazy<string>(Resolve);

    public static string BundleRoot => Root.Value;

    private static string Resolve()
    {
        using var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResource);
        if (payload == null)
        {
            // A developer build has no embedded package; the published installer must.
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, KnownPaths.BundleDirectoryName);
        }

        var extractionRoot = Path.Combine(Path.GetTempPath(), "JSR-Setup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(extractionRoot);
        try
        {
            var prefix = extractionRoot + Path.DirectorySeparatorChar;
            using (var archive = new ZipArchive(payload, ZipArchiveMode.Read, leaveOpen: false))
            {
                if (archive.Entries.Count == 0 || archive.Entries.Count > 256)
                    throw new InvalidDataException("安装程序内置数据的文件数量无效。");
                long totalBytes = 0;
                foreach (var entry in archive.Entries)
                {
                    totalBytes = checked(totalBytes + entry.Length);
                    if (totalBytes > 150L * 1024 * 1024)
                        throw new InvalidDataException("安装程序内置数据超出大小限制。");
                    var target = Path.GetFullPath(Path.Combine(extractionRoot,
                        entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                    if (!target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("安装程序内置数据包含无效路径。");
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(target);
                        continue;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    using var source = entry.Open();
                    using var destination = File.Create(target);
                    source.CopyTo(destination);
                }
            }
            var bundle = Path.Combine(extractionRoot, KnownPaths.BundleDirectoryName);
            if (!File.Exists(Path.Combine(bundle, "PackageContents.xml")) ||
                !File.Exists(Path.Combine(bundle, PackageVerifier.ManifestFileName)))
                throw new InvalidDataException("安装程序内置数据缺少清单。");
            AppDomain.CurrentDomain.ProcessExit += (_, _) => TryRemove(extractionRoot);
            return bundle;
        }
        catch
        {
            TryRemove(extractionRoot);
            throw;
        }
    }

    private static void TryRemove(string path)
    {
        try { Directory.Delete(path, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
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
