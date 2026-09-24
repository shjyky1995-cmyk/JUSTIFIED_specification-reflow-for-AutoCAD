#nullable disable
using System.IO;
using System.Linq;
using System.Threading;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using ModelDocument = Justified.SpecificationReflow.AutoCAD.Contracts.Documents.Document;
using Justified.SpecificationReflow.AutoCAD.Contracts.Layout;
using Justified.SpecificationReflow.AutoCAD.Contracts.Ports;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;
using Justified.SpecificationReflow.AutoCAD.Contracts.Templates;
using Justified.SpecificationReflow.AutoCAD.DocxAdapter;
using Justified.SpecificationReflow.AutoCAD.LayoutEngine;
using NUnit.Framework;

namespace Justified.SpecificationReflow.AutoCAD.Core.Tests;

public class LineBreakTests
{
    [Test]
    public void ChinesePunctuationAtTheBoundaryMovesBackInsteadOfStartingALine()
    {
        var narrow = Lay("中文测试，后续", 4);
        Assert.That(LayoutSamples.Lines(narrow), Is.EqualTo(new[] { "中文测", "试，后续" }));
        var wider = Lay("中文测试，后续", 5);
        Assert.That(LayoutSamples.Lines(wider), Is.EqualTo(new[] { "中文测试，", "后续" }));
        Assert.That(LayoutSamples.Rebuild(Sample("中文测试，后续"), wider), Is.EqualTo("中文测试，后续"));
    }

    [Test]
    public void OpeningPunctuationDoesNotEndALineWhenTextFollows()
    {
        var result = Lay("见（说明文字）", 3);
        Assert.That(LayoutSamples.Lines(result), Is.EqualTo(new[] { "见（说", "明文", "字）" }));
    }

    [Test]
    public void EnglishBreaksAtSpacesAndKeepsTheSpaceInTheSource()
    {
        var document = Sample("hello world");
        var result = Lay(document, 6);
        Assert.That(LayoutSamples.Lines(result), Is.EqualTo(new[] { "hello", "world" }));
        Assert.That(LayoutSamples.Lines(result)[1], Does.Not.StartWith(" "));
        Assert.That(LayoutSamples.Rebuild(document, result), Is.EqualTo("hello world"));

        var doubled = Sample("hello  world");
        var wrapped = Lay(doubled, 6);
        Assert.That(LayoutSamples.Lines(wrapped), Is.EqualTo(new[] { "hello", "world" }));
        Assert.That(LayoutSamples.Rebuild(doubled, wrapped), Is.EqualTo("hello  world"));
    }

    [Test]
    public void EngineeringTokensMoveWholeAndDoNotSwallowOrdinaryWords()
    {
        var measure = new FakeMeasure { Unit = 1, Cjk = 2 };
        AssertToken("强度C30满足", 4, measure, "强度", "C30", "满足");
        AssertToken("钢筋HRB400连接", 7, measure, "钢筋", "HRB400", "连接");
        AssertToken("直径Φ20@200布置", 8, measure, "直径", "Φ20@200", "布置");
        AssertToken("取值0.10g即可", 6, measure, "取值", "0.10g", "即可");
        AssertToken("荷载20kN/m²标准", 8, measure, "荷载", "20kN/m²", "标准");
        AssertToken("厚度35mm即可", 4, measure, "厚度", "35mm", "即可");
        AssertToken("坡度1/1000满足", 7, measure, "坡度", "1/1000", "满足");
        AssertToken("见GB 50010-2010条", 14, measure, "见", "GB 50010-2010", "条");
        AssertToken("按GB/T 50011-2010执行", 16, measure, "按", "GB/T 50011-2010", "执行");

        var prose = Lay(Sample("Use Code now"), 5, new FakeMeasure());
        Assert.That(LayoutSamples.Lines(prose), Is.EqualTo(new[] { "Use", "Code", "now" }));
    }

