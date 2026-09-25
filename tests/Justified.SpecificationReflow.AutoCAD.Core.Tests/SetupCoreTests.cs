using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Justified.SpecificationReflow.AutoCAD.Setup;
using NUnit.Framework;

namespace Justified.SpecificationReflow.AutoCAD.Core.Tests;

public class SetupCoreTests
{
    private static readonly string[] RequiredFiles =
    {
        "Contents/Windows/DocumentFormat.OpenXml.dll",
        "Contents/Windows/DocumentFormat.OpenXml.Framework.dll",
        "Contents/Windows/Newtonsoft.Json.dll",
        "Contents/Windows/Justified.SpecificationReflow.AutoCAD.Contracts.dll",
        "Contents/Windows/Justified.SpecificationReflow.AutoCAD.DocumentCore.dll",
        "Contents/Windows/Justified.SpecificationReflow.AutoCAD.DocxAdapter.dll",
        "Contents/Windows/Justified.SpecificationReflow.AutoCAD.Standards.dll",
        "Contents/Windows/Justified.SpecificationReflow.AutoCAD.LayoutEngine.dll",
        "Contents/Windows/Justified.SpecificationReflow.AutoCAD.Application.dll",
        "Contents/Windows/Justified.SpecificationReflow.AutoCAD.AutoCadAdapter.dll",
        "Contents/Windows/Justified.SpecificationReflow.AutoCAD.PluginHost.dll",
        "PackageContents.xml",
        "BUILD.json"
    };

    private static List<ManifestEntry> CompleteManifest()
    {
        return RequiredFiles
            .Select(file => new ManifestEntry { File = file, Hash = "hash-" + file })
            .ToList();
    }

    private sealed class FakePackageSource : IPackageFileSource
    {
        private readonly Dictionary<string, string> _files;

        public FakePackageSource(IEnumerable<string> files, IEnumerable<string>? extraFiles = null)
        {
            _files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files) _files[file] = "hash-" + file;
            if (extraFiles != null)
            {
                foreach (var file in extraFiles) _files[file] = "hash-" + file;
            }
        }

        public bool FileExists(string relativePath) => _files.ContainsKey(relativePath);

        public IReadOnlyCollection<string> ListFiles() => _files.Keys.ToList();

