using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using Justified.SpecificationReflow.AutoCAD.Setup;
using Microsoft.Win32;

namespace Justified.SpecificationReflow.AutoCAD.Setup;

internal static class EnvironmentProbe
{
    public static int? NetFrameworkRelease()
    {
        using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
            .OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full");
        var value = key?.GetValue("Release");
        return value as int?;
    }

    public static bool AutoCad2021Installed()
    {
        using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
            .OpenSubKey(@"SOFTWARE\Autodesk\AutoCAD\R24.0");
        return key != null;
    }

    public static bool AutoCadRunning()
    {
        try
        {
            return Process.GetProcessesByName("acad").Length > 0;
        }
        catch (System.Exception error) when (error is InvalidOperationException || error is System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    public static IReadOnlyList<EnvironmentCheck> Inspect()
    {
        return EnvironmentInspector.Inspect(NetFrameworkRelease(), AutoCad2021Installed(), AutoCadRunning());
    }
}

internal sealed class InstalledInfo
{
    public string? InstallLocation { get; set; }

    public string? UninstallerDirectory { get; set; }

    public bool Registered { get; set; }
}

internal static class RegistryStore
{
    private const string UninstallSubKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

    public static void WriteInstall(string displayVersion, string installLocation, string uninstallString, string uninstallerDirectory, long estimatedSizeKb)
    {
        using var key = Registry.CurrentUser.CreateSubKey(UninstallSubKey + @"\" + KnownPaths.UninstallRegistryKeyName);
        key.SetValue("DisplayName", "JUSTIFIED 设计说明落图工具", RegistryValueKind.String);
        key.SetValue("DisplayVersion", displayVersion, RegistryValueKind.String);
        key.SetValue("Publisher", "Stephen Kim", RegistryValueKind.String);
        key.SetValue("InstallLocation", installLocation, RegistryValueKind.String);
        key.SetValue("UninstallString", uninstallString, RegistryValueKind.String);
        key.SetValue("DisplayIcon", Path.Combine(uninstallerDirectory, "Setup.exe"), RegistryValueKind.String);
        key.SetValue("UninstallerDirectory", uninstallerDirectory, RegistryValueKind.String);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("EstimatedSize", (int)estimatedSizeKb, RegistryValueKind.DWord);
    }

    public static InstalledInfo ReadInstall()
    {
        var info = new InstalledInfo();
        using var key = Registry.CurrentUser.OpenSubKey(UninstallSubKey + @"\" + KnownPaths.UninstallRegistryKeyName);
        if (key == null) return info;
        info.Registered = true;
        info.InstallLocation = key.GetValue("InstallLocation") as string;
        info.UninstallerDirectory = key.GetValue("UninstallerDirectory") as string;
        return info;
    }

    public static void Remove()
    {
        Registry.CurrentUser.DeleteSubKeyTree(UninstallSubKey + @"\" + KnownPaths.UninstallRegistryKeyName, throwOnMissingSubKey: false);
    }
}

internal sealed class FilePackageSource : IPackageFileSource
{
    private readonly string _root;

    public FilePackageSource(string root)
    {
        _root = Path.GetFullPath(root);
    }

    public bool FileExists(string relativePath)
    {
        return File.Exists(ToFull(relativePath));
    }

    public IReadOnlyCollection<string> ListFiles()
    {
        var files = new List<string>();
        foreach (var absolute in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            files.Add(ToRelative(absolute));
        }
        return files;
    }

    public string ComputeHash(string relativePath)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(ToFull(relativePath));
        var hash = sha.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }

    private string ToFull(string relativePath)
    {
        return Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private string ToRelative(string absolutePath)
    {
        return absolutePath.Substring(_root.Length + 1).Replace(Path.DirectorySeparatorChar, '/');
    }
}
