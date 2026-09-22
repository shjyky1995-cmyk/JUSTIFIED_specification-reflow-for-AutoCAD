using System;
using System.IO;
using System.Linq;
using System.Threading;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;
using Justified.SpecificationReflow.AutoCAD.Contracts.Templates;
using Justified.SpecificationReflow.AutoCAD.Standards;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Justified.SpecificationReflow.AutoCAD.Core.Tests;

public class StandardPackageTests
{
    [Test]
    public void PublishedDirectoryRejectsThePendingInstitutionReference()
    {
        var published = Path.Combine(Root(), "standards", "published");
        Assert.That(Directory.GetFiles(published, "*.json", SearchOption.AllDirectories), Is.Empty);
        var catalog = new DirectoryPackageCatalog(published, allowTestFixtures: false);
        var standard = catalog.Load(new StandardRef { Id = "institution-note", Version = "pending" }, CancellationToken.None);
        var template = catalog.Load(new TemplateRef { Id = "structure-A1", Version = "pending" }, CancellationToken.None);
        Assert.That(standard.Standard, Is.Null);
        Assert.That(template.Template, Is.Null);
        Assert.That(standard.Diagnostics.Single().Code, Is.EqualTo(DiagnosticCodes.ETemplateInvalid));
        Assert.That(template.Diagnostics.Single().Code, Is.EqualTo(DiagnosticCodes.ETemplateInvalid));
        Assert.That(standard.Success, Is.False);
    }

    [Test]
    public void TestFixtureRoundTripKeepsColumnWidthsAndBlocksProductionUse()
    {
        using var stage = new PackageStage();
        var allowed = new DirectoryPackageCatalog(stage.Root, allowTestFixtures: true);
        var standard = allowed.Load(new StandardRef { Id = "test-note", Version = "test-1" }, CancellationToken.None);
        var template = allowed.Load(new TemplateRef { Id = "test-A1", Version = "test-1" }, CancellationToken.None);
        Assert.That(standard.Success, Is.True, Dump(standard.Diagnostics));
        Assert.That(template.Success, Is.True, Dump(template.Diagnostics));
        Assert.That(standard.Standard!.StandardId, Is.EqualTo("test-note"));
        Assert.That(standard.Standard.TextHeight, Is.EqualTo(2.5));
        Assert.That(template.Template!.Columns.Select(column => column.Right - column.Left), Is.EqualTo(new[] { 80d, 90d }));
        Assert.That(template.Template.Status, Is.EqualTo(TemplateStatus.Calibrated));
        Assert.That(File.ReadAllText(stage.StandardPath), Does.Contain("test-fixture"));

        var production = new DirectoryPackageCatalog(stage.Root, allowTestFixtures: false);
        var rejected = production.Load(new StandardRef { Id = "test-note", Version = "test-1" }, CancellationToken.None);
        Assert.That(rejected.Standard, Is.Null);
        Assert.That(rejected.Diagnostics.Single().Message, Does.Contain("测试夹具"));
    }