    [Test]
    public void OverlongTokenSplitsOnGraphemesAndRecordsTheBreak()
    {
        var result = Lay("ABCDEFGHIJKL", 5);
        Assert.That(LayoutSamples.Lines(result), Is.EqualTo(new[] { "ABCDE", "FGHIJ", "KL" }));
        var warnings = result.Diagnostics.Where(item => item.Code == DiagnosticCodes.WTokenSplit).ToArray();
        Assert.That(warnings.Select(item => item.Details["breakIndex"]), Is.EqualTo(new[] { "5", "10" }));
        Assert.That(warnings.All(item => item.Details["token"] == "ABCDEFGHIJKL" && item.Severity == Severity.Warning), Is.True);
        Assert.That(result.Statistics.WarningCount, Is.EqualTo(2));
        Assert.That(result.Pages, Is.Not.Empty);
    }

    [Test]
    public void ACharacterWiderThanTheColumnStopsWithoutAPage()
    {
        var measure = new FakeMeasure { Unit = 10, Cjk = 10 };
        var result = Lay(Sample("甲"), 5, measure);
        Assert.That(result.Pages, Is.Empty);
        Assert.That(result.Diagnostics.Single(item => item.Severity == Severity.Error).Code, Is.EqualTo(DiagnosticCodes.ENoLegalBreak));
    }

    [Test]
    public void KinsokuThatCannotFormALineStops()
    {
        var result = Lay("（说", 1);
        Assert.That(result.Pages, Is.Empty);
        Assert.That(result.Diagnostics.Single(item => item.Severity == Severity.Error).Code, Is.EqualTo(DiagnosticCodes.ENoLegalBreak));
        Assert.That(result.Diagnostics.Single(item => item.Severity == Severity.Error).Details["text"], Does.Contain("（"));
    }

    [Test]
    public void NumberingStaysInlineOnTheFirstLineAndIsNotRepeated()
    {
        var document = LayoutSamples.DocumentOf(LayoutSamples.Text(0, "材料要求说明", "1.1"));
        var result = Lay(document, 8);
        LayoutSamples.Ok(result);
        Assert.That(LayoutSamples.Lines(result), Is.EqualTo(new[] { "1.1  材料要", "求说明" }));
        Assert.That(string.Join("", LayoutSamples.Lines(result)).Split(new[] { "1.1" }, System.StringSplitOptions.None).Length - 1, Is.EqualTo(1));
        Assert.That(result.Pages[0].Columns[0].Rows[1].VisualLine.Text, Does.Not.Contain("1.1"));
        Assert.That(result.Pages[0].Columns[0].Rows[0].VisualLine.RenderRuns.Single().RelativeOrigin.X, Is.EqualTo(0));
        Assert.That(LayoutSamples.Rebuild(document, result), Is.EqualTo("材料要求说明"));
    }

    [Test]
    public void NumberingThatCannotShareTheLineWithTheFirstCharacterIsRejected()
    {
        var document = LayoutSamples.DocumentOf(LayoutSamples.Text(0, "材料", "1.1"));
        var result = Lay(document, 5);
        Assert.That(result.Pages, Is.Empty);
        Assert.That(result.Diagnostics.Single(item => item.Severity == Severity.Error).Code, Is.EqualTo(DiagnosticCodes.ENoLegalBreak));
        Assert.That(LayoutSamples.Lines(result), Is.Empty);
    }

