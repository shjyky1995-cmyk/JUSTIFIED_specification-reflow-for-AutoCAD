using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Justified.SpecificationReflow.AutoCAD.Setup;

public sealed class ManifestEntry
{
    [JsonProperty("File")]
    public string File { get; set; } = string.Empty;

    [JsonProperty("Hash")]
    public string Hash { get; set; } = string.Empty;
}

public interface IPackageFileSource
{
    bool FileExists(string relativePath);

    IReadOnlyCollection<string> ListFiles();

    string ComputeHash(string relativePath);
}

public sealed class ManifestVerification
{
    public bool Ok => Problems.Count == 0;

    public List<string> Problems { get; } = new List<string>();
}

public static class PackageVerifier
{
    public const string ManifestFileName = "SHA256.json";

    private static readonly string[] RequiredRuntimeFiles =
    {
        "DocumentFormat.OpenXml.dll",
        "DocumentFormat.OpenXml.Framework.dll",
        "Newtonsoft.Json.dll",
        "Justified.SpecificationReflow.AutoCAD.Contracts.dll",
        "Justified.SpecificationReflow.AutoCAD.DocumentCore.dll",
        "Justified.SpecificationReflow.AutoCAD.DocxAdapter.dll",
        "Justified.SpecificationReflow.AutoCAD.Standards.dll",
        "Justified.SpecificationReflow.AutoCAD.LayoutEngine.dll",
        "Justified.SpecificationReflow.AutoCAD.Application.dll",
        "Justified.SpecificationReflow.AutoCAD.AutoCadAdapter.dll",
        "Justified.SpecificationReflow.AutoCAD.PluginHost.dll"
    };

    public static IReadOnlyList<ManifestEntry> ParseManifest(string json)
    {
        var entries = JsonConvert.DeserializeObject<List<ManifestEntry>>(json);
        return entries ?? new List<ManifestEntry>();
    }

    public static ManifestVerification Verify(IReadOnlyList<ManifestEntry> entries, IPackageFileSource source)
    {
        var result = new ManifestVerification();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            var normalized = Normalize(entry.File);
            if (normalized == null)
            {
                result.Problems.Add("清单存在非法文件路径：" + (entry.File ?? string.Empty));
                continue;
            }
            if (!seen.Add(normalized))
            {
                result.Problems.Add("清单存在重复条目：" + entry.File);
                continue;
            }
            if (!source.FileExists(normalized))
            {
                result.Problems.Add("包内缺少文件：" + entry.File);
                continue;
            }
            var actual = source.ComputeHash(normalized);
            if (!string.Equals(actual, entry.Hash, StringComparison.OrdinalIgnoreCase))
            {
                result.Problems.Add("文件校验和不匹配：" + entry.File);
            }
        }

        foreach (var actual in source.ListFiles())
        {
            var normalized = Normalize(actual);
            if (normalized == null) continue;
            if (string.Equals(normalized, ManifestFileName, StringComparison.OrdinalIgnoreCase)) continue;
            if (!seen.Contains(normalized))
            {
                result.Problems.Add("包内存在清单外文件：" + actual);
            }
        }

        foreach (var required in RequiredRuntimeFiles)
        {
            var found = seen.Any(path => string.Equals(path, required, StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("/" + required, StringComparison.OrdinalIgnoreCase));
            if (!found)
            {
                result.Problems.Add("包内缺少运行依赖：" + required);
            }
        }

        return result;
    }

    internal static string? Normalize(string? path)
    {
        if (path == null) return null;
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (Path.IsPathRooted(path)) return null;
        var parts = path.Replace('\\', '/').Split('/');
        foreach (var part in parts)
        {
            if (part.Length == 0) continue;
            if (part == "..") return null;
        }
        var joined = string.Join("/", parts.Where(part => part.Length > 0));
        return joined.Length == 0 ? null : joined;
    }
}

public static class KnownPaths
{
    public const string BundleDirectoryName = "JUSTIFIED_specification-reflow-for-AutoCAD.bundle";
    public const string ProductDirectoryName = "JUSTIFIED_specification-reflow-for-AutoCAD";
    public const string UninstallRegistryKeyName = ProductDirectoryName;

    // Pre-flight/test seams: redirect install targets without touching the live candidate.
    public static string PluginsRoot
    {
        get
        {
            var overrideRoot = Environment.GetEnvironmentVariable("DN_SETUP_PLUGINS_ROOT");
            return string.IsNullOrWhiteSpace(overrideRoot)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Autodesk", "ApplicationPlugins")
                : Path.GetFullPath(overrideRoot);
        }
    }

