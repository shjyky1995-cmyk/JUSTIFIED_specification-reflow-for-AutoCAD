#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using Justified.SpecificationReflow.AutoCAD.Contracts.Geometry;
using Justified.SpecificationReflow.AutoCAD.Contracts.Layout;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;
using Justified.SpecificationReflow.AutoCAD.Contracts.Templates;
using Justified.SpecificationReflow.AutoCAD.LayoutEngine;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Justified.SpecificationReflow.AutoCAD.Core.Tests;

public class ColumnFlowTests
{
    [Test]
    public void SixLinesFillOnePageAndTheSeventhOpensTheNextPage()
    {
        var template = LayoutSamples.Columns(new[] { 10d, 10d, 10d }, 2, 7.2, 90);
        var six = Flow(Lines("甲", 6), template);
        LayoutSamples.Ok(six);
        Assert.That(six.Pages, Has.Count.EqualTo(1));
        Assert.That(six.Statistics.PageCount, Is.EqualTo(1));
        Assert.That(six.Statistics.RowCount, Is.EqualTo(6));
        Assert.That(six.Statistics.ObjectCount, Is.EqualTo(6));
        Assert.That(six.Pages[0].Columns.Select(column => column.Rows.Count), Is.EqualTo(new[] { 2, 2, 2 }));
        Assert.That(six.Pages[0].PageOffset.X, Is.EqualTo(0));
        Assert.That(six.Pages[0].Columns[0].Rows[0].Baseline, Is.EqualTo(90).Within(1e-6));
        Assert.That(six.Pages[0].Columns[0].Rows[1].Baseline, Is.EqualTo(82.8).Within(1e-6));
        Assert.That(six.Pages[0].Columns.SelectMany(column => column.Rows).All(row => row.RowIndex == 0 || row.RowIndex == 1), Is.True);

        var seven = Flow(Lines("乙", 7), template);
        LayoutSamples.Ok(seven);
        Assert.That(seven.Pages, Has.Count.EqualTo(2));
        Assert.That(seven.Statistics.RowCount, Is.EqualTo(7));
        Assert.That(seven.Pages[1].Columns, Has.Count.EqualTo(1));
        Assert.That(seven.Pages[1].Columns[0].ColumnIndex, Is.EqualTo(0));
        Assert.That(seven.Pages[1].Columns[0].Rows.Single().RowIndex, Is.EqualTo(0));
        Assert.That(seven.Pages[1].Columns[0].Rows.Single().VisualLine.Text, Is.EqualTo("乙"));
        Assert.That(seven.Pages[1].PageOffset.X, Is.EqualTo(template.PageStep.Value.X).Within(1e-6));
        Assert.That(seven.Pages[1].PageOffset.Y, Is.EqualTo(0));
        Assert.That(LayoutSamples.Lines(seven).Length, Is.EqualTo(7));
    }

    [Test]
    public void AFullPageDoesNotCreateAnEmptyTail()
    {
        var template = LayoutSamples.Columns(new[] { 10d, 10d, 10d }, 2, 7.2, 90);
        var result = Flow(Lines("满", 6), template);
        LayoutSamples.Ok(result);
        Assert.That(result.Pages, Has.Count.EqualTo(1));
        Assert.That(result.Pages[0].Columns.SelectMany(column => column.Rows).Count(), Is.EqualTo(6));
        Assert.That(result.Pages[0].Columns.All(column => column.Rows.Count == 2), Is.True);
    }