    [Test]
    public void ExplicitBreaksEndTheLineAndConsecutiveBreaksKeepAnEmptySlot()
    {
        var single = LayoutSamples.DocumentOf(LayoutSamples.Runs(0, BlockType.Paragraph, null,
            LayoutSamples.Run("甲"), LayoutSamples.Run("\n"), LayoutSamples.Run("乙")));
        var once = Lay(single, 10);
        LayoutSamples.Ok(once);
        Assert.That(LayoutSamples.Lines(once), Is.EqualTo(new[] { "甲", "乙" }));
        Assert.That(LayoutSamples.Rows(once).Any(row => row.Occupancy == Occupancy.Spacer), Is.False);
        Assert.That(LayoutSamples.Rebuild(single, once), Is.EqualTo("甲\n乙"));

        var doubled = LayoutSamples.DocumentOf(LayoutSamples.Runs(1, BlockType.Paragraph, null,
            LayoutSamples.Run("甲"), LayoutSamples.Run("\n"), LayoutSamples.Run("\n"), LayoutSamples.Run("乙")));
        var twice = Lay(doubled, 10);
        LayoutSamples.Ok(twice);
        Assert.That(LayoutSamples.Rows(twice).Select(row => row.Occupancy), Is.EqualTo(new[] { Occupancy.Text, Occupancy.Spacer, Occupancy.Text }));
        Assert.That(LayoutSamples.Rows(twice)[1].VisualLine, Is.Null);
        Assert.That(LayoutSamples.Rebuild(doubled, twice), Is.EqualTo("甲\n\n乙"));
    }

    [Test]
    public void WholeLineMeasurementOverridesTheSumOfSeparateWidths()
    {
        var measure = new FakeMeasure { Cjk = 3 };
        measure.Exact["甲乙"] = 10;
        var result = Lay(Sample("甲乙"), 7, measure);
        LayoutSamples.Ok(result);
        Assert.That(LayoutSamples.Lines(result), Is.EqualTo(new[] { "甲", "乙" }));
        Assert.That(LayoutSamples.Rows(result).Select(row => row.VisualLine.MeasuredWidth), Is.EqualTo(new[] { 3d, 3d }));
    }

    [Test]
    public void InkWiderThanTheAdvanceForcesAnEarlierBreak()
    {
        var measure = new FakeMeasure { ExtraInkAfterLength = 1, ExtraInk = 5 };
        var result = Lay(Sample("ABCD"), 4, measure);
        LayoutSamples.Ok(result);
        Assert.That(LayoutSamples.Lines(result), Is.EqualTo(new[] { "A", "B", "C", "D" }));
    }

    [Test]
    public void MixedWidthsFollowMeasurementRatherThanCharacterCount()
    {
        var measure = new FakeMeasure { Unit = 1, Cjk = 2 };
        var result = Lay(Sample("1234中5"), 5, measure);
        LayoutSamples.Ok(result);
        Assert.That(LayoutSamples.Lines(result), Is.EqualTo(new[] { "1234", "中5" }));
        Assert.That(LayoutSamples.Rows(result)[0].VisualLine.MeasuredWidth, Is.EqualTo(4));
    }

    [Test]
    public void ParagraphsDoNotShareAVisualLine()
    {
        var document = LayoutSamples.DocumentOf(LayoutSamples.Text(0, "甲"), LayoutSamples.Text(1, "乙"));
        var result = Lay(document, 10);
        LayoutSamples.Ok(result);
        Assert.That(LayoutSamples.Lines(result), Is.EqualTo(new[] { "甲", "乙" }));
        Assert.That(result.Pages[0].Columns[0].Rows[0].VisualLine.SourceSlices.Single().ParagraphIndex, Is.EqualTo(0));
        Assert.That(result.Pages[0].Columns[0].Rows[1].VisualLine.SourceSlices.Single().ParagraphIndex, Is.EqualTo(1));
    }

