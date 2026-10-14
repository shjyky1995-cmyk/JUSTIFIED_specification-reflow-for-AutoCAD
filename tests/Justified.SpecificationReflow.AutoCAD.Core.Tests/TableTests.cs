using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using DocumentFormat.OpenXml.Wordprocessing;
using Justified.SpecificationReflow.AutoCAD.Application;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using Justified.SpecificationReflow.AutoCAD.Contracts.Geometry;
using Justified.SpecificationReflow.AutoCAD.Contracts.Layout;
using Justified.SpecificationReflow.AutoCAD.Contracts.Ports;
using Justified.SpecificationReflow.AutoCAD.Contracts.Rendering;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;
using Justified.SpecificationReflow.AutoCAD.DocumentCore.Json;
using Justified.SpecificationReflow.AutoCAD.DocxAdapter;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using ModelDocument = Justified.SpecificationReflow.AutoCAD.Contracts.Documents.Document;

namespace Justified.SpecificationReflow.AutoCAD.Core.Tests;

public class TableTests
{
    private static InstitutionStandard Standard()
    {
        var s = LayoutSamples.Standard();
        s.TableStyle = new Contracts.Standards.TableStyle { HorizontalPaddingEm = 0.25, VerticalPaddingEm = 0.15, Layer = "JSR_NOTE_TABLE", Linetype = "Continuous", Lineweight = 18 };
        return s;
    }
    private static Block Grid(int rows = 2, int cols = 2)
    {
        var t = new TableData { RowCount = rows, ColumnWidths = Enumerable.Repeat(1d, cols).ToList() };
        for (int r = 0; r < rows; r++) for (int c = 0; c < cols; c++)
            t.Cells.Add(new TableCellData { Row = r, Column = c, Top = true, Bottom = true, Left = true, Right = true,
                Paragraphs = new List<Block> { LayoutSamples.Text(r * cols + c, "格" + r + c) } });
        return new Block { Id = "t1", Type = BlockType.Table, Table = t };
    }
    private static ModelDocument Doc(params Block[] blocks)
    {
        var d = LayoutSamples.DocumentOf(blocks); d.SchemaVersion = "2.0"; return d;
    }
    private static string Errors(LayoutResult l) => string.Join(";", l.Diagnostics.Select(d => d.Message));
    private static DocxDocumentParser Parser() => new DocxDocumentParser(new DocxParseOptions {
        DocumentId = "table-test", DisciplineCode = "structure", StyleMap = new StyleMap {
            Entries = { new StyleMapEntry { Match = StyleMapMatch.StyleId, Key = "Normal", Target = BlockType.Paragraph } } } });
    private static byte[] Bytes(string xml, bool terminator = true)
    {
        var b = new DocxFixtureBuilder { EmitTerminator = terminator };
        b.Styles.Add(DocxFixtureBuilder.ParagraphStyle("Normal", "Normal", null, true));
        b.Body.Add(new Table { InnerXml = xml }); return b.Build();
    }
    private const string Ns = " xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private static string Cell(string text, string props = "") => "<w:tc><w:tcPr>" + props + "</w:tcPr><w:p><w:r><w:t>" + text + "</w:t></w:r></w:p></w:tc>";
    private static string Xml(string rows) => "<w:tblPr" + Ns + "><w:tblBorders><w:top w:val=\"single\"/><w:bottom w:val=\"single\"/><w:left w:val=\"single\"/><w:right w:val=\"single\"/><w:insideH w:val=\"single\"/><w:insideV w:val=\"single\"/></w:tblBorders></w:tblPr><w:tblGrid" + Ns + "><w:gridCol w:w=\"1000\"/><w:gridCol w:w=\"2000\"/></w:tblGrid>" + rows;
    private static string Row(string cells, string props = "") => "<w:tr" + Ns + "><w:trPr>" + props + "</w:trPr>" + cells + "</w:tr>";

    [Test]
    public void DocxMergesAndEmptyLastTableSurviveRoundTrip()
    {
        var xml = Xml(Row(Cell("合并", "<w:vMerge w:val=\"restart\"/>") + Cell("上"))
            + Row(Cell("", "<w:vMerge/>") + Cell("下")) + Row(Cell("", "<w:gridSpan w:val=\"2\"/>")));
        var parsed = Parser().Parse(new DocxBytesSource("test.docx", Bytes(xml, false)), new ParseProfile(), CancellationToken.None);
        Assert.That(parsed.Success, Is.True, string.Join(";", parsed.Diagnostics.Select(d => d.Message)));
        var doc = parsed.Document!;
        Assert.That(doc.SchemaVersion, Is.EqualTo("2.0"));
        var cells = doc.Blocks.Single().Table!.Cells;
        Assert.That(cells.Count, Is.EqualTo(4));
        Assert.That(cells[0].RowSpan, Is.EqualTo(2));
        Assert.That(cells.Last().ColumnSpan, Is.EqualTo(2));
        Assert.That(JsonProtocol.Default.LoadDocument(JsonProtocol.Default.SaveDocument(doc)).Success, Is.True);
    }

