#nullable disable
using System;
using System.IO;
using System.Linq;
using System.Threading;
using Justified.SpecificationReflow.AutoCAD.Application;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using Justified.SpecificationReflow.AutoCAD.Contracts.Geometry;
using Justified.SpecificationReflow.AutoCAD.Contracts.Layout;
using Justified.SpecificationReflow.AutoCAD.Contracts.Ports;
using Justified.SpecificationReflow.AutoCAD.Contracts.Rendering;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;
using Justified.SpecificationReflow.AutoCAD.Contracts.Templates;
using Justified.SpecificationReflow.AutoCAD.DocxAdapter;
using Justified.SpecificationReflow.AutoCAD.LayoutEngine;
using Justified.SpecificationReflow.AutoCAD.Standards;
using NUnit.Framework;

namespace Justified.SpecificationReflow.AutoCAD.Core.Tests;

public class NotePlacementTests
{
    [Test]
    public void TwoAnchorsDifferByTheSameVectorAndScaleMovesThePage()
    {
        var template = LayoutSamples.Columns(new[] { 10d, 10d, 10d }, 2, 7.2, -6.2);
        template.Columns[0].Left = -710;
        template.Columns[0].Right = -700;
        template.PageStep = new Point2 { X = 861, Y = 0 };
        var standard = LayoutSamples.Standard();
        standard.StandardId = "jsr-note";
        standard.Version = "1.0.0";
        standard.LayerPolicy.LayerName = "JSR_NOTE_TEXT";
        template.StandardRef = new StandardRef { Id = standard.StandardId, Version = standard.Version };
        var layout = LayoutSamples.Engine().Layout(
            Lines("甲", 7),
            standard,
            template,
            new FakeMeasure(),
            CancellationToken.None);
        LayoutSamples.Ok(layout);

        var origin = Place(layout, template, standard, 0, 0, 1);
        var shifted = Place(layout, template, standard, 100, -20, 1);
        Assert.That(origin.Success, Is.True, Dump(origin.Diagnostics));
        Assert.That(shifted.Texts, Has.Count.EqualTo(origin.Texts.Count));
        for (var index = 0; index < origin.Texts.Count; index++)
        {
            Assert.That(shifted.Texts[index].Position.X - origin.Texts[index].Position.X, Is.EqualTo(100).Within(1e-6));
            Assert.That(shifted.Texts[index].Position.Y - origin.Texts[index].Position.Y, Is.EqualTo(-20).Within(1e-6));
            Assert.That(shifted.Texts[index].Text, Is.EqualTo(origin.Texts[index].Text));
        }

        Assert.That(origin.Texts[0].Position.X, Is.EqualTo(-710).Within(1e-6));
        Assert.That(origin.Texts[0].Position.Y, Is.EqualTo(-6.2).Within(1e-6));
        Assert.That(origin.Texts[0].Height, Is.EqualTo(4.5).Within(1e-9));
        Assert.That(origin.Texts[0].WidthFactor, Is.EqualTo(0.75).Within(1e-9));
        Assert.That(origin.Texts[0].StyleName, Is.EqualTo("jsr-note-1.0.0-body"));
        Assert.That(origin.Texts[0].Layer, Is.EqualTo("JSR_NOTE_TEXT"));
        var last = origin.Texts.Last();
        Assert.That(last.PageIndex, Is.EqualTo(1));
        Assert.That(last.Position.X, Is.EqualTo(-710 + 861).Within(1e-6));

        var scaled = Place(layout, template, standard, 0, 0, 100);
        Assert.That(scaled.Texts[0].Position.X, Is.EqualTo(-71000).Within(1e-4));
        Assert.That(scaled.Texts[0].Position.Y, Is.EqualTo(-620).Within(1e-4));
        Assert.That(scaled.Texts[0].Height, Is.EqualTo(450).Within(1e-6));
        Assert.That(scaled.Texts[0].WidthFactor, Is.EqualTo(0.75).Within(1e-9));
        Assert.That(scaled.Texts.Last().Position.X - scaled.Texts[0].Position.X, Is.EqualTo(86100).Within(1e-2));
    }

