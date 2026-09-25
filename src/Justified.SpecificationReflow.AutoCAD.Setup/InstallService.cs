using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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

        log.Report("正在安装…");

        if (!Directory.Exists(plan.PluginsRoot))
        {
            Directory.CreateDirectory(plan.PluginsRoot);
        }

        if (plan.IsUpgrade && plan.BackupPath != null)
        {
            if (Directory.Exists(plan.BackupPath))
            {
                Directory.Delete(plan.BackupPath, recursive: true);
            }
            Directory.Move(plan.TargetBundleRoot, plan.BackupPath);
            log.Report("旧版本已备份。");
        }

        CopyDirectory(plan.SourceBundleRoot, plan.TargetBundleRoot);

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

        var setupExe = Path.Combine(uninstallerRoot, "Setup.exe");
        var uninstallString = "\"" + setupExe + "\" --uninstall";
        var sizeKb = Directory.EnumerateFiles(plan.TargetBundleRoot, "*", SearchOption.AllDirectories)
            .Sum(file => new FileInfo(file).Length) / 1024;
        RegistryStore.WriteInstall(build.Version, plan.TargetBundleRoot, uninstallString, uninstallerRoot, sizeKb);
        log.Report("安装完成。");
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
            log.Report("已移除插件。");
        }
        else
        {
            log.Report("未找到已安装的插件。");
        }

        RegistryStore.Remove();
        log.Report("已移除卸载入口。");

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
                }
                catch (System.Exception error) when (error is IOException || error is UnauthorizedAccessException)
                {
                }
            }
        }
        log.Report("卸载完成。");
        RemoveUninstallerFolderAfterExit(uninstallerRoot, log);
    }

    private static void RemoveUninstallerFolderAfterExit(string? uninstallerRoot, IProgress<string> log)
    {
        if (uninstallerRoot == null || uninstallerRoot.Trim().Length == 0 || !Directory.Exists(uninstallerRoot))
        {
            return;
        }
        var running = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var target = uninstallerRoot.TrimEnd(Path.DirectorySeparatorChar);
        if (!string.Equals(running, target, StringComparison.OrdinalIgnoreCase))
        {
            log.Report("卸载程序不在安装位置运行，保留卸载程序目录：" + target);
            return;
        }
        try
        {
            var pid = Process.GetCurrentProcess().Id;
            var command = "Wait-Process -Id " + pid.ToString(CultureInfo.InvariantCulture)
                + " -ErrorAction SilentlyContinue; foreach ($i in 1..5) { try { Remove-Item -LiteralPath '"
                + target + "' -Recurse -Force -ErrorAction Stop; break } catch { Start-Sleep -Milliseconds 800 } }";
            var start = new ProcessStartInfo("powershell.exe",
                "-NoProfile -WindowStyle Hidden -Command \"" + command + "\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Process.Start(start);
        }
        catch (System.Exception error) when (error is System.ComponentModel.Win32Exception || error is InvalidOperationException)
        {
            log.Report("卸载程序目录自动清理未安排成功：" + error.Message);
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