    [Test]
    public void GeometryComesFromTheStandardAndNotFromFixedNumbers()
    {
        var standard = LayoutSamples.Standard(height: 5.5, widthFactor: 0.8, pitch: 9, indent: 2, hanging: 1);
        var template = LayoutSamples.Columns(new[] { 10d }, 4, 9, 90);
        var document = LayoutSamples.DocumentOf(LayoutSamples.Text(0, "甲乙丙丁戊己庚辛壬癸甲乙丙丁戊己庚辛"));
        var result = LayoutSamples.Engine().Layout(document, standard, template, new FakeMeasure(), CancellationToken.None);
        LayoutSamples.Ok(result);
        var rows = LayoutSamples.Rows(result);
        Assert.That(rows[0].VisualLine.Text, Is.EqualTo("甲乙丙丁戊己庚辛"));
        Assert.That(rows[0].VisualLine.RenderRuns.Single().RelativeOrigin.X, Is.EqualTo(2));
        Assert.That(rows[1].VisualLine.Text.Length, Is.EqualTo(9));
        Assert.That(rows[1].VisualLine.RenderRuns.Single().RelativeOrigin.X, Is.EqualTo(1));
        Assert.That(rows[0].VisualLine.RenderRuns.Single().ResolvedStyle.TextHeight, Is.EqualTo(5.5));
        Assert.That(rows[0].VisualLine.RenderRuns.Single().ResolvedStyle.WidthFactor, Is.EqualTo(0.8));
        Assert.That(rows[0].Baseline, Is.EqualTo(90));
        Assert.That(rows[1].Baseline, Is.EqualTo(81));
    }

    [Test]
    public void ALineUsesTheWidthOfTheColumnItIsPlacedIn()
    {
        var document = LayoutSamples.DocumentOf(LayoutSamples.Text(0, "甲乙丙丁戊己庚辛壬癸甲乙丙丁戊己庚辛"));
        var template = LayoutSamples.Columns(new[] { 4d, 9d }, 3, 7.2, 90);
        var result = LayoutSamples.Engine().Layout(document, LayoutSamples.Standard(), template, new FakeMeasure(), CancellationToken.None);
        LayoutSamples.Ok(result);
        Assert.That(result.Pages, Has.Count.EqualTo(1));
        Assert.That(result.Pages[0].Columns[0].Rows.Select(row => row.VisualLine.Text.Length), Is.EqualTo(new[] { 4, 4, 4 }));
        Assert.That(result.Pages[0].Columns[1].Rows.Single().VisualLine.Text.Length, Is.EqualTo(6));
        Assert.That(result.Pages[0].Columns[1].Rows.Single().VisualLine.SourceSlices.Single().ParagraphIndex, Is.EqualTo(0));
    }

    [Test]
    public void MeasureFailureAndCancellationDoNotProducePages()
    {
        var missing = new FakeMeasure
        {
            Failure = new Diagnostic
            {
                Code = DiagnosticCodes.EFontMissing,
                Severity = Severity.Error,
                Stage = DiagnosticStage.Measure,
                Message = "找不到字体。"
            },
            FailOnCall = 1
        };
        var failed = Lay(Sample("甲乙"), 10, missing);
        Assert.That(failed.Pages, Is.Empty);
        Assert.That(failed.Diagnostics.Single(item => item.Severity == Severity.Error).Code, Is.EqualTo(DiagnosticCodes.EFontMissing));

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var stopped = LayoutSamples.Engine().Layout(Sample("甲乙"), LayoutSamples.Standard(), LayoutSamples.Columns(10), new FakeMeasure(), cancelled.Token);
        Assert.That(stopped.Pages, Is.Empty);
        Assert.That(stopped.Diagnostics.Single().Code, Is.EqualTo(DiagnosticCodes.Cancelled));
    }

