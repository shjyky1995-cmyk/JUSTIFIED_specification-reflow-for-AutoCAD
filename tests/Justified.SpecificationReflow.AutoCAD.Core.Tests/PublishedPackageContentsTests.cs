using System;
using System.IO;
using System.Linq;
using System.Threading;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;
using Justified.SpecificationReflow.AutoCAD.Standards;
using NUnit.Framework;

namespace Justified.SpecificationReflow.AutoCAD.Core.Tests;

public class PublishedPackageContentsTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    [Test]
    public void PublishedCatalogOffersExactlyOneUsableTemplatePerSupportedPaper()
    {
        var root = Path.Combine(RepoRoot(), "standards", "published");
        var list = new DirectoryPackageCatalog(root, allowTestFixtures: false).ListTemplates(CancellationToken.None);
        var usable = list.Templates.Where(item =>
            string.Equals(item.Classification, "production", StringComparison.OrdinalIgnoreCase)
            && item.Calibrated).ToList();
        foreach (var paper in new[] { "A1", "A2", "A3" })
        {
            var matches = usable.Where(item => string.Equals(item.PaperCode, paper, StringComparison.OrdinalIgnoreCase)).ToList();
            Assert.That(matches.Count, Is.EqualTo(1), paper + " 必须且只能有一份可用的已发布模板；随包不能没有默认模板。");
        }
    }

    [Test]
    public void PublishedCatalogLoadsStandardAndTemplatesWithoutErrors()
    {
        var root = Path.Combine(RepoRoot(), "standards", "published");
        var catalog = new DirectoryPackageCatalog(root, allowTestFixtures: false);
        var list = catalog.ListTemplates(CancellationToken.None);
        var usable = list.Templates.Where(item =>
            string.Equals(item.Classification, "production", StringComparison.OrdinalIgnoreCase)
            && item.Calibrated).ToList();
        Assert.That(usable, Is.Not.Empty);
        foreach (var summary in usable)
        {
            var loaded = catalog.Load(new TemplateRef { Id = summary.TemplateId, Version = summary.Version }, CancellationToken.None);
            Assert.That(loaded.Success, Is.True, summary.TemplateId + " " + summary.Version);
            Assert.That(loaded.Template, Is.Not.Null);
        }
    }
}