    [Test]
    public void SpacersSuperscriptsAndLayoutErrorsProduceNoText()
    {
        var template = LayoutSamples.Columns(new[] { 10d }, 4, 7.2, 90);
        var standard = LayoutSamples.Standard();
        var layout = LayoutSamples.Engine().Layout(
            LayoutSamples.DocumentOf(LayoutSamples.Text(0, "甲"), LayoutSamples.Spacer(1), LayoutSamples.Text(2, "乙")),
            standard,
            template,
            new FakeMeasure(),
            CancellationToken.None);
        var placed = Place(layout, template, standard, 0, 0, 1);
        Assert.That(placed.Success, Is.True, Dump(placed.Diagnostics));
        Assert.That(placed.Texts.Select(item => item.Text), Is.EqualTo(new[] { "甲", "乙" }));

        var superscript = Copy(layout);
        superscript.Pages[0].Columns[0].Rows[0].VisualLine.RenderRuns[0].ResolvedStyle.Semantic = RunSemantic.Superscript;
        var blocked = NotePlacement.Create(superscript, template, standard, Transform(0, 0, 1), CancellationToken.None);
        Assert.That(blocked.Success, Is.False);
        Assert.That(blocked.Texts, Is.Empty);
        Assert.That(blocked.Diagnostics.Any(item => item.Message.Contains("上下标")), Is.True);

        var tooWide = LayoutSamples.Engine().Layout(
            LayoutSamples.DocumentOf(LayoutSamples.Text(0, "甲")),
            standard,
            LayoutSamples.Columns(1),
            new FakeMeasure { Unit = 10, Cjk = 10 },
            CancellationToken.None);
        var failed = NotePlacement.Create(tooWide, LayoutSamples.Columns(1), standard, Transform(0, 0, 1), CancellationToken.None);
        Assert.That(failed.Success, Is.False);
        Assert.That(failed.Texts, Is.Empty);
        Assert.That(failed.Diagnostics.Any(item => item.Code == DiagnosticCodes.ENoLegalBreak), Is.True);
    }

    [Test]
    public void UcsTranslationAndQuarterTurnChangeTheAnchorOnly()
    {
        var moved = NoteCoordinates.UcsToWcs(new Point2 { X = 5, Y = 2 }, new Point2 { X = 10, Y = 20 }, 0);
        Assert.That(moved.X, Is.EqualTo(15).Within(1e-9));
        Assert.That(moved.Y, Is.EqualTo(22).Within(1e-9));
        var turned = NoteCoordinates.UcsToWcs(new Point2 { X = 5, Y = 0 }, new Point2 { X = 0, Y = 0 }, Math.PI / 2);
        Assert.That(turned.X, Is.EqualTo(0).Within(1e-9));
        Assert.That(turned.Y, Is.EqualTo(5).Within(1e-9));
    }