    [TestCase("<w:vMerge/>")]
    [TestCase("<w:textDirection w:val=\"tbRl\"/>")]
    [TestCase("<w:tcBorders><w:tl2br w:val=\"single\"/></w:tcBorders>")]
    public void UnsupportedOrInvalidTableIsLocatedAndRejected(string props)
    {
        var parsed = Parser().Parse(new DocxBytesSource("test.docx", Bytes(Xml(Row(Cell("内容", props) + Cell("乙"))))), new ParseProfile(), CancellationToken.None);
        Assert.That(parsed.Success, Is.False);
        Assert.That(parsed.Document, Is.Null);
        Assert.That(parsed.Diagnostics.Any(d => d.SourceRef != null && d.Message.Contains("表格")), Is.True);
    }

    [Test]
    public void GridUsesSixUniqueLinesAndFourTextObjects()
    {
        var s = Standard(); var template = LayoutSamples.Columns(40);
        var result = LayoutSamples.Engine().Layout(Doc(Grid()), s, template, new FakeMeasure(), CancellationToken.None);
        Assert.That(result.Diagnostics, Is.Empty, Errors(result));
        var c = result.Pages.Single().Columns.Single();
        Assert.That(c.Lines.Count, Is.EqualTo(6));
        Assert.That(c.TableTexts.Count, Is.EqualTo(4));
        Assert.That(result.Statistics.ObjectCount, Is.EqualTo(10));
        Assert.That(JsonProtocol.Default.LoadLayoutResult(JsonProtocol.Default.SaveLayoutResult(result)).Success, Is.True);
        var placed = NotePlacement.Create(result, template, s, new RenderTransform { AnchorWcs = new Point2 { X = 100, Y = 50 }, UnitScale = 2 }, CancellationToken.None);
        Assert.That(placed.Success, Is.True);
        Assert.That(placed.Lines[0].Start.X, Is.EqualTo(100 + 2 * c.Lines[0].Start.X));
        Assert.That(placed.Lines[0].Start.Y, Is.EqualTo(50 + 2 * c.Lines[0].Start.Y));
        Assert.That(placed.Texts.All(t => t.Height == s.TextHeight * 2), Is.True);
    }

    [Test]
    public void HorizontalMergeHasNoInternalVerticalLine()
    {
        var b = Grid(1); b.Table!.Cells.RemoveAt(1); b.Table.Cells[0].ColumnSpan = 2;
        var l = LayoutSamples.Engine().Layout(Doc(b), Standard(), LayoutSamples.Columns(40), new FakeMeasure(), CancellationToken.None);
        Assert.That(l.Diagnostics, Is.Empty, Errors(l));
        Assert.That(l.Pages[0].Columns[0].Lines.Count, Is.EqualTo(4));
    }

    [Test]
    public void VerticalMergeMovesAsAGroupAndNeverGetsAnInternalHorizontalEdge()
    {
        var b = Grid(); b.Table!.Cells.RemoveAt(2); b.Table.Cells[0].RowSpan = 2;
        var l = LayoutSamples.Engine().Layout(Doc(LayoutSamples.Spacer(8, 2), b), Standard(), LayoutSamples.Columns(new[] { 40d, 50d }, 3, 7.2, 90), new FakeMeasure(), CancellationToken.None);
        Assert.That(l.Diagnostics, Is.Empty, Errors(l));
        Assert.That(l.Pages[0].Columns[0].TableTexts, Is.Empty);
        var c = l.Pages[0].Columns[1];
        Assert.That(c.TableTexts.Count, Is.EqualTo(3));
        Assert.That(c.Lines.Count(e => Math.Abs(e.Start.Y - e.End.Y) < 1e-8 && e.Start.X == 25), Is.EqualTo(1));
    }

    [Test]
    public void RepeatHeaderAndDifferentColumnWidthsReflowWithoutEmptyTailPage()
    {
        var b = Grid(5); b.Table!.HeaderRows = 1;
        var l = LayoutSamples.Engine().Layout(Doc(b), Standard(), LayoutSamples.Columns(new[] { 40d, 30d }, 3, 7.2, 90), new FakeMeasure(), CancellationToken.None);
        Assert.That(l.Diagnostics, Is.Empty, Errors(l));
        Assert.That(l.Pages.Count, Is.EqualTo(1));
        var columns = l.Pages[0].Columns;
        Assert.That(columns.Count, Is.EqualTo(2));
        Assert.That(columns[1].TableTexts[0].VisualLine!.Text, Is.EqualTo("格00"));
        Assert.That(columns[1].Lines.Max(e => e.End.X), Is.EqualTo(30).Within(1e-8));
    }