    [Test]
    public void SuperscriptStaysBlockedWithoutCalibration()
    {
        var document = LayoutSamples.DocumentOf(LayoutSamples.Runs(0, BlockType.Paragraph, null,
            LayoutSamples.Run("强度"), LayoutSamples.Run("2", RunSemantic.Superscript)));
        var result = LayoutSamples.Engine().Layout(document, LayoutSamples.Standard(calibrateScripts: false), LayoutSamples.Columns(20), new FakeMeasure(), CancellationToken.None);
        Assert.That(result.Pages, Is.Empty);
        Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(DiagnosticCodes.ETemplateInvalid));
        Assert.That(result.Diagnostics.Single().Message, Does.Contain("上下标"));
    }

    [Test]
    public void SuperscriptGeneratesScaledRunWithBaselineOffset()
    {
        var standard = LayoutSamples.Standard();
        var document = LayoutSamples.DocumentOf(LayoutSamples.Runs(0, BlockType.Paragraph, null,
            LayoutSamples.Run("强度"), LayoutSamples.Run("2", RunSemantic.Superscript)));
        var result = LayoutSamples.Engine().Layout(document, standard, LayoutSamples.Columns(40), new FakeMeasure { Cjk = 4, Unit = 2 }, CancellationToken.None);
        LayoutSamples.Ok(result);
        var line = LayoutSamples.Rows(result).Single(row => row.Occupancy == Occupancy.Text).VisualLine!;
        Assert.That(line.Text, Is.EqualTo("强度2"));
        Assert.That(line.RenderRuns, Has.Count.EqualTo(2));

        var body = line.RenderRuns[0];
        Assert.That(body.Text, Is.EqualTo("强度"));
        Assert.That(body.ResolvedStyle.Semantic, Is.EqualTo(RunSemantic.Normal));
        Assert.That(body.ResolvedStyle.TextHeight, Is.EqualTo(4.5).Within(1e-9));
        Assert.That(body.BaselineOffset, Is.EqualTo(0));
        Assert.That(body.RelativeOrigin.X, Is.EqualTo(0));

        var script = line.RenderRuns[1];
        Assert.That(script.Text, Is.EqualTo("2"));
        Assert.That(script.ResolvedStyle.Semantic, Is.EqualTo(RunSemantic.Superscript));
        Assert.That(script.ResolvedStyle.TextHeight, Is.EqualTo(4.5 * ScriptCalibration.SuperscriptScale).Within(1e-9));
        Assert.That(script.BaselineOffset, Is.EqualTo(4.5 * ScriptCalibration.SuperscriptRise).Within(1e-9));
        Assert.That(script.RelativeOrigin.X, Is.EqualTo(body.MeasuredAdvance).Within(1e-9));
        Assert.That(script.RelativeOrigin.X, Is.GreaterThan(0));
    }

    [Test]
    public void LiteralCubicAndNegativeExponentUseCalibratedSuperscript()
    {
        var document = LayoutSamples.DocumentOf(LayoutSamples.Runs(0, BlockType.Paragraph, null,
            LayoutSamples.Run("强度 N/mm²；含量 3.0kg/m³；膨胀率 2.5×10⁻⁴")));
        var result = LayoutSamples.Engine().Layout(document, LayoutSamples.Standard(), LayoutSamples.Columns(200), new FakeMeasure { Cjk = 4, Unit = 2 }, CancellationToken.None);
        LayoutSamples.Ok(result);
        var rendered = LayoutSamples.Rows(result).Where(row => row.Occupancy == Occupancy.Text)
            .SelectMany(row => row.VisualLine!.RenderRuns).ToArray();
        Assert.That(rendered.Any(run => run.Text == "3" && run.ResolvedStyle.Semantic == RunSemantic.Superscript), Is.True);
        Assert.That(rendered.Any(run => run.Text == "-4" && run.ResolvedStyle.Semantic == RunSemantic.Superscript), Is.True);
        Assert.That(rendered.Any(run => run.Text.Contains('²') && run.ResolvedStyle.Semantic == RunSemantic.Normal), Is.True);
        Assert.That(rendered.All(run => !run.Text.Contains('³') && !run.Text.Contains('⁻') && !run.Text.Contains('⁴')), Is.True);

        var uncalibrated = LayoutSamples.Engine().Layout(document, LayoutSamples.Standard(calibrateScripts: false), LayoutSamples.Columns(200), new FakeMeasure(), CancellationToken.None);
        Assert.That(uncalibrated.Diagnostics.Any(item => item.Code == DiagnosticCodes.ETemplateInvalid), Is.True);
    }

    [Test]
    public void SubscriptDropsBelowTheBaseline()
    {
        var standard = LayoutSamples.Standard();
        var document = LayoutSamples.DocumentOf(LayoutSamples.Runs(0, BlockType.Paragraph, null,
            LayoutSamples.Run("x"), LayoutSamples.Run("1", RunSemantic.Subscript)));
        var result = LayoutSamples.Engine().Layout(document, standard, LayoutSamples.Columns(40), new FakeMeasure { Cjk = 4, Unit = 2 }, CancellationToken.None);
        LayoutSamples.Ok(result);
        var line = LayoutSamples.Rows(result).Single(row => row.Occupancy == Occupancy.Text).VisualLine!;
        Assert.That(line.RenderRuns, Has.Count.EqualTo(2));
        var script = line.RenderRuns[1];
        Assert.That(script.ResolvedStyle.Semantic, Is.EqualTo(RunSemantic.Subscript));
        Assert.That(script.ResolvedStyle.TextHeight, Is.EqualTo(4.5 * ScriptCalibration.SubscriptScale).Within(1e-9));
        Assert.That(script.BaselineOffset, Is.EqualTo(-4.5 * ScriptCalibration.SubscriptDrop).Within(1e-9));
    }

    [Test]
    public void PlainTextWithoutScriptsIgnoresMissingCalibration()
    {
        var standard = LayoutSamples.Standard(calibrateScripts: false);
        var result = LayoutSamples.Engine().Layout(Sample("甲乙"), standard, LayoutSamples.Columns(20), new FakeMeasure(), CancellationToken.None);
        LayoutSamples.Ok(result);
        Assert.That(LayoutSamples.Lines(result), Is.EqualTo(new[] { "甲乙" }));
    }

    [Test]
    public void PendingRulesAndUncalibratedTemplatesStayBlocked()
    {
        var notePath = Path.Combine(Root(), "standards", "drafts", "jsr-note-1.0.0.json");
        var templatePath = Path.Combine(Root(), "standards", "drafts", "jsr-A1-three-column-1.0.0.json");
        var noteBefore = File.ReadAllText(notePath);
        var templateBefore = File.ReadAllText(templatePath);
        Assert.That(noteBefore, Does.Contain("\"lineBreakRuleVersion\": \"pending\""));
        Assert.That(templateBefore, Does.Contain("\"status\": \"uncalibrated\""));

        var document = Sample("甲");
        var pending = LayoutSamples.Standard(lineBreakVersion: "pending");
        var blocked = LayoutSamples.Engine().Layout(document, pending, LayoutSamples.Columns(10), new FakeMeasure(), CancellationToken.None);
        Assert.That(blocked.Pages, Is.Empty);
        Assert.That(blocked.Diagnostics.Any(item => item.Message.Contains("换行规则")), Is.True);

        var unknown = LayoutSamples.Standard(lineBreakVersion: "somewhere-else");
        var unrecognized = LayoutSamples.Engine().Layout(document, unknown, LayoutSamples.Columns(10), new FakeMeasure(), CancellationToken.None);
        Assert.That(unrecognized.Diagnostics.Any(item => item.Message.Contains("不认识")), Is.True);

        var template = LayoutSamples.Columns(10);
        template.Status = TemplateStatus.Uncalibrated;
        var uncalibrated = LayoutSamples.Engine().Layout(document, LayoutSamples.Standard(), template, new FakeMeasure(), CancellationToken.None);
        Assert.That(uncalibrated.Pages, Is.Empty);
        Assert.That(uncalibrated.Diagnostics.Any(item => item.Message.Contains("尚未标定")), Is.True);

        Assert.That(File.ReadAllText(notePath), Is.EqualTo(noteBefore));
        Assert.That(File.ReadAllText(templatePath), Is.EqualTo(templateBefore));
    }

    [Test]
    public void ParsedDocumentKeepsBreaksNumberingAndDoesNotRepeatTheLabel()
    {
        var builder = new DocxFixtureBuilder();
        builder.Styles.Add(DocxFixtureBuilder.ParagraphStyle("Normal", "Normal", null, true));
        builder.Styles.Add(DocxFixtureBuilder.ParagraphStyle("Heading1", "heading 1", "Normal"));
        builder.Body.Add(DocxFixtureBuilder.Paragraph("Heading1", DocxFixtureBuilder.TextRun("材料要求")));
        builder.Body.Add(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "Normal" }),
            new Run(
                new Text("甲") { Space = SpaceProcessingModeValues.Preserve },
                new Break(),
                new Text("乙") { Space = SpaceProcessingModeValues.Preserve })));
        builder.Body.Add(DocxFixtureBuilder.Paragraph("Normal"));
        builder.Body.Add(DocxFixtureBuilder.Paragraph("Normal", DocxFixtureBuilder.TextRun("丙")));
        var map = new StyleMap
        {
            Entries =
            {
                new StyleMapEntry { Match = StyleMapMatch.StyleId, Key = "Heading1", Target = BlockType.Heading1 },
                new StyleMapEntry { Match = StyleMapMatch.StyleId, Key = "Normal", Target = BlockType.Paragraph }
            }
        };
        var parsed = new DocxDocumentParser(new DocxParseOptions
        {
            DocumentId = "sample-doc-001",
            DisciplineCode = "structure",
            StyleMap = map
        }).Parse(new DocxBytesSource("sample.docx", builder.Build()), new ParseProfile
        {
            Standard = new StandardRef { Id = "test-note", Version = "1.0.0" }
        }, CancellationToken.None);
        Assert.That(parsed.Success, Is.True, string.Join(" | ", parsed.Diagnostics.Select(item => item.Message)));

        var result = Lay(parsed.Document, 40);
        LayoutSamples.Ok(result);
        Assert.That(LayoutSamples.Lines(result), Is.EqualTo(new[] { "材料要求", "甲", "乙", "丙" }));
        Assert.That(LayoutSamples.Rows(result).Count(row => row.Occupancy == Occupancy.Spacer), Is.EqualTo(1));
        Assert.That(result.DocumentHash, Is.EqualTo(parsed.Document.Source.ContentHash));
        Assert.That(result.EngineVersion, Is.EqualTo(LayoutEngineInfo.EngineVersion));
        Assert.That(result.SchemaVersion, Is.EqualTo("1.0"));
        var bodyLines = LayoutSamples.Rows(result).Where(row => row.VisualLine != null && row.VisualLine.SourceSlices[0].ParagraphIndex == 1).Select(row => row.VisualLine.Text).ToArray();
        Assert.That(bodyLines, Is.EqualTo(new[] { "甲", "乙" }));
    }

    private static LayoutResult Lay(string text, double width)
    {
        return Lay(Sample(text), width, new FakeMeasure());
    }

    private static LayoutResult Lay(ModelDocument document, double width)
    {
        return Lay(document, width, new FakeMeasure());
    }

    private static LayoutResult Lay(ModelDocument document, double width, FakeMeasure measure)
    {
        return LayoutSamples.Engine().Layout(document, LayoutSamples.Standard(), LayoutSamples.Columns(width), measure, CancellationToken.None);
    }

    private static ModelDocument Sample(string text)
    {
        return LayoutSamples.DocumentOf(LayoutSamples.Text(0, text));
    }

    private static void AssertToken(string text, double width, FakeMeasure measure, params string[] lines)
    {
        var result = Lay(Sample(text), width, measure);
        LayoutSamples.Ok(result);
        Assert.That(LayoutSamples.Lines(result), Is.EqualTo(lines), text);
        Assert.That(result.Diagnostics.Any(item => item.Code == DiagnosticCodes.WTokenSplit), Is.False, text);
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        return dir == null ? throw new System.InvalidOperationException("找不到仓库根目录。") : dir.FullName;
    }
}