    [Test]
    public void DraftSessionFillsPendingInMemoryAndLeavesTheFileAlone()
    {
        var directory = Path.Combine(Root(), "standards", "drafts");
        var notePath = Path.Combine(directory, "jsr-note-1.0.0.json");
        var templatePath = Path.Combine(directory, "jsr-A3-two-column-1.0.0.json");
        var noteBefore = File.ReadAllText(notePath);
        var templateBefore = File.ReadAllText(templatePath);
        var session = new DraftSessionLoader().Load(directory, "A3", LayoutEngineInfo.LineBreakRuleVersion, CancellationToken.None);
        Assert.That(session.Success, Is.True, Dump(session.Diagnostics));
        Assert.That(session.Standard.LineBreakRuleVersion, Is.EqualTo(LayoutEngineInfo.LineBreakRuleVersion));
        Assert.That(session.Standard.MeasurementTolerance, Is.EqualTo(0));
        Assert.That(session.Template.Status, Is.EqualTo(TemplateStatus.Calibrated));
        Assert.That(session.Template.Columns, Has.Count.EqualTo(2));
        Assert.That(session.Diagnostics.Any(item => item.Severity == Severity.Info && item.Code == "DRAFT"), Is.True);
        Assert.That(File.ReadAllText(notePath), Is.EqualTo(noteBefore));
        Assert.That(File.ReadAllText(templatePath), Is.EqualTo(templateBefore));

        var layout = LayoutSamples.Engine().Layout(
            LayoutSamples.DocumentOf(LayoutSamples.Text(0, "材料")),
            session.Standard,
            session.Template,
            new FakeMeasure { Unit = 10, Cjk = 10 },
            CancellationToken.None);
        var placed = NotePlacement.Create(layout, session.Template, session.Standard, Transform(0, 0, 1), CancellationToken.None);
        Assert.That(placed.Success, Is.True, Dump(placed.Diagnostics));
        Assert.That(placed.Texts.Single().Position.X, Is.EqualTo(session.Template.Columns[0].Left).Within(1e-6));
        Assert.That(placed.Texts.Single().Position.Y, Is.EqualTo(session.Template.Columns[0].FirstBaselineY).Within(1e-6));
    }