    [Test]
    public void MissingOrUncalibratedValuesStayBlocked()
    {
        using var stage = new PackageStage();
        var catalog = new DirectoryPackageCatalog(stage.Root, allowTestFixtures: true);

        Rewrite(stage.StandardPath, token => token.Remove("textHeight"));
        var missingHeight = catalog.Load(new StandardRef { Id = "test-note", Version = "test-1" }, CancellationToken.None);
        Assert.That(missingHeight.Standard, Is.Null);
        Assert.That(missingHeight.Diagnostics.Any(item => item.Details!["path"] == "textHeight"), Is.True, Dump(missingHeight.Diagnostics));

        stage.Reset();
        var pendingPath = Path.Combine(stage.Root, "standards", "test-note", "pending.json");
        File.Copy(stage.StandardPath, pendingPath);
        Rewrite(pendingPath, token => token["version"] = "pending");
        var pending = catalog.Load(new StandardRef { Id = "test-note", Version = "pending" }, CancellationToken.None);
        Assert.That(pending.Standard, Is.Null);
        Assert.That(pending.Diagnostics.Any(item => item.Details!["path"] == "version"), Is.True, Dump(pending.Diagnostics));

        stage.Reset();
        Rewrite(stage.TemplatePath, token => token["status"] = "uncalibrated");
        var uncalibrated = catalog.Load(new TemplateRef { Id = "test-A1", Version = "test-1" }, CancellationToken.None);
        Assert.That(uncalibrated.Template, Is.Null);
        Assert.That(uncalibrated.Diagnostics.Any(item => item.Details!["path"] == "status"), Is.True, Dump(uncalibrated.Diagnostics));

        stage.Reset();
        Rewrite(stage.TemplatePath, token => token["columns"] = new JArray());
        var noColumns = catalog.Load(new TemplateRef { Id = "test-A1", Version = "test-1" }, CancellationToken.None);
        Assert.That(noColumns.Template, Is.Null);
        Assert.That(noColumns.Diagnostics.Any(item => item.Details!["path"] == "columns"), Is.True, Dump(noColumns.Diagnostics));

        stage.Reset();
        Rewrite(stage.TemplatePath, token => token["columns"]![0]!["rowCount"] = 0);
        var zeroRows = catalog.Load(new TemplateRef { Id = "test-A1", Version = "test-1" }, CancellationToken.None);
        Assert.That(zeroRows.Template, Is.Null);
        Assert.That(zeroRows.Diagnostics.Any(item => item.Message.Contains("行数")), Is.True, Dump(zeroRows.Diagnostics));

        stage.Reset();
        Rewrite(stage.TemplatePath, token => token.Remove("pageStep"));
        var noStep = catalog.Load(new TemplateRef { Id = "test-A1", Version = "test-1" }, CancellationToken.None);
        Assert.That(noStep.Template, Is.Null);
        Assert.That(noStep.Diagnostics.Any(item => item.Details!["path"] == "pageStep"), Is.True, Dump(noStep.Diagnostics));

        stage.Reset();
        Rewrite(stage.TemplatePath, token => token["resolvedRowPitch"] = 9);
        var pitch = catalog.Load(new TemplateRef { Id = "test-A1", Version = "test-1" }, CancellationToken.None);
        Assert.That(pitch.Template, Is.Null);
        Assert.That(pitch.Diagnostics.Any(item => item.Details!["path"] == "resolvedRowPitch"), Is.True, Dump(pitch.Diagnostics));

        stage.Reset();
        Rewrite(stage.StandardPath, token => token["styles"]!["heading1"]!["beforeSlots"] = 1);
        var slots = catalog.Load(new StandardRef { Id = "test-note", Version = "test-1" }, CancellationToken.None);
        Assert.That(slots.Standard, Is.Null);
        Assert.That(slots.Diagnostics.Any(item => item.Message.Contains("前后槽")), Is.True, Dump(slots.Diagnostics));

        var cancelled = catalog.Load(new StandardRef { Id = "test-note", Version = "test-1" }, new CancellationToken(true));
        Assert.That(cancelled.Standard, Is.Null);
        Assert.That(cancelled.Diagnostics.Single().Code, Is.EqualTo(DiagnosticCodes.Cancelled));
    }

    [Test]
    public void MissingFontAndDifferentExistingStyleAreRejected()
    {
        using var stage = new PackageStage();
        var catalog = new DirectoryPackageCatalog(stage.Root, allowTestFixtures: true);
        var loaded = catalog.Load(new StandardRef { Id = "test-note", Version = "test-1" }, CancellationToken.None);
        Assert.That(loaded.Success, Is.True, Dump(loaded.Diagnostics));
        var standard = loaded.Standard!;
        var validator = new PackageValidator();

        var missing = validator.ValidateFonts(standard, identity => identity == "TESTFONT-BIG.shx");
        Assert.That(missing.Any(item => item.Code == DiagnosticCodes.EFontMissing && item.Details!["path"].EndsWith("fileIdentity")), Is.True, Dump(missing));
        var present = validator.ValidateFonts(standard, _ => true);
        Assert.That(present.Any(item => item.Severity == Severity.Error), Is.False, Dump(present));

        var versioned = PackageValidator.VersionedStyleName(standard, "body");
        var conflict = validator.ValidateStyleReuse(standard, new[]
        {
            new InstalledStyleSnapshot { Name = "Standard", FontFamily = "Other", TextHeight = 9, WidthFactor = 1, ObliqueAngle = 15 },
            new InstalledStyleSnapshot { Name = versioned, FontFamily = "TESTFONT", TextHeight = 9, WidthFactor = 0.8, ObliqueAngle = 0 }
        });
        Assert.That(conflict.Select(item => item.Code), Is.EqualTo(new[] { DiagnosticCodes.EStyleConflict }));
        Assert.That(conflict.Single().Message, Does.Contain(versioned).And.Contain("不会修改旧样式"));

        var same = validator.ValidateStyleReuse(standard, new[]
        {
            new InstalledStyleSnapshot
            {
                Name = versioned,
                FontFamily = "TESTFONT",
                TextHeight = standard.TextHeight,
                WidthFactor = standard.WidthFactor,
                ObliqueAngle = standard.ObliqueAngle
            }
        });
        Assert.That(same, Is.Empty);
    }

    [Test]
    public void TestPapersKeepIndependentColumnCounts()
    {
        using var stage = new PackageStage();
        var catalog = new DirectoryPackageCatalog(stage.Root, allowTestFixtures: true);
        var standard = catalog.Load(new StandardRef { Id = "test-note", Version = "test-1" }, CancellationToken.None).Standard!;
        var a1 = catalog.Load(new TemplateRef { Id = "test-A1", Version = "test-1" }, CancellationToken.None).Template!;
        var validator = new PackageValidator();
        var a2 = CopyPaper(a1, PaperCode.A2, new[] { Column(a1.Columns[0], "only", 10, 180) });
        var a3 = CopyPaper(a1, PaperCode.A3, new[]
        {
            Column(a1.Columns[0], "a", 10, 60),
            Column(a1.Columns[0], "b", 70, 120),
            Column(a1.Columns[0], "c", 130, 190)
        });
        Assert.That(validator.ValidateTemplate(a1, standard), Is.Empty);
        Assert.That(validator.ValidateTemplate(a2, standard), Is.Empty);
        Assert.That(validator.ValidateTemplate(a3, standard), Is.Empty);
        Assert.That(new[] { a1.Columns.Count, a2.Columns.Count, a3.Columns.Count }, Is.EqualTo(new[] { 2, 1, 3 }));
        a1.Columns.Clear();
        Assert.That(validator.ValidateTemplate(a1, standard).Any(item => item.Details!["path"] == "columns"), Is.True);
    }