    [Test]
    public void OversizedRowGroupStopsWithoutPartialResult()
    {
        var b = Grid(1); b.Table!.Cells[0].Paragraphs = Enumerable.Range(0, 10).Select(i => LayoutSamples.Text(i, "多段")).ToList();
        var l = LayoutSamples.Engine().Layout(Doc(b), Standard(), LayoutSamples.Columns(new[] { 40d }, 3, 7.2, 90), new FakeMeasure(), CancellationToken.None);
        Assert.That(l.Pages, Is.Empty);
        Assert.That(Errors(l), Does.Contain("超过完整栏高"));
    }

    [Test]
    public void NoBordersCreatesOnlyTextsAndLongTextAddsHeight()
    {
        var b = Grid(1); foreach (var c in b.Table!.Cells) c.Top = c.Bottom = c.Left = c.Right = false;
        b.Table.Cells[0].Paragraphs[0].Runs[0].Text = new string('字', 30);
        var l = LayoutSamples.Engine().Layout(Doc(b), Standard(), LayoutSamples.Columns(24), new FakeMeasure(), CancellationToken.None);
        Assert.That(l.Diagnostics, Is.Empty, Errors(l));
        var c0 = l.Pages[0].Columns[0];
        Assert.That(c0.Lines, Is.Empty); Assert.That(c0.Rows.Count, Is.GreaterThan(1));
        Assert.That(string.Concat(c0.TableTexts.Select(t => t.VisualLine!.Text)), Does.Contain(new string('字', 30)));
    }

    [Test]
    public void MissingStandardAndOverlappingCellsFailAndCancellationReturnsNothing()
    {
        var engine = LayoutSamples.Engine(); var doc = Doc(Grid()); var template = LayoutSamples.Columns(40);
        Assert.That(engine.Layout(doc, LayoutSamples.Standard(), template, new FakeMeasure(), CancellationToken.None).Pages, Is.Empty);
        doc.Blocks[0].Table!.Cells[1].Column = 0;
        Assert.That(Errors(engine.Layout(doc, Standard(), template, new FakeMeasure(), CancellationToken.None)), Does.Contain("结构无效"));
        Assert.That(engine.Layout(Doc(Grid()), Standard(), template, new FakeMeasure(), new CancellationToken(true)).Pages, Is.Empty);
    }

    [Test]
    public void EndToEndCountsTableCharactersAndLinesInLimits()
    {
        var source = new DocxBytesSource("table.docx", Bytes(Xml(Row(Cell("甲乙") + Cell("丙丁")))));
        var svc = new NoteGenerationService(Parser(), LayoutSamples.Engine(), new FakeMeasure());
        var transform = new RenderTransform { UnitScale = 1 };
        var good = svc.Generate(source, Standard(), LayoutSamples.Columns(40), transform, CancellationToken.None);
        Assert.That(good.Success, Is.True, string.Join(";", good.Diagnostics.Select(d => d.Message)));
        Assert.That(good.Lines.Count, Is.EqualTo(5));
        var chars = svc.Generate(source, Standard(), LayoutSamples.Columns(40), transform, CancellationToken.None, new GenerationLimits { MaxCharacters = 3 });
        Assert.That(chars.Success, Is.False); Assert.That(chars.Texts, Is.Empty); Assert.That(chars.Lines, Is.Empty);
        var objects = svc.Generate(source, Standard(), LayoutSamples.Columns(40), transform, CancellationToken.None, new GenerationLimits { MaxTexts = 6 });
        Assert.That(objects.Success, Is.False); Assert.That(objects.Texts, Is.Empty); Assert.That(objects.Lines, Is.Empty);
    }

    [Test]
    public void TableProtocolCannotMasqueradeAsVersionOne()
    {
        var json = JObject.Parse(JsonProtocol.Default.SaveDocument(Doc(Grid())));
        json["schemaVersion"] = "1.0";
        Assert.That(JsonProtocol.Default.LoadDocument(json.ToString()).Success, Is.False);
    }

