using System;
using System.IO;
using System.Linq;
using System.Threading;
using Justified.SpecificationReflow.AutoCAD.Application;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using Justified.SpecificationReflow.AutoCAD.Contracts.Geometry;
using Justified.SpecificationReflow.AutoCAD.Contracts.Rendering;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;
using Justified.SpecificationReflow.AutoCAD.Contracts.Templates;
using Justified.SpecificationReflow.AutoCAD.DocxAdapter;
using Justified.SpecificationReflow.AutoCAD.LayoutEngine;
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
    public void ShippedTemplatePlacesNoteCenteredBelowTitleBlockWithTightPages()
    {
        var root = Path.Combine(RepoRoot(), "standards", "published");
        var catalog = new DirectoryPackageCatalog(root, allowTestFixtures: false);
        var standardLoad = catalog.Load(new StandardRef { Id = "jsr-note", Version = "1.0.0" }, CancellationToken.None);
        Assert.That(standardLoad.Success, Is.True);
        var templateLoad = catalog.Load(new TemplateRef { Id = "jsr-A2-three-column", Version = "1.1.0" }, CancellationToken.None);
        Assert.That(templateLoad.Success, Is.True, string.Join("；", templateLoad.Diagnostics.Select(item => item.Code + " " + item.Message)));
        var template = templateLoad.Template!;
        var pageWidth = template.PageBounds.Right - template.PageBounds.Left;

        var builder = new DocxFixtureBuilder();
        builder.Styles.Add(DocxFixtureBuilder.ParagraphStyle("Normal", "Normal"));
        for (var i = 0; i < 40; i++)
            builder.Body.Add(DocxFixtureBuilder.Paragraph("Normal", DocxFixtureBuilder.TextRun(new string('甲', 40) + "。")));
        var service = new NoteGenerationService(
            new DocxDocumentParser(new DocxParseOptions
            {
                DocumentId = "geometry",
                DisciplineCode = "structure",
                StyleMap = new StyleMap { Entries = { new StyleMapEntry { Match = StyleMapMatch.StyleId, Key = "Normal", Target = BlockType.Paragraph } } }
            }),
            new SpecificationLayoutEngine(GenerationLimits.Default.MaxPages),
            new FakeMeasure());
        var result = service.Generate(
            new DocxBytesSource("geometry.docx", builder.Build()),
            standardLoad.Standard!,
            template,
            new RenderTransform { AnchorWcs = new Point2 { X = 1000, Y = 2000 }, UnitScale = 1, TargetSpace = TargetSpace.Model },
            CancellationToken.None,
            GenerationLimits.Default);
        Assert.That(result.Success, Is.True, string.Join("；", result.Diagnostics.Select(item => item.Code + " " + item.Message)));
        Assert.That(result.Texts.Count, Is.GreaterThan(0));

        const double anchorX = 1000;
        const double anchorY = 2000;
        var firstColumnLeft = template.Columns.Min(column => column.Left);
        var first = result.Texts.OrderBy(text => text.Position.X).ThenBy(text => text.Position.Y).First();
        Assert.That(first.Position.X, Is.EqualTo(anchorX + firstColumnLeft).Within(0.001), "首行应从锚点右侧第一栏左缘开始。");
        Assert.That(result.Texts.All(text => text.Position.Y < anchorY - 60), Is.True, "顶部 60mm 预留图框与标题栏，说明不得进入。");
        var columnsLeft = template.Columns.Min(column => column.Left);
        var columnsRight = template.Columns.Max(column => column.Right);
        Assert.That(columnsLeft, Is.EqualTo(pageWidth - columnsRight).Within(0.001), "栏组在整页内左右边距应相等（横向居中）。");
        if (result.Layout!.Pages.Count > 1)
        {
            var step = result.Layout.Pages[1].PageOffset.X - result.Layout.Pages[0].PageOffset.X;
            Assert.That(step, Is.EqualTo(pageWidth).Within(0.001), "页与页应紧贴，间距等于页宽。");
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
            Assert.That(loaded.Success, Is.True, summary.TemplateId + " " + summary.Version + "："
                + string.Join("；", loaded.Diagnostics.Select(item => item.Code + " " + item.Message)));
            Assert.That(loaded.Template, Is.Not.Null);
        }
    }
}