    [Test]
    public void WordToPlacementDoesNotKeepTextWhenParsingFails()
    {
        var parser = new FixedParser(new DocumentParseResult(null, new[]
        {
            new Diagnostic { Code = DiagnosticCodes.EUnsupportedContent, Severity = Severity.Error, Stage = DiagnosticStage.Parse, Message = "有表格。" }
        }));
        var service = new NoteGenerationService(parser, LayoutSamples.Engine(), new FakeMeasure());
        var result = service.Generate(new StubSource(), LayoutSamples.Standard(), LayoutSamples.Columns(20), Transform(0, 0, 1), CancellationToken.None);
        Assert.That(result.Success, Is.False);
        Assert.That(result.Texts, Is.Empty);
        Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(DiagnosticCodes.EUnsupportedContent));
    }

    [Test]
    public void ASecondPlanMatchesTheFirstAndKeepsNoEntityIdentity()
    {
        var standard = LayoutSamples.Standard();
        var template = LayoutSamples.Columns(30);
        var service = new NoteGenerationService(new FixedParser(new DocumentParseResult(LayoutSamples.DocumentOf(LayoutSamples.Text(0, "材料要求")), new Diagnostic[0])), LayoutSamples.Engine(), new FakeMeasure());
        var transform = Transform(5, 6, 1);
        var first = service.Generate(new StubSource(), standard, template, transform, CancellationToken.None);
        var second = service.Generate(new StubSource(), standard, template, transform, CancellationToken.None);
        Assert.That(first.Success, Is.True, Dump(first.Diagnostics));
        Assert.That(second.Texts.Select(item => item.Position.X), Is.EqualTo(first.Texts.Select(item => item.Position.X)));
        Assert.That(second.Texts.Select(item => item.Position.Y), Is.EqualTo(first.Texts.Select(item => item.Position.Y)));
        Assert.That(second.Texts.Select(item => item.Text), Is.EqualTo(first.Texts.Select(item => item.Text)));
    }

    [Test]
    public void DemoDocxPlacesTheHeadingAtTheFirstColumnOrigin()
    {
        var path = Path.Combine(Root(), "测试文件", "示例-单行说明.docx");
        var parser = new DocxDocumentParser(new DocxParseOptions
        {
            DocumentId = "dn-note",
            DisciplineCode = "structure",
            StyleMap = DemoMap()
        });
        var parsed = parser.Parse(new DocxFileSource(path), new ParseProfile
        {
            Standard = new StandardRef { Id = "jsr-note", Version = "1.0.0" }
        }, CancellationToken.None);
        Assert.That(parsed.Success, Is.True, Dump(parsed.Diagnostics));
        Assert.That(parsed.Document.Blocks.SelectMany(block => block.Runs).All(run => run.Semantic == RunSemantic.Normal), Is.True);

        var session = new DraftSessionLoader().Load(Path.Combine(Root(), "standards", "drafts"), "A1", LayoutEngineInfo.LineBreakRuleVersion, CancellationToken.None);
        Assert.That(session.Success, Is.True, Dump(session.Diagnostics));
        var generated = new NoteGenerationService(parser, LayoutSamples.Engine(), new FakeMeasure { Unit = 10, Cjk = 10 }).Generate(
            new DocxFileSource(path), session.Standard, session.Template, Transform(0, 0, 1), CancellationToken.None);
        Assert.That(generated.Success, Is.True, Dump(generated.Diagnostics));
        Assert.That(generated.Texts[0].Text, Is.EqualTo("设计说明"));
        Assert.That(generated.Texts[0].Position.X, Is.EqualTo(session.Template.Columns[0].Left).Within(1e-6));
        Assert.That(generated.Texts[0].Position.Y, Is.EqualTo(session.Template.Columns[0].FirstBaselineY).Within(1e-6));
        Assert.That(generated.Texts.Select(item => item.Text), Does.Contain("第一行说明"));
        Assert.That(generated.Texts.Select(item => item.Text), Does.Contain("第二行说明"));
        Assert.That(generated.Layout.Pages.SelectMany(page => page.Columns).SelectMany(column => column.Rows).Count(row => row.Occupancy == Occupancy.Spacer), Is.EqualTo(1));
    }

    private static StyleMap DemoMap()
    {
        var map = new StyleMap();
        map.Entries.Add(new StyleMapEntry { Match = StyleMapMatch.StyleId, Key = "Heading1", Target = BlockType.Heading1 });
        map.Entries.Add(new StyleMapEntry { Match = StyleMapMatch.StyleId, Key = "Heading2", Target = BlockType.Heading2 });
        map.Entries.Add(new StyleMapEntry { Match = StyleMapMatch.StyleId, Key = "Normal", Target = BlockType.Paragraph });
        return map;
    }

    private static NotePlacementResult Place(LayoutResult layout, LayoutTemplate template, InstitutionStandard standard, double x, double y, double scale)
    {
        return NotePlacement.Create(layout, template, standard, Transform(x, y, scale), CancellationToken.None);
    }

    private static RenderTransform Transform(double x, double y, double scale)
    {
        return new RenderTransform
        {
            AnchorWcs = new Point2 { X = x, Y = y },
            UnitScale = scale,
            TargetSpace = TargetSpace.Model
        };
    }

    private static Document Lines(string text, int count)
    {
        var document = LayoutSamples.DocumentOf();
        for (var index = 0; index < count; index++)
            document.Blocks.Add(LayoutSamples.Text(index, text));
        return document;
    }

    private static LayoutResult Copy(LayoutResult layout)
    {
        return layout;
    }

    private static string Dump(System.Collections.Generic.IEnumerable<Diagnostic> diagnostics)
    {
        return string.Join(" | ", diagnostics.Select(item => item.Code + " " + item.Message));
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        if (dir == null) throw new InvalidOperationException("找不到仓库根目录。");
        return dir.FullName;
    }

    private sealed class FixedParser : IDocumentParser
    {
        private readonly DocumentParseResult _result;

        public FixedParser(DocumentParseResult result)
        {
            _result = result;
        }

        public DocumentParseResult Parse(IDocumentSource source, ParseProfile profile, CancellationToken cancellationToken)
        {
            return _result;
        }
    }

    private sealed class StubSource : IDocumentSource
    {
        public SourceInfo Info { get; } = new SourceInfo { Kind = DocumentSourceKind.Docx, Name = "sample.docx" };

        public Stream OpenRead()
        {
            return new MemoryStream();
        }
    }
}