    [Test]
    public void ExplicitAutoWidthsRequireConfiguredPolicyAndAlwaysWarn()
    {
        var xml = Xml(Row(Cell("甲", "<w:tcW w:type=\"auto\"/>") + Cell("乙", "<w:tcW w:type=\"auto\"/>")));
        var table = new Table { InnerXml = xml }; table.GetFirstChild<TableGrid>()!.Remove();
        var parsed = Parser().Parse(new DocxBytesSource("auto.docx", Bytes(table.InnerXml)), new ParseProfile(), CancellationToken.None);
        Assert.That(parsed.Success, Is.True);
        Assert.That(parsed.Document!.Blocks[0].Table!.AutoColumnWidths, Is.True);
        var s = Standard();
        var denied = LayoutSamples.Engine().Layout(parsed.Document, s, LayoutSamples.Columns(40), new FakeMeasure(), CancellationToken.None);
        Assert.That(denied.Pages, Is.Empty);
        s.TableStyle!.AutoWidthPolicy = "equal-columns-with-warning";
        var allowed = LayoutSamples.Engine().Layout(parsed.Document, s, LayoutSamples.Columns(40), new FakeMeasure(), CancellationToken.None);
        Assert.That(allowed.Pages, Is.Not.Empty);
        Assert.That(allowed.Diagnostics.Single().Code, Is.EqualTo("W_TABLE_AUTO_WIDTH"));
    }

    [Test]
    public void MergeContinuationCannotAttachToOrdinaryCell()
    {
        var xml = Xml(Row(Cell("甲") + Cell("乙")) + Row(Cell("", "<w:vMerge/>") + Cell("丙")));
        var parsed = Parser().Parse(new DocxBytesSource("invalid.docx", Bytes(xml)), new ParseProfile(), CancellationToken.None);
        Assert.That(parsed.Success, Is.False);
        Assert.That(parsed.Diagnostics.Single().Message, Does.Contain("起始单元格"));
    }

    [Test]
    public void InheritedTableBorderAndExplicitHiddenCellBorderAreResolved()
    {
        var b = new DocxFixtureBuilder();
        b.Styles.Add(DocxFixtureBuilder.ParagraphStyle("Normal", "Normal", null, true));
        var style = new Style { Type = StyleValues.Table, StyleId = "GridStyle" };
        style.InnerXml = "<w:tblPr" + Ns + "><w:tblBorders><w:top w:val=\"single\"/><w:bottom w:val=\"single\"/></w:tblBorders></w:tblPr>";
        b.Styles.Add(style);
        var t = new Table { InnerXml = Xml(Row(Cell("甲", "<w:tcBorders><w:top w:val=\"nil\"/></w:tcBorders>") + Cell("乙"))) };
        t.GetFirstChild<TableProperties>()!.InnerXml = "<w:tblStyle" + Ns + " w:val=\"GridStyle\"/>";
        b.Body.Add(t);
        var p = Parser().Parse(new DocxBytesSource("style.docx", b.Build()), new ParseProfile(), CancellationToken.None);
        Assert.That(p.Success, Is.True);
        Assert.That(p.Document!.Blocks[0].Table!.Cells[0].Top, Is.False);
        Assert.That(p.Document.Blocks[0].Table!.Cells[1].Top, Is.True);
        Assert.That(p.Document.Blocks[0].Table!.Cells.All(c => c.Bottom), Is.True);
    }

    [Test]
    public void TableScriptRunsKeepSizingAndDoNotTouchBorders()
    {
        var b = Grid(1);
        b.Table!.Cells[0].Paragraphs[0].Runs = new List<TextRun> { LayoutSamples.Run("面积m"), LayoutSamples.Run("2", RunSemantic.Superscript), LayoutSamples.Run("下标"), LayoutSamples.Run("1", RunSemantic.Subscript) };
        var s = Standard();
        var l = LayoutSamples.Engine().Layout(Doc(b), s, LayoutSamples.Columns(80), new FakeMeasure(), CancellationToken.None);
        Assert.That(l.Diagnostics, Is.Empty, Errors(l));
        var c = l.Pages[0].Columns[0];
        var row = c.TableTexts[0];
        Assert.That(row.VisualLine!.RenderRuns.Any(r => r.ResolvedStyle.Semantic == RunSemantic.Superscript && r.BaselineOffset > 0), Is.True);
        Assert.That(row.VisualLine.RenderRuns.Any(r => r.ResolvedStyle.Semantic == RunSemantic.Subscript && r.BaselineOffset < 0), Is.True);
        Assert.That(row.Baseline + row.VisualLine.InkBounds.MaxY, Is.LessThan(c.Lines.Max(e => e.Start.Y)));
        Assert.That(row.Baseline + row.VisualLine.InkBounds.MinY, Is.GreaterThan(c.Lines.Min(e => e.Start.Y)));
    }

    [Test]
    public void EntireTableMarkedAsHeaderDoesNotDuplicateItsRows()
    {
        var b = Grid(3); b.Table!.HeaderRows = 3;
        var l = LayoutSamples.Engine().Layout(Doc(b), Standard(), LayoutSamples.Columns(40), new FakeMeasure(), CancellationToken.None);
        Assert.That(l.Diagnostics, Is.Empty, Errors(l));
        Assert.That(l.Pages.SelectMany(p => p.Columns).Sum(c => c.TableTexts.Count), Is.EqualTo(6));
    }

