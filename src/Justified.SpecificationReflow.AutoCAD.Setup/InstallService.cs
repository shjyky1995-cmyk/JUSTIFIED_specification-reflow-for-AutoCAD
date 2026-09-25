using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Justified.SpecificationReflow.AutoCAD.Setup;

internal sealed class BuildInfo
{
    public string Version { get; set; } = "0.1.0";

    public string SourceCommit { get; set; } = "未知";

    public string BuiltUtc { get; set; } = "未知";

    public string SupportedHost { get; set; } = "AutoCAD 2021 R24.0 / Windows x64 / .NET Framework 4.8";

    public List<string> Pending { get; } = new List<string>();
}

internal sealed class InstallResult
{
    public string TargetBundleRoot { get; set; } = string.Empty;

    public string? BackupPath { get; set; }
}

internal sealed class InstallService
{
    private readonly string _bundleRoot;

    public InstallService(string bundleRoot)
    {
        _bundleRoot = Path.GetFullPath(bundleRoot);
    }

    public bool BundleLooksComplete =>
        Directory.Exists(_bundleRoot) && File.Exists(Path.Combine(_bundleRoot, "PackageContents.xml"));

    public BuildInfo LoadBuildInfo()
    {
        var info = new BuildInfo();
        var path = Path.Combine(_bundleRoot, "BUILD.json");
        if (!File.Exists(path)) return info;
        try
        {
            var json = JObject.Parse(File.ReadAllText(path));
            info.Version = (string?)json["version"] ?? info.Version;
            info.SourceCommit = (string?)json["sourceCommit"] ?? info.SourceCommit;
            info.BuiltUtc = (string?)json["builtUtc"] ?? info.BuiltUtc;
            info.SupportedHost = (string?)json["supportedHost"] ?? info.SupportedHost;
            var pending = json["pending"] as JArray;
            if (pending != null)
            {
                info.Pending.AddRange(pending.Select(item => (string?)item ?? string.Empty).Where(item => item.Length > 0));
            }
        }
        catch (System.Exception error) when (error is IOException || error is Newtonsoft.Json.JsonException || error is ArgumentException)
        {
        }
        return info;
    }

    public ManifestVerification Verify(out IReadOnlyList<ManifestEntry> entries)
    {
        entries = new List<ManifestEntry>();
        var manifestPath = Path.Combine(_bundleRoot, PackageVerifier.ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            var missing = new ManifestVerification();
            missing.Problems.Add("包内缺少 SHA256.json 校验清单，无法确认文件完整。");
            return missing;
        }
        entries = PackageVerifier.ParseManifest(File.ReadAllText(manifestPath));
        return PackageVerifier.Verify(entries, new FilePackageSource(_bundleRoot));
    }

    public InstallResult Install(BuildInfo build, IProgress<string> log)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        var targetRoot = Path.Combine(KnownPaths.PluginsRoot, KnownPaths.BundleDirectoryName);
        var plan = InstallPlan.Create(_bundleRoot, KnownPaths.PluginsRoot, stamp, Directory.Exists(targetRoot));
        var result = new InstallResult { TargetBundleRoot = plan.TargetBundleRoot, BackupPath = plan.BackupPath };

        log.Report("安装来源：" + plan.SourceBundleRoot);
        foreach (var step in plan.Steps)
        {
            log.Report("· " + step);
        }

        if (!Directory.Exists(plan.PluginsRoot))
        {
            Directory.CreateDirectory(plan.PluginsRoot);
            log.Report("已创建插件目录：" + plan.PluginsRoot);
        }

        if (plan.IsUpgrade && plan.BackupPath != null)
        {
            if (Directory.Exists(plan.BackupPath))
            {
                Directory.Delete(plan.BackupPath, recursive: true);
            }
            Directory.Move(plan.TargetBundleRoot, plan.BackupPath);
            log.Report("旧版已备份：" + plan.BackupPath);
        }

        CopyDirectory(plan.SourceBundleRoot, plan.TargetBundleRoot);
        log.Report("插件包已复制：" + plan.TargetBundleRoot);

        var uninstallerRoot = KnownPaths.UninstallerRoot;
        Directory.CreateDirectory(uninstallerRoot);
        foreach (var name in new[] { "Setup.exe", "Justified.SpecificationReflow.AutoCAD.SetupCore.dll", "Newtonsoft.Json.dll" })
        {
            var source = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name);
            if (File.Exists(source))
            {
                File.Copy(source, Path.Combine(uninstallerRoot, name), overwrite: true);
            }
        }
        log.Report("卸载程序已复制：" + uninstallerRoot);

        var setupExe = Path.Combine(uninstallerRoot, "Setup.exe");
        var uninstallString = "\"" + setupExe + "\" --uninstall";
        var sizeKb = Directory.EnumerateFiles(plan.TargetBundleRoot, "*", SearchOption.AllDirectories)
            .Sum(file => new FileInfo(file).Length) / 1024;
        RegistryStore.WriteInstall(build.Version, plan.TargetBundleRoot, uninstallString, uninstallerRoot, sizeKb);
        log.Report("已注册卸载入口，可在“应用和功能”卸载。");
        return result;
    }

    public void Uninstall(IProgress<string> log)
    {
        var installed = RegistryStore.ReadInstall();
        var bundle = installed.InstallLocation;
        if (string.IsNullOrWhiteSpace(bundle) || !Directory.Exists(bundle))
        {
            bundle = Path.Combine(KnownPaths.PluginsRoot, KnownPaths.BundleDirectoryName);
        }
        if (Directory.Exists(bundle))
        {
            Directory.Delete(bundle, recursive: true);
            log.Report("已删除插件包：" + bundle);
        }
        else
        {
            log.Report("未找到已安装的插件包，可能已被手动移除。");
        }

        RegistryStore.Remove();
        log.Report("已移除“应用和功能”中的卸载入口。");

        var uninstallerRoot = installed.UninstallerDirectory;
        if (!string.IsNullOrWhiteSpace(uninstallerRoot) && Directory.Exists(uninstallerRoot))
        {
            var running = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            foreach (var file in Directory.GetFiles(uninstallerRoot))
            {
                try
                {
                    if (!string.Equals(Path.GetFullPath(file), Path.GetFullPath(running), StringComparison.OrdinalIgnoreCase))
                    {
                        File.Delete(file);
                    }
                }
                catch (System.Exception error) when (error is IOException || error is UnauthorizedAccessException)
                {
                }
            }
            if (Directory.GetFiles(uninstallerRoot).Length == 0)
            {
                try
                {
                    Directory.Delete(uninstallerRoot, recursive: true);
                    log.Report("已清理卸载程序目录：" + uninstallerRoot);
                }
                catch (System.Exception error) when (error is IOException || error is UnauthorizedAccessException)
                {
                }
            }
        }
        log.Report("卸载完成。图纸中已生成的文字不受影响。");
        RemoveUninstallerFolderAfterExit(uninstallerRoot);
    }

    private static void RemoveUninstallerFolderAfterExit(string? uninstallerRoot)
    {
        if (uninstallerRoot == null || uninstallerRoot.Trim().Length == 0 || !Directory.Exists(uninstallerRoot)) return;
        var running = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var target = uninstallerRoot.TrimEnd(Path.DirectorySeparatorChar);
        if (!string.Equals(running, target, StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            var start = new ProcessStartInfo("cmd.exe", "/c timeout /t 2 >nul & rmdir /s /q \"" + target + "\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Process.Start(start);
        }
        catch (System.Exception error) when (error is System.ComponentModel.Win32Exception || error is InvalidOperationException)
        {
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
        }
        foreach (var directory in Directory.GetDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }
}