    [Test]
    public void ListTemplatesSummarizesEveryPackageAndFailsWithoutDirectory()
    {
        using var stage = new PackageStage();
        var catalog = new DirectoryPackageCatalog(stage.Root, allowTestFixtures: false);
        var list = catalog.ListTemplates(CancellationToken.None);
        Assert.That(list.Diagnostics, Is.Empty);
        var only = list.Templates.Single();
        Assert.That(only.TemplateId, Is.EqualTo("test-A1"));
        Assert.That(only.Version, Is.EqualTo("test-1"));
        Assert.That(only.PaperCode, Is.EqualTo("A1"));
        Assert.That(only.DisciplineCode, Is.EqualTo("structure"));
        Assert.That(only.Classification, Is.EqualTo("test-fixture"));
        Assert.That(only.Calibrated, Is.True);
        Assert.That(only.FilePath, Is.EqualTo(stage.TemplatePath));

        var missingRoot = Path.Combine(Path.GetTempPath(), "jsr-missing-" + Guid.NewGuid().ToString("N"));
        var empty = new DirectoryPackageCatalog(missingRoot, allowTestFixtures: false).ListTemplates(CancellationToken.None);
        Assert.That(empty.Templates, Is.Empty);
        Assert.That(empty.Diagnostics.Single().Severity, Is.EqualTo(Severity.Error));

        var cancelled = catalog.ListTemplates(new CancellationToken(true));
        Assert.That(cancelled.Templates, Is.Empty);
        Assert.That(cancelled.Diagnostics.Single().Code, Is.EqualTo(DiagnosticCodes.Cancelled));
    }

    private static LayoutTemplate CopyPaper(LayoutTemplate source, PaperCode paper, ColumnGeometry[] columns)
    {
        return new LayoutTemplate
        {
            TemplateId = "test-" + paper,
            Version = source.Version,
            DisciplineCode = source.DisciplineCode,
            PaperCode = paper,
            Status = source.Status,
            Orientation = source.Orientation,
            Unit = source.Unit,
            StandardRef = source.StandardRef,
            Anchor = source.Anchor,
            PageBounds = source.PageBounds,
            Columns = new System.Collections.Generic.List<ColumnGeometry>(columns),
            PageStep = source.PageStep,
            FramePolicy = source.FramePolicy
        };
    }

    private static ColumnGeometry Column(ColumnGeometry source, string id, double left, double right)
    {
        return new ColumnGeometry
        {
            ColumnId = id,
            Left = left,
            Right = right,
            Top = source.Top,
            Bottom = source.Bottom,
            FirstBaselineY = source.FirstBaselineY,
            RowCount = source.RowCount
        };
    }

    private static void Rewrite(string path, Action<JObject> change)
    {
        var token = JObject.Parse(File.ReadAllText(path));
        change(token);
        File.WriteAllText(path, token.ToString());
    }

    private static string Dump(System.Collections.Generic.IReadOnlyList<Diagnostic> diagnostics)
    {
        return string.Join(" | ", diagnostics.Select(item => item.Code + ":" + item.Details?["path"] + ":" + item.Message));
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("找不到仓库根目录。");
    }

    private sealed class PackageStage : IDisposable
    {
        public PackageStage()
        {
            Root = Path.Combine(StandardPackageTests.Root(), "artifacts", "t06-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(Root, "standards", "test-note"));
            Directory.CreateDirectory(Path.Combine(Root, "templates", "test-A1"));
            var fixtures = Path.Combine(StandardPackageTests.Root(), "tests", "Justified.SpecificationReflow.AutoCAD.Core.Tests", "Fixtures");
            StandardPath = Path.Combine(Root, "standards", "test-note", "test-1.json");
            TemplatePath = Path.Combine(Root, "templates", "test-A1", "test-1.json");
            File.Copy(Path.Combine(fixtures, "test-note-standard.json"), StandardPath);
            File.Copy(Path.Combine(fixtures, "test-note-a1.json"), TemplatePath);
            _standard = File.ReadAllText(StandardPath);
            _template = File.ReadAllText(TemplatePath);
        }

        public string Root { get; }

        public string StandardPath { get; }

        public string TemplatePath { get; }

        public void Reset()
        {
            File.WriteAllText(StandardPath, _standard);
            File.WriteAllText(TemplatePath, _template);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }

        private readonly string _standard;
        private readonly string _template;
    }
}