    private static string Root()
    {
        var d = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (d != null && !Directory.Exists(Path.Combine(d.FullName, "standards", "candidates", "t28"))) d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("缺少候选标准目录");
    }

    private static DocxDocumentParser FullParser()
    {
        var map = new StyleMap();
        map.Entries.Add(new StyleMapEntry { Match = StyleMapMatch.StyleId, Key = "Heading1", Target = BlockType.Heading1 });
        map.Entries.Add(new StyleMapEntry { Match = StyleMapMatch.StyleId, Key = "Heading2", Target = BlockType.Heading2 });
        map.Entries.Add(new StyleMapEntry { Match = StyleMapMatch.StyleId, Key = "Normal", Target = BlockType.Paragraph });
        foreach (var name in new[] { "Normal", "正文" }) map.Entries.Add(new StyleMapEntry { Match = StyleMapMatch.Name, Key = name, Target = BlockType.Paragraph });
        return new DocxDocumentParser(new DocxParseOptions { DocumentId = "t28-smoke", DisciplineCode = "structure", StyleMap = map });
    }

    private static byte[] Sample()
    {
        var b = new DocxFixtureBuilder();
        b.Styles.Add(DocxFixtureBuilder.ParagraphStyle("Normal", "Normal", null, true));
        b.Body.Add(DocxFixtureBuilder.Paragraph("Normal", DocxFixtureBuilder.TextRun("表格功能试用：仅为脱敏测试，不作为工程说明。")));
        b.Body.Add(new Table { InnerXml = Xml(Row(Cell("普通表格") + Cell("文字字体字号与正文一致")) + Row(Cell("项目") + Cell("较长文字应当自动换行，增加行高，不缩小字号。"))) });
        b.Body.Add(new Table { InnerXml = Xml(Row(Cell("横向合并单元格", "<w:gridSpan w:val=\"2\"/>")) + Row(Cell("甲") + Cell("乙"))) });
        b.Body.Add(new Table { InnerXml = Xml(Row(Cell("纵向合并", "<w:vMerge w:val=\"restart\"/>") + Cell("上行")) + Row(Cell("", "<w:vMerge/>") + Cell("下行"))) });
        var rows = Row(Cell("序号") + Cell("长表重复表头"), "<w:tblHeader/>");
        for (int i = 1; i <= 80; i++) rows += Row(Cell(i.ToString()) + Cell("脱敏条目，核对续栏和续页顺序。"));
        b.Body.Add(new Table { InnerXml = Xml(rows) });
        b.Body.Add(DocxFixtureBuilder.Paragraph("Normal", DocxFixtureBuilder.TextRun("表格后的正文仍按原顺序排版。")));
        return b.Build();
    }

    // 脱敏材料/设备表：六个不等宽列、两层表头、横纵组合合并、多段与上下标。
    private static byte[] ComplexSample()
    {
        var b = new DocxFixtureBuilder();
        b.Styles.Add(DocxFixtureBuilder.ParagraphStyle("Normal", "Normal", null, true));
        b.Body.Add(DocxFixtureBuilder.Paragraph("Normal", DocxFixtureBuilder.TextRun("复杂工程表格试用：全部数值仅为测试，不作为工程结论。")));
        var header = "<w:tblHeader/>";
        var rows = Row(Cell("材料统计", "<w:gridSpan w:val=\"3\"/>") + Cell("设计取值及说明", "<w:gridSpan w:val=\"3\"/>"), header)
            + Row(Cell("分组") + Cell("编号") + Cell("构件名称") + Cell("材料") + Cell("数量") + Cell("备注"), header);
        for (int group = 1; group <= 40; group++)
        {
            for (int row = 0; row < 3; row++)
            {
                var merge = row == 0 ? "<w:vMerge w:val=\"restart\"/>" : "<w:vMerge/>";
                var note = Cell("长文字检查：尺寸及施工要求应完整换行，保持原有字号。");
                if (row == 1)
                    note = "<w:tc><w:tcPr/><w:p><w:r><w:t>面积 m</w:t></w:r><w:r><w:rPr><w:vertAlign w:val=\"superscript\"/></w:rPr><w:t>2</w:t></w:r></w:p><w:p><w:r><w:t>第二段核对留白。</w:t></w:r></w:p></w:tc>";
                var key = "K" + group.ToString("D2") + row;
                rows += Row(Cell(row == 0 ? "G" + group.ToString("D2") : "", merge)
                    + Cell(key) + Cell(row == 2 ? "连续合并构件名称与材料" : "墙板及连接构件", row == 2 ? "<w:gridSpan w:val=\"2\"/>" : "")
                    + (row == 2 ? "" : Cell("C30")) + Cell("12") + note);
            }
        }
        var xml = Xml(rows);
        var table = new Table { InnerXml = xml };
        table.GetFirstChild<TableGrid>()!.Remove();
        table.InsertAt(new TableGrid(new[] { 600, 900, 1800, 900, 800, 2000 }
            .Select(w => new GridColumn { Width = w.ToString() })), 1);
        b.Body.Add(table);
        b.Body.Add(DocxFixtureBuilder.Paragraph("Normal", DocxFixtureBuilder.TextRun("复杂表格后的正文：核对顺序与下边距。")));
        return b.Build();
    }