    [Test]
    public void SpacersAndExplicitBreaksConsumeSlotsWithoutAnEmptyTextObject()
    {
        var template = LayoutSamples.Columns(new[] { 10d, 10d, 10d }, 2, 7.2, 90);
        var blocks = new List<Block>();
        blocks.AddRange(Lines("文", 2));
        blocks.Add(LayoutSamples.Spacer(10));
        blocks.AddRange(Lines("字", 3).Select((block, index) =>
        {
            block.SourceRef.ParagraphIndex = 20 + index;
            block.Id = "p" + block.SourceRef.ParagraphIndex;
            return block;
        }));
        var full = Flow(blocks, template);
        LayoutSamples.Ok(full);
        Assert.That(full.Pages, Has.Count.EqualTo(1));
        Assert.That(full.Statistics.RowCount, Is.EqualTo(6));
        Assert.That(full.Statistics.ObjectCount, Is.EqualTo(5));
        Assert.That(LayoutSamples.Rows(full).Count(row => row.Occupancy == Occupancy.Spacer && row.VisualLine == null), Is.EqualTo(1));

        blocks.Add(LayoutSamples.Spacer(30));
        var overflow = Flow(blocks, template);
        LayoutSamples.Ok(overflow);
        Assert.That(overflow.Pages, Has.Count.EqualTo(2));
        Assert.That(overflow.Pages[1].Columns[0].Rows.Single().Occupancy, Is.EqualTo(Occupancy.Spacer));

        var broken = LayoutSamples.DocumentOf(LayoutSamples.Runs(0, BlockType.Paragraph, null, LayoutSamples.Run("\n")));
        for (var index = 0; index < 5; index++)
            broken.Blocks.Add(LayoutSamples.Text(index + 1, "行"));
        var withBreak = Flow(broken.Blocks, template);
        LayoutSamples.Ok(withBreak);
        Assert.That(withBreak.Pages, Has.Count.EqualTo(1));
        Assert.That(withBreak.Statistics.RowCount, Is.EqualTo(6));
        Assert.That(withBreak.Statistics.ObjectCount, Is.EqualTo(5));
    }

    [Test]
    public void AParagraphUsesTheNewColumnWidthAndDoesNotRepeatItsNumber()
    {
        var template = LayoutSamples.Columns(new[] { 6d, 20d }, 3, 7.2, 90);
        template.Columns[0].RowCount = 1;
        var document = LayoutSamples.DocumentOf(LayoutSamples.Text(3, "材料要求说明文字", "1.1"));
        var result = LayoutSamples.Engine().Layout(document, LayoutSamples.Standard(), template, new FakeMeasure(), CancellationToken.None);
        LayoutSamples.Ok(result);
        Assert.That(result.Pages, Has.Count.EqualTo(1));
        Assert.That(result.Pages[0].Columns[0].Rows.Single().VisualLine.Text, Is.EqualTo("1.1  材"));
        var continuation = result.Pages[0].Columns[1].Rows.Single().VisualLine;
        Assert.That(continuation.Text, Is.EqualTo("料要求说明文字"));
        Assert.That(continuation.Text, Does.Not.Contain("1.1"));
        Assert.That(continuation.MeasuredWidth, Is.GreaterThan(6));
        Assert.That(continuation.SourceSlices.Single().ParagraphIndex, Is.EqualTo(3));
        Assert.That(LayoutSamples.Rebuild(document, result), Is.EqualTo("材料要求说明文字"));
    }

    [Test]
    public void AParagraphCanCrossPagesWithoutDuplicatingLines()
    {
        var template = LayoutSamples.Columns(new[] { 1d, 1d, 1d }, 2, 7.2, 90);
        var document = LayoutSamples.DocumentOf(LayoutSamples.Text(4, "甲乙丙丁戊己庚"));
        var result = LayoutSamples.Engine().Layout(document, LayoutSamples.Standard(), template, new FakeMeasure(), CancellationToken.None);
        LayoutSamples.Ok(result);
        Assert.That(result.Pages, Has.Count.EqualTo(2));
        Assert.That(LayoutSamples.Lines(result), Is.EqualTo(new[] { "甲", "乙", "丙", "丁", "戊", "己", "庚" }));
        Assert.That(result.Pages[1].Columns[0].Rows.Single().VisualLine.Text, Is.EqualTo("庚"));
        Assert.That(LayoutSamples.Rows(result).All(row => row.VisualLine.SourceSlices.Single().ParagraphIndex == 4), Is.True);
    }