        public string ComputeHash(string relativePath) => _files[relativePath];
    }

    [Test]
    public void VerifyAcceptsCompleteManifest()
    {
        var entries = CompleteManifest();
        var report = PackageVerifier.Verify(entries, new FakePackageSource(RequiredFiles));
        Assert.That(report.Ok, Is.True, string.Join(";", report.Problems));
    }

    [Test]
    public void VerifyReportsHashMismatch()
    {
        var entries = CompleteManifest();
        entries[0] = new ManifestEntry { File = entries[0].File, Hash = "tampered" };
        var report = PackageVerifier.Verify(entries, new FakePackageSource(RequiredFiles));
        Assert.That(report.Ok, Is.False);
        Assert.That(report.Problems.Any(problem => problem.Contains("校验和不匹配")), Is.True);
    }

    [Test]
    public void VerifyReportsMissingFile()
    {
        var entries = CompleteManifest();
        var present = RequiredFiles.Where(file => !file.EndsWith("PluginHost.dll", StringComparison.Ordinal)).ToArray();
        var report = PackageVerifier.Verify(entries, new FakePackageSource(present));
        Assert.That(report.Ok, Is.False);
        Assert.That(report.Problems.Any(problem => problem.Contains("缺少文件")), Is.True);
    }

    [Test]
    public void VerifyRejectsAbsoluteManifestPath()
    {
        var entries = CompleteManifest();
        entries.Add(new ManifestEntry { File = @"C:\Windows\evil.dll", Hash = "x" });
        var report = PackageVerifier.Verify(entries, new FakePackageSource(RequiredFiles));
        Assert.That(report.Ok, Is.False);
        Assert.That(report.Problems.Any(problem => problem.Contains("非法文件路径")), Is.True);
    }

    [Test]
    public void VerifyRejectsPathEscape()
    {
        var entries = CompleteManifest();
        entries.Add(new ManifestEntry { File = "../outside.dll", Hash = "x" });
        var report = PackageVerifier.Verify(entries, new FakePackageSource(RequiredFiles));
        Assert.That(report.Ok, Is.False);
        Assert.That(report.Problems.Any(problem => problem.Contains("非法文件路径")), Is.True);
    }

    [Test]
    public void VerifyRejectsDuplicateEntriesIgnoringCase()
    {
        var entries = CompleteManifest();
        entries.Add(new ManifestEntry { File = "packagecontents.xml", Hash = "other" });
        var report = PackageVerifier.Verify(entries, new FakePackageSource(RequiredFiles));
        Assert.That(report.Ok, Is.False);
        Assert.That(report.Problems.Any(problem => problem.Contains("重复条目")), Is.True);
    }

    [Test]
    public void VerifyReportsUnexpectedFile()
    {
        var entries = CompleteManifest();
        var source = new FakePackageSource(RequiredFiles, new[] { "Contents/Windows/acmgd.dll" });
        var report = PackageVerifier.Verify(entries, source);
        Assert.That(report.Ok, Is.False);
        Assert.That(report.Problems.Any(problem => problem.Contains("清单外文件")), Is.True);
    }

    [Test]
    public void VerifyReportsMissingRuntimeDependency()
    {
        var entries = CompleteManifest();
        entries.RemoveAll(entry => entry.File.EndsWith("Newtonsoft.Json.dll", StringComparison.Ordinal));
        var present = RequiredFiles.Where(file => !file.EndsWith("Newtonsoft.Json.dll", StringComparison.Ordinal)).ToArray();
        var report = PackageVerifier.Verify(entries, new FakePackageSource(present));
        Assert.That(report.Ok, Is.False);
        Assert.That(report.Problems.Any(problem => problem.Contains("缺少运行依赖")), Is.True);
    }

    [Test]
    public void ParseManifestReadsEveryEntry()
    {
        var json = "[{\"File\":\"a.txt\",\"Hash\":\"abc\"},{\"File\":\"b.txt\",\"Hash\":\"def\"}]";
        var entries = PackageVerifier.ParseManifest(json);
        Assert.That(entries.Count, Is.EqualTo(2));
        Assert.That(entries[0].File, Is.EqualTo("a.txt"));
        Assert.That(entries[1].Hash, Is.EqualTo("def"));
    }

    [Test]
    public void FreshInstallPlanHasNoBackup()
    {
        var plan = InstallPlan.Create(@"G:\pkg\bundle", @"G:\plugins", "20260925-170000", targetExists: false);
        Assert.That(plan.IsUpgrade, Is.False);
        Assert.That(plan.BackupPath, Is.Null);
        Assert.That(plan.TargetBundleRoot, Is.EqualTo(Path.Combine(@"G:\plugins", KnownPaths.BundleDirectoryName)));
        Assert.That(plan.Steps, Is.Not.Empty);
    }

    [Test]
    public void UpgradePlanBacksUpExistingBundle()
    {
        var plan = InstallPlan.Create(@"G:\pkg\bundle", @"G:\plugins", "20260925-170000", targetExists: true);
        Assert.That(plan.IsUpgrade, Is.True);
        Assert.That(plan.BackupPath, Is.EqualTo(Path.Combine(@"G:\plugins", KnownPaths.BundleDirectoryName + ".backup-20260925-170000")));
        Assert.That(plan.Steps.Any(step => step.Contains("备份")), Is.True);
    }

    [Test]
    public void EnvironmentPassesOnReadyMachine()
    {
        var checks = EnvironmentInspector.Inspect(EnvironmentInspector.Net48Release, autoCadKeyPresent: true, autoCadRunning: false);
        Assert.That(EnvironmentInspector.AllBlockingChecksPassed(checks), Is.True);
        Assert.That(checks.All(check => check.Passed), Is.True);
    }

    [Test]
    public void EnvironmentBlocksOnMissingAutoCad()
    {
        var checks = EnvironmentInspector.Inspect(EnvironmentInspector.Net48Release, autoCadKeyPresent: false, autoCadRunning: false);
        Assert.That(EnvironmentInspector.AllBlockingChecksPassed(checks), Is.False);
        var cad = checks.Single(check => check.Name.Contains("AutoCAD 2021"));
        Assert.That(cad.Passed, Is.False);
        Assert.That(cad.Blocking, Is.True);
        Assert.That(cad.Detail, Does.Contain("AutoCAD 2021"));
    }

    [Test]
    public void EnvironmentBlocksOnOldRuntime()
    {
        var checks = EnvironmentInspector.Inspect(461808, autoCadKeyPresent: true, autoCadRunning: false);
        Assert.That(EnvironmentInspector.AllBlockingChecksPassed(checks), Is.False);
    }

    [Test]
    public void EnvironmentBlocksWhileAutoCadRuns()
    {
        var checks = EnvironmentInspector.Inspect(EnvironmentInspector.Net48Release, autoCadKeyPresent: true, autoCadRunning: true);
        Assert.That(EnvironmentInspector.AllBlockingChecksPassed(checks), Is.False);
        var running = checks.Single(check => check.Name == "AutoCAD 已退出");
        Assert.That(running.Detail, Does.Contain("退出 AutoCAD"));
    }
}