    [TestCase("A1", "jsr-A1-three-column", 41.63, 68)]
    [TestCase("A2", "jsr-A2-three-column", 50, 43)]
    [TestCase("A3", "jsr-A3-two-column", 52, 25)]
    public void ComplexEngineeringTableAndTextShareSafeMargins(string paper, string id, double margin, int rows)
    {
        var catalog = new Standards.DirectoryPackageCatalog(Path.Combine(Root(), "standards", "candidates", "t28"), false);
        var standard = catalog.Load(new StandardRef { Id = "jsr-note", Version = "1.1.0" }, CancellationToken.None).Standard!;
        var template = catalog.Load(new TemplateRef { Id = id, Version = "1.2.1" }, CancellationToken.None).Template!;
        var bytes = ComplexSample();
        var measure = new InspectionMeasure();
        var result = new NoteGenerationService(FullParser(), LayoutSamples.Engine(), measure)
            .Generate(new DocxBytesSource("complex.docx", bytes), standard, template, new RenderTransform { UnitScale = 1 }, CancellationToken.None);
        Assert.That(result.Success, Is.True, string.Join(";", result.Diagnostics.Select(d => d.Message)));
        Assert.That(result.Layout!.Pages.Count, Is.GreaterThan(1));
        Assert.That(result.Texts.Last().Text, Does.Contain("复杂表格后的正文"));
        foreach (var page in result.Layout.Pages) foreach (var column in page.Columns)
        {
            var g = template.Columns[column.ColumnIndex];
            Assert.That(g.RowCount, Is.EqualTo(rows));
            Assert.That(g.Bottom - template.PageBounds.Bottom, Is.EqualTo(margin).Within(1e-8));
            foreach (var edge in column.Lines)
                foreach (var point in new[] { edge.Start, edge.End })
                {
                    Assert.That(point.X, Is.InRange(-1e-8, g.Right - g.Left + 1e-8));
                    Assert.That(point.Y, Is.InRange(g.Bottom - 1e-8, g.Top + 1e-8));
                }
            var texts = column.TableTexts.Concat(column.Rows.Where(r => r.VisualLine != null)).ToArray();
            foreach (var text in texts)
            {
                var ink = text.VisualLine!.InkBounds;
                Assert.That(text.Baseline + ink.MinY, Is.GreaterThanOrEqualTo(g.Bottom - 1e-8));
                Assert.That(text.Baseline + ink.MaxY, Is.LessThanOrEqualTo(g.Top + 1e-8));
                Assert.That(ink.MinX, Is.GreaterThanOrEqualTo(-1e-8));
                Assert.That(ink.MaxX, Is.LessThanOrEqualTo(g.Right - g.Left + 1e-8));
            }
            for (int a = 0; a < texts.Length; a++) for (int c = a + 1; c < texts.Length; c++)
            {
                var x = texts[a].VisualLine!.InkBounds; var y = texts[c].VisualLine!.InkBounds;
                bool overlaps = x.MinX < y.MaxX - 1e-8 && x.MaxX > y.MinX + 1e-8
                    && texts[a].Baseline + x.MinY < texts[c].Baseline + y.MaxY - 1e-8
                    && texts[a].Baseline + x.MaxY > texts[c].Baseline + y.MinY + 1e-8;
                Assert.That(overlaps, Is.False, "单元格或后续正文文字不得互相覆盖。");
            }
        }
        for (int group = 1; group <= 40; group++) for (int row = 0; row < 3; row++)
            Assert.That(result.Texts.Count(t => t.Text == "K" + group.ToString("D2") + row), Is.EqualTo(1), "正文条目不得丢失或重复。");
        var output = Environment.GetEnvironmentVariable("T28_VISUAL_OUTPUT");
        if (!string.IsNullOrWhiteSpace(output))
        {
            Directory.CreateDirectory(output);
            File.WriteAllBytes(Path.Combine(output, "复杂工程表格试用.docx"), bytes);
            File.WriteAllText(Path.Combine(output, paper + "-layout.json"), JsonProtocol.Default.SaveLayoutResult(result.Layout));
            File.WriteAllText(Path.Combine(output, paper + "-template.json"), Newtonsoft.Json.JsonConvert.SerializeObject(template));
            File.WriteAllText(Path.Combine(output, paper + "-measure-requests.json"), Newtonsoft.Json.JsonConvert.SerializeObject(measure.Requests.Values));
        }
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("T28_HOST_MEASUREMENTS"))
            && Environment.GetEnvironmentVariable("T28_COLLECT_MEASUREMENTS") != "1")
            Assert.That(measure.Missing, Is.Zero, "真实字宽复核不得混用模拟值；先补齐测量请求。");
    }

    private sealed class InspectionMeasure : ITextMeasureService
    {
        public sealed class Entry
        {
            public string Key { get; set; } = string.Empty;
            public string Text { get; set; } = string.Empty;
            public double Height { get; set; }
            public double WidthFactor { get; set; }
            public Bounds2? Ink { get; set; }
        }
        public readonly Dictionary<string, Entry> Requests = new Dictionary<string, Entry>();
        private readonly Dictionary<string, Entry> _host = new Dictionary<string, Entry>();
        private readonly FakeMeasure _fallback = new FakeMeasure { Unit = 1.7, Cjk = 3.375, InkDescent = 0.8 };
        public int Missing { get; private set; }
        public InspectionMeasure()
        {
            var path = Environment.GetEnvironmentVariable("T28_HOST_MEASUREMENTS");
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            foreach (var e in Newtonsoft.Json.JsonConvert.DeserializeObject<List<Entry>>(File.ReadAllText(path))!) _host[e.Key] = e;
        }
        public TextMeasurement Measure(IReadOnlyList<TextRun> runs, ResolvedStyle style, CancellationToken cancellation)
        {
            var text = runs[0].Text;
            var key = style.TextHeight.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" + text;
            Requests[key] = new Entry { Key = key, Text = text, Height = style.TextHeight, WidthFactor = style.WidthFactor };
            if (_host.TryGetValue(key, out var measured) && measured.Ink != null)
                return new TextMeasurement(new[] { new RunMeasurement(measured.Ink.Value.MaxX, measured.Ink.Value) }, Array.Empty<Contracts.Diagnostics.Diagnostic>());
            Missing++;
            return _fallback.Measure(runs, style, cancellation);
        }
    }

    [Test]
    public void TableBottomUsesActualEdgeAndMovesWholeRowBeforeReservedArea()
    {
        var template = LayoutSamples.Columns(new[] { 40d, 40d }, 3, 7.2, 90);
        // 表格比文字基线多占底部空间：第3行基线可用，但其底线已在安全区外。
        foreach (var g in template.Columns) { g.Top = 100; g.Bottom = 75; }
        var result = LayoutSamples.Engine().Layout(Doc(Grid(3)), Standard(), template, new FakeMeasure { InkDescent = 0.8 }, CancellationToken.None);
        Assert.That(result.Diagnostics, Is.Empty, Errors(result));
        Assert.That(result.Pages.Single().Columns.Count, Is.EqualTo(2));
        Assert.That(result.Pages[0].Columns[0].TableTexts.Count, Is.EqualTo(4));
        Assert.That(result.Pages[0].Columns[1].TableTexts.Count, Is.EqualTo(2));
        Assert.That(result.Pages.SelectMany(p => p.Columns).SelectMany(c => c.Lines).Min(e => e.Start.Y), Is.GreaterThanOrEqualTo(75));
    }

    [TestCase("jsr-A1-three-column")]
    [TestCase("jsr-A2-three-column")]
    [TestCase("jsr-A3-two-column")]
    public void FullTextColumnsUseTheSameSafeRegionAsTables(string id)
    {
        var catalog = new Standards.DirectoryPackageCatalog(Path.Combine(Root(), "standards", "candidates", "t28"), false);
        var standard = catalog.Load(new StandardRef { Id = "jsr-note", Version = "1.1.0" }, CancellationToken.None).Standard!;
        var template = catalog.Load(new TemplateRef { Id = id, Version = "1.2.1" }, CancellationToken.None).Template!;
        var doc = Doc(Enumerable.Range(0, 250).Select(i => LayoutSamples.Text(i, "纯文字留边检查" + i)).ToArray());
        var result = LayoutSamples.Engine().Layout(doc, standard, template, new FakeMeasure { InkDescent = 0.8 }, CancellationToken.None);
        Assert.That(result.Diagnostics, Is.Empty, Errors(result));
        Assert.That(LayoutSamples.Lines(result).Length, Is.EqualTo(250));
        foreach (var page in result.Pages) foreach (var column in page.Columns)
        {
            var g = template.Columns[column.ColumnIndex];
            foreach (var row in column.Rows.Where(r => r.VisualLine != null))
            {
                Assert.That(row.Baseline + row.VisualLine!.InkBounds.MinY, Is.GreaterThanOrEqualTo(g.Bottom - 1e-8));
                Assert.That(row.Baseline + row.VisualLine.InkBounds.MaxY, Is.LessThanOrEqualTo(g.Top + 1e-8));
            }
        }
    }

    [Test]
    public void CandidateStandardsAndThreePaperSizesProduceBoundedEditableObjects()
    {
        var bytes = Sample();
        var catalog = new Standards.DirectoryPackageCatalog(Path.Combine(Root(), "standards", "candidates", "t28"), false);
        var s = catalog.Load(new StandardRef { Id = "jsr-note", Version = "1.1.0" }, CancellationToken.None);
        Assert.That(s.Success, Is.True);
        foreach (var id in new[] { "jsr-A1-three-column", "jsr-A2-three-column", "jsr-A3-two-column" })
        {
            var template = catalog.Load(new TemplateRef { Id = id, Version = "1.2.1" }, CancellationToken.None);
            Assert.That(template.Success, Is.True);
            var svc = new NoteGenerationService(FullParser(), LayoutSamples.Engine(), new FakeMeasure { Unit = 1.7, Cjk = 3.375 });
            var result = svc.Generate(new DocxBytesSource("sample.docx", bytes), s.Standard!, template.Template!, new RenderTransform { UnitScale = 1 }, CancellationToken.None);
            Assert.That(result.Success, Is.True, string.Join(";", result.Diagnostics.Select(d => d.Message)));
            Assert.That(result.Lines, Is.Not.Empty);
            Assert.That(result.Texts, Is.Not.Empty);
            foreach (var page in result.Layout!.Pages) foreach (var col in page.Columns)
            {
                var g = template.Template!.Columns[col.ColumnIndex];
                foreach (var line in col.Lines)
                {
                    Assert.That(line.Start.X, Is.InRange(-1e-8, g.Right - g.Left + 1e-8));
                    Assert.That(line.End.X, Is.InRange(-1e-8, g.Right - g.Left + 1e-8));
                    Assert.That(line.Start.Y, Is.InRange(g.Bottom - 1e-8, g.Top + 1e-8));
                    Assert.That(line.End.Y, Is.InRange(g.Bottom - 1e-8, g.Top + 1e-8));
                }
            }
            Assert.That(result.Texts.Last().Text, Does.Contain("表格后的正文"));
            Assert.That(JsonProtocol.Default.ValidateLayoutResult(JsonProtocol.Default.SaveLayoutResult(result.Layout)), Is.Empty);
        }
        var output = Environment.GetEnvironmentVariable("T28_SAMPLE_OUTPUT");
        if (!string.IsNullOrWhiteSpace(output)) File.WriteAllBytes(output, bytes);
    }

    [Test, Explicit("仅按环境变量读取本机私有样本；不将业务数据写入仓库")]
    public void LocalPrivateSampleCompatibility()
    {
        var path = Environment.GetEnvironmentVariable("T28_PRIVATE_SAMPLE");
        Assert.That(path, Is.Not.Null.And.Not.Empty);
        var catalog = new Standards.DirectoryPackageCatalog(Path.Combine(Root(), "standards", "candidates", "t28"), false);
        var standard = catalog.Load(new StandardRef { Id = "jsr-note", Version = "1.1.0" }, CancellationToken.None).Standard!;
        foreach (var id in new[] { "jsr-A1-three-column", "jsr-A2-three-column", "jsr-A3-two-column" })
        {
            var template = catalog.Load(new TemplateRef { Id = id, Version = "1.2.1" }, CancellationToken.None).Template!;
            var result = new NoteGenerationService(FullParser(), LayoutSamples.Engine(), new FakeMeasure { Unit = 1.7, Cjk = 3.375 })
                .Generate(new DocxFileSource(path!), standard, template, new RenderTransform { UnitScale = 1 }, CancellationToken.None, GenerationLimits.Default);
            Assert.That(result.Success, Is.True, string.Join(";", result.Diagnostics.Select(d => d.Message)));
            Assert.That(result.Document!.Blocks.Count(b => b.Type == BlockType.Table), Is.GreaterThan(0));
            TestContext.WriteLine(id + " tables=" + result.Document.Blocks.Count(b => b.Type == BlockType.Table) + " pages=" + result.Layout!.Pages.Count + " texts=" + result.Texts.Count + " lines=" + result.Lines.Count + "; simulated measurement only");
        }
    }
}