    [Test]
    public void HeadingsUseConfiguredSlotsAndTheProjectStandardAddsNone()
    {
        var template = LayoutSamples.Columns(new[] { 20d }, 6, 7.2, 90);
        var plain = LayoutSamples.Standard();
        var document = LayoutSamples.DocumentOf(
            LayoutSamples.Text(0, "标题", type: BlockType.Heading1),
            LayoutSamples.Text(1, "正文"));
        var zero = LayoutSamples.Engine().Layout(document, plain, template, new FakeMeasure(), CancellationToken.None);
        LayoutSamples.Ok(zero);
        Assert.That(LayoutSamples.Rows(zero).Select(row => row.Occupancy), Is.EqualTo(new[] { Occupancy.Text, Occupancy.Text }));
        Assert.That(zero.Pages[0].Columns[0].Rows[0].VisualLine.RenderRuns.Single().ResolvedStyle.StyleId, Is.EqualTo("heading1"));
        Assert.That(zero.Pages[0].Columns[0].Rows[1].VisualLine.RenderRuns.Single().ResolvedStyle.StyleId, Is.EqualTo("body"));

        plain.Styles["heading1"].BeforeSlots = 1;
        plain.Styles["heading1"].AfterSlots = 1;
        var spaced = LayoutSamples.Engine().Layout(document, plain, template, new FakeMeasure(), CancellationToken.None);
        LayoutSamples.Ok(spaced);
        Assert.That(LayoutSamples.Rows(spaced).Select(row => row.Occupancy), Is.EqualTo(new[]
        {
            Occupancy.Spacer, Occupancy.Text, Occupancy.Spacer, Occupancy.Text
        }));
    }

    [Test]
    public void TheSafetyPageLimitRejectsAnotherPageButNotAFullOne()
    {
        var template = LayoutSamples.Columns(new[] { 10d, 10d, 10d }, 2, 7.2, 90);
        var full = LayoutSamples.Engine(1).Layout(LayoutSamples.DocumentOf(Lines("满", 6).ToArray()), LayoutSamples.Standard(), template, new FakeMeasure(), CancellationToken.None);
        LayoutSamples.Ok(full);
        Assert.That(full.Pages, Has.Count.EqualTo(1));

        var overflow = LayoutSamples.Engine(1).Layout(LayoutSamples.DocumentOf(Lines("溢", 7).ToArray()), LayoutSamples.Standard(), template, new FakeMeasure(), CancellationToken.None);
        Assert.That(overflow.Pages, Is.Empty);
        Assert.That(overflow.Diagnostics.Single(item => item.Severity == Severity.Error).Code, Is.EqualTo(DiagnosticCodes.EResourceLimit));
    }

