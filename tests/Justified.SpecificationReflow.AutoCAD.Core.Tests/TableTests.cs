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

    [Test]
    public void CandidateStandardsAndThreePaperSizesProduceBoundedEditableObjects()
    {
        var bytes = Sample();
        var catalog = new Standards.DirectoryPackageCatalog(Path.Combine(Root(), "standards", "candidates", "t28"), false);
        var s = catalog.Load(new StandardRef { Id = "jsr-note", Version = "1.1.0" }, CancellationToken.None);
        Assert.That(s.Success, Is.True);
        foreach (var id in new[] { "jsr-A1-three-column", "jsr-A2-three-column", "jsr-A3-two-column" })
        {
            var template = catalog.Load(new TemplateRef { Id = id, Version = "1.2.0" }, CancellationToken.None);
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
            var template = catalog.Load(new TemplateRef { Id = id, Version = "1.2.0" }, CancellationToken.None).Template!;
            var result = new NoteGenerationService(FullParser(), LayoutSamples.Engine(), new FakeMeasure { Unit = 1.7, Cjk = 3.375 })
                .Generate(new DocxFileSource(path!), standard, template, new RenderTransform { UnitScale = 1 }, CancellationToken.None, GenerationLimits.Default);
            Assert.That(result.Success, Is.True, string.Join(";", result.Diagnostics.Select(d => d.Message)));
            Assert.That(result.Document!.Blocks.Count(b => b.Type == BlockType.Table), Is.GreaterThan(0));
            TestContext.WriteLine(id + " tables=" + result.Document.Blocks.Count(b => b.Type == BlockType.Table) + " pages=" + result.Layout!.Pages.Count + " texts=" + result.Texts.Count + " lines=" + result.Lines.Count + "; simulated measurement only");
        }
    }
}