    public static string UninstallerRoot
    {
        get
        {
            var overrideRoot = Environment.GetEnvironmentVariable("DN_SETUP_UNINSTALLER_ROOT");
            return string.IsNullOrWhiteSpace(overrideRoot)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", ProductDirectoryName)
                : Path.GetFullPath(overrideRoot);
        }
    }
}

public sealed class InstallPlan
{
    public string SourceBundleRoot { get; }

    public string PluginsRoot { get; }

    public string TargetBundleRoot { get; }

    public bool IsUpgrade { get; }

    public string? BackupPath { get; }

    public IReadOnlyList<string> Steps { get; }

    private InstallPlan(string sourceBundleRoot, string pluginsRoot, bool targetExists, string backupStamp)
    {
        SourceBundleRoot = Path.GetFullPath(sourceBundleRoot);
        PluginsRoot = Path.GetFullPath(pluginsRoot);
        TargetBundleRoot = Path.Combine(PluginsRoot, KnownPaths.BundleDirectoryName);
        IsUpgrade = targetExists;
        BackupPath = IsUpgrade ? Path.Combine(PluginsRoot, BackupName(backupStamp)) : null;
        var steps = new List<string>();
        if (IsUpgrade)
        {
            steps.Add("发现已安装的插件包，先备份为 " + BackupPath);
        }
        else
        {
            steps.Add("未发现已安装的插件包，按全新安装处理。");
        }
        steps.Add("复制插件包到 " + TargetBundleRoot);
        steps.Add("复制卸载程序到 " + KnownPaths.UninstallerRoot);
        steps.Add("在“应用和功能”注册卸载入口。");
        Steps = steps;
    }

    public static InstallPlan Create(string sourceBundleRoot, string pluginsRoot, string backupStamp, bool targetExists)
    {
        return new InstallPlan(sourceBundleRoot, pluginsRoot, targetExists, backupStamp);
    }

    public static string BackupName(string backupStamp)
    {
        return KnownPaths.BundleDirectoryName + ".backup-" + backupStamp;
    }
}

public sealed class EnvironmentCheck
{
    public string Name { get; }

    public bool Passed { get; }

    public bool Blocking { get; }

    public string Detail { get; }

    public string? HelpUrl { get; }

    public bool AutoInstallable { get; }

    public EnvironmentCheck(string name, bool passed, bool blocking, string detail, string? helpUrl = null, bool autoInstallable = false)
    {
        Name = name;
        Passed = passed;
        Blocking = blocking;
        Detail = detail;
        HelpUrl = helpUrl;
        AutoInstallable = autoInstallable;
    }
}

public static class EnvironmentInspector
{
    public const int Net48Release = 528040;
    public const string Net48DownloadUrl = "https://go.microsoft.com/fwlink/?linkid=2088631";
    public const string AutoCadUrl = "https://www.autodesk.com.cn/products/autocad/overview";

    public static IReadOnlyList<EnvironmentCheck> Inspect(int? netFrameworkRelease, bool autoCadKeyPresent, bool autoCadRunning)
    {
        var checks = new List<EnvironmentCheck>();
        var release = netFrameworkRelease ?? 0;
        checks.Add(new EnvironmentCheck(
            ".NET Framework 4.8 或更高",
            release >= Net48Release,
            true,
            release == 0
                ? "未安装 .NET Framework 4.8，点右侧按钮一键安装。"
                : release >= Net48Release
                    ? "已安装（Release=" + release.ToString(CultureInfo.InvariantCulture) + "）。"
                    : "版本过低（Release=" + release.ToString(CultureInfo.InvariantCulture) + "），点右侧按钮一键升级。",
            Net48DownloadUrl,
            autoInstallable: true));
        checks.Add(new EnvironmentCheck(
            "AutoCAD 2021（R24.0）",
            autoCadKeyPresent,
            true,
            autoCadKeyPresent
                ? "已检测到 AutoCAD 2021 安装信息。"
                : "未检测到 AutoCAD 2021。本工具需要 AutoCAD 2021 环境，请先安装。",
            AutoCadUrl));
        checks.Add(new EnvironmentCheck(
            "AutoCAD 已退出",
            !autoCadRunning,
            true,
            autoCadRunning
                ? "AutoCAD 正在运行。请先保存图纸并退出，再点右侧按钮重新检查。"
                : "AutoCAD 未运行，可以安全复制插件文件。"));
        return checks;
    }

    public static bool AllBlockingChecksPassed(IReadOnlyList<EnvironmentCheck> checks)
    {
        return checks.Where(check => check.Blocking).All(check => check.Passed);
    }
}