    [Test]
    public void RowPitchMustClearTheMeasuredGlyph()
    {
        var template = LayoutSamples.Columns(new[] { 10d }, 4, 7.2, 90);
        var measure = new FakeMeasure { InkHeight = 8 };
        var result = LayoutSamples.Engine().Layout(LayoutSamples.DocumentOf(LayoutSamples.Text(0, "甲乙")), LayoutSamples.Standard(), template, measure, CancellationToken.None);
        Assert.That(result.Pages, Is.Empty);
        Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(DiagnosticCodes.ETemplateInvalid));
        Assert.That(result.Diagnostics.Single().Message, Does.Contain("行距"));
    }

    [Test]
    public void EmptyInputProducesNoPageAndIllegalTemplatesAreRejected()
    {
        var empty = LayoutSamples.Engine().Layout(LayoutSamples.DocumentOf(), LayoutSamples.Standard(), LayoutSamples.Columns(10), new FakeMeasure(), CancellationToken.None);
        LayoutSamples.Ok(empty);
        Assert.That(empty.Pages, Is.Empty);
        Assert.That(empty.Statistics.PageCount, Is.EqualTo(0));

        var template = LayoutSamples.Columns(10);
        template.Columns[0].RowCount = 0;
        var zeroRows = LayoutSamples.Engine().Layout(LayoutSamples.DocumentOf(LayoutSamples.Text(0, "甲")), LayoutSamples.Standard(), template, new FakeMeasure(), CancellationToken.None);
        Assert.That(zeroRows.Pages, Is.Empty);
        Assert.That(zeroRows.Diagnostics.Any(item => item.Code == DiagnosticCodes.ETemplateInvalid), Is.True);
    }

    [Test]
    public void A1A2AndA3DraftGeometryFlowAcrossColumnsAndPages()
    {
        var notePath = DraftPath("jsr-note-1.0.0.json");
        var noteBefore = File.ReadAllText(notePath);
        Assert.That(noteBefore, Does.Contain("\"lineBreakRuleVersion\": \"pending\""));
        Assert.That(noteBefore, Does.Contain("\"symbolMapVersion\": \"pending\""));
        var standard = NoteForTest(JObject.Parse(noteBefore));

        var papers = new[]
        {
            new PaperCase("jsr-A1-three-column-1.0.0.json", new[] { 230d, 230d, 230d }, 71, 861),
            new PaperCase("jsr-A2-three-column-1.0.0.json", new[] { 160d, 160d, 160d }, 47, 614),
            new PaperCase("jsr-A3-two-column-1.0.0.json", new[] { 170d, 170d }, 30, 440)
        };
        foreach (var paper in papers)
        {
            var path = DraftPath(paper.File);
            var before = File.ReadAllText(path);
            Assert.That(before, Does.Contain("\"status\": \"uncalibrated\""), paper.File);
            var template = TemplateForTest(JObject.Parse(before), standard);
            Assert.That(template.Columns.Select(column => column.Right - column.Left), Is.EqualTo(paper.Widths), paper.File);
            Assert.That(template.Columns.Select(column => column.RowCount), Is.EqualTo(Enumerable.Repeat(paper.Rows, paper.Widths.Length)), paper.File);
            Assert.That(template.PageStep.Value.X, Is.EqualTo(paper.PageStepX), paper.File);
            Assert.That(template.Columns[0].FirstBaselineY, Is.EqualTo(-6.2).Within(1e-9), paper.File);

            var blocked = LayoutSamples.Engine().Layout(LayoutSamples.DocumentOf(LayoutSamples.Text(0, "甲")), standard, Uncalibrated(template), new FakeMeasure { Unit = 10, Cjk = 10 }, CancellationToken.None);
            Assert.That(blocked.Pages, Is.Empty, paper.File);
            Assert.That(blocked.Diagnostics.Any(item => item.Message.Contains("尚未标定")), Is.True, paper.File);

            var slots = paper.Rows * paper.Widths.Length;
            var measure = new FakeMeasure { Unit = 10, Cjk = 10 };
            var full = LayoutSamples.Engine().Layout(LayoutSamples.DocumentOf(Lines("行", slots).ToArray()), standard, template, measure, CancellationToken.None);
            LayoutSamples.Ok(full);
            Assert.That(full.Pages, Has.Count.EqualTo(1), paper.File);
            Assert.That(full.Statistics.RowCount, Is.EqualTo(slots), paper.File);
            AssertInside(full, template, standard);

            var extra = LayoutSamples.Engine().Layout(LayoutSamples.DocumentOf(Lines("续", slots + 1).ToArray()), standard, template, measure, CancellationToken.None);
            LayoutSamples.Ok(extra);
            Assert.That(extra.Pages, Has.Count.EqualTo(2), paper.File);
            Assert.That(extra.Pages[1].Columns.SelectMany(column => column.Rows).Count(), Is.EqualTo(1), paper.File);
            Assert.That(extra.Pages[1].PageOffset.X, Is.EqualTo(paper.PageStepX).Within(1e-6), paper.File);
            AssertInside(extra, template, standard);

            var withGap = Lines("空", slots - 1);
            withGap.Add(LayoutSamples.Spacer(1000));
            var gapped = LayoutSamples.Engine().Layout(LayoutSamples.DocumentOf(withGap.ToArray()), standard, template, measure, CancellationToken.None);
            LayoutSamples.Ok(gapped);
            Assert.That(gapped.Pages, Has.Count.EqualTo(1), paper.File);
            Assert.That(gapped.Statistics.ObjectCount, Is.EqualTo(slots - 1), paper.File);

            var perLine = (int)Math.Floor((template.Columns[0].Right - template.Columns[0].Left) / 10d);
            var lead = Lines("前", paper.Rows - 1);
            lead.Add(LayoutSamples.Text(500, new string('跨', perLine + 1)));
            var crossed = LayoutSamples.Engine().Layout(LayoutSamples.DocumentOf(lead.ToArray()), standard, template, measure, CancellationToken.None);
            LayoutSamples.Ok(crossed);
            var crossingRows = LayoutSamples.Rows(crossed).Where(row => row.VisualLine != null && row.VisualLine.SourceSlices[0].ParagraphIndex == 500).ToArray();
            Assert.That(crossingRows, Has.Length.EqualTo(2), paper.File);
            Assert.That(crossingRows[0].VisualLine.Text.Length, Is.EqualTo(perLine), paper.File);
            Assert.That(crossingRows[1].VisualLine.Text, Is.EqualTo("跨"), paper.File);
            Assert.That(ColumnOf(crossed, crossingRows[0]), Is.EqualTo(0), paper.File);
            Assert.That(ColumnOf(crossed, crossingRows[1]), Is.EqualTo(1), paper.File);
            Assert.That(File.ReadAllText(path), Is.EqualTo(before), paper.File);
        }

        Assert.That(File.ReadAllText(notePath), Is.EqualTo(noteBefore));
    }

    private static List<Block> Lines(string text, int count)
    {
        var blocks = new List<Block>();
        for (var index = 0; index < count; index++)
            blocks.Add(LayoutSamples.Text(index, text));
        return blocks;
    }

    private static LayoutResult Flow(IEnumerable<Block> blocks, LayoutTemplate template)
    {
        return LayoutSamples.Engine().Layout(LayoutSamples.DocumentOf(blocks.ToArray()), LayoutSamples.Standard(), template, new FakeMeasure(), CancellationToken.None);
    }

    private static void AssertInside(LayoutResult result, LayoutTemplate template, InstitutionStandard standard)
    {
        foreach (var page in result.Pages)
        {
            Assert.That(page.PageOffset.X, Is.EqualTo(page.PageIndex * template.PageStep.Value.X).Within(1e-6));
            Assert.That(page.PageOffset.Y, Is.EqualTo(page.PageIndex * template.PageStep.Value.Y).Within(1e-6));
            foreach (var column in page.Columns)
            {
                var geometry = template.Columns[column.ColumnIndex];
                var width = geometry.Right - geometry.Left;
                foreach (var row in column.Rows)
                {
                    Assert.That(row.Baseline, Is.EqualTo(geometry.FirstBaselineY - row.RowIndex * standard.RowPitch).Within(1e-6));
                    if (row.VisualLine == null) continue;
                    Assert.That(row.VisualLine.MeasuredWidth, Is.LessThanOrEqualTo(width + standard.MeasurementTolerance + 1e-6));
                    Assert.That(row.VisualLine.RenderRuns.Single().RelativeOrigin.X, Is.EqualTo(0));
                }
            }
        }
    }

    private static int ColumnOf(LayoutResult result, RowSlot row)
    {
        foreach (var page in result.Pages)
        {
            foreach (var column in page.Columns)
            {
                if (column.Rows.Contains(row)) return column.ColumnIndex;
            }
        }

        return -1;
    }

    private static InstitutionStandard NoteForTest(JObject json)
    {
        var body = json["styles"]["body"];
        var standard = LayoutSamples.Standard(
            height: (double)json["textHeight"],
            widthFactor: (double)json["widthFactor"],
            pitch: (double)json["rowPitch"],
            indent: (double)body["indent"],
            hanging: (double)body["hangingIndent"]);
        standard.StandardId = (string)json["standardId"];
        standard.Version = (string)json["version"];
        standard.ObliqueAngle = (double)json["obliqueAngle"];
        Assert.That(standard.TextHeight, Is.EqualTo(4.5));
        Assert.That(standard.WidthFactor, Is.EqualTo(0.75));
        Assert.That(standard.RowPitch, Is.EqualTo(7.2));
        Assert.That(standard.Styles["heading1"].Indent, Is.EqualTo(0));
        Assert.That(standard.Styles["body"].HangingIndent, Is.EqualTo(0));
        return standard;
    }

    private static LayoutTemplate TemplateForTest(JObject json, InstitutionStandard standard)
    {
        var paper = (string)json["paperCode"];
        var template = new LayoutTemplate
        {
            TemplateId = (string)json["templateId"],
            Version = (string)json["version"],
            DisciplineCode = (string)json["disciplineCode"],
            PaperCode = paper == "A1" ? PaperCode.A1 : paper == "A2" ? PaperCode.A2 : PaperCode.A3,
            Status = TemplateStatus.Calibrated,
            Orientation = Orientation.Landscape,
            Unit = LengthUnit.Mm,
            StandardRef = new StandardRef { Id = standard.StandardId, Version = standard.Version },
            Anchor = new TemplateAnchor
            {
                Kind = AnchorKind.NoteTopRight,
                X = (double)json["anchor"]["x"],
                Y = (double)json["anchor"]["y"]
            },
            PageBounds = new PageBounds
            {
                Left = (double)json["pageBounds"]["left"],
                Right = (double)json["pageBounds"]["right"],
                Top = (double)json["pageBounds"]["top"],
                Bottom = (double)json["pageBounds"]["bottom"]
            },
            PageStep = new Point2 { X = (double)json["pageStep"]["x"], Y = (double)json["pageStep"]["y"] },
            FramePolicy = FramePolicy.None,
            Columns = new List<ColumnGeometry>()
        };
        foreach (var column in json["columns"])
        {
            template.Columns.Add(new ColumnGeometry
            {
                ColumnId = (string)column["columnId"],
                Left = (double)column["left"],
                Right = (double)column["right"],
                Top = (double)column["top"],
                Bottom = (double)column["bottom"],
                FirstBaselineY = (double)column["firstBaselineY"],
                RowCount = (int)column["rowCount"]
            });
        }

        return template;
    }

    private static LayoutTemplate Uncalibrated(LayoutTemplate template)
    {
        return new LayoutTemplate
        {
            TemplateId = template.TemplateId,
            Version = template.Version,
            DisciplineCode = template.DisciplineCode,
            PaperCode = template.PaperCode,
            Status = TemplateStatus.Uncalibrated,
            Orientation = template.Orientation,
            Unit = template.Unit,
            StandardRef = template.StandardRef,
            Anchor = template.Anchor,
            PageBounds = template.PageBounds,
            PageStep = template.PageStep,
            FramePolicy = template.FramePolicy,
            Columns = template.Columns
        };
    }

    private static string DraftPath(string name)
    {
        return Path.Combine(Root(), "standards", "drafts", name);
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        if (dir == null) throw new InvalidOperationException("找不到仓库根目录。");
        return dir.FullName;
    }

    private sealed class PaperCase
    {
        public PaperCase(string file, double[] widths, int rows, double pageStepX)
        {
            File = file;
            Widths = widths;
            Rows = rows;
            PageStepX = pageStepX;
        }

        public string File { get; }

        public double[] Widths { get; }

        public int Rows { get; }

        public double PageStepX { get; }
    }
}
