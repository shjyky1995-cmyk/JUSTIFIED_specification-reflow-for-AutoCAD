using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using ModelDocument = Justified.SpecificationReflow.AutoCAD.Contracts.Documents.Document;
using Justified.SpecificationReflow.AutoCAD.Contracts.Ports;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;
using Justified.SpecificationReflow.AutoCAD.DocumentCore.Json;
using Justified.SpecificationReflow.AutoCAD.DocxAdapter;
using NUnit.Framework;

namespace Justified.SpecificationReflow.AutoCAD.Core.Tests;

public class DocxParserTests
{
    [Test]
    public void WordAndWpsStyleIdentitiesProduceTheSameBlocks()
    {
        var word = Note("Heading1", "Heading2", "Normal", "heading 1", "heading 2", "Normal", false);
        var wps = Note("1", "2", "a", "标题 1", "标题 2", "正文", false);
        var wordResult = Parse(word, SampleMap());
        var wpsResult = Parse(wps, SampleMap());
        Assert.That(wordResult.Success, Is.True, Dump(wordResult));
        Assert.That(wpsResult.Success, Is.True, Dump(wpsResult));
        Assert.That(Project(wpsResult.Document!), Is.EqualTo(Project(wordResult.Document!)));
        var document = wordResult.Document!;
        Assert.That(document.Blocks.Select(block => block.Type), Is.EqualTo(new[] { BlockType.Heading1, BlockType.Heading2, BlockType.Paragraph, BlockType.Paragraph, BlockType.Paragraph }));
        Assert.That(document.Blocks[2].Numbering, Is.Null);
        Assert.That(document.Blocks[2].Runs[0].Text, Is.EqualTo("（1）钢筋采用HRB400。"));
        Assert.That(document.Blocks[3].Runs[1].Semantic, Is.EqualTo(RunSemantic.Superscript));
        Assert.That(document.Blocks[3].Runs[1].Text, Is.EqualTo("2"));
        Assert.That(document.Blocks[3].Runs[2].Text, Does.Contain("²"));
        Assert.That(string.Concat(document.Blocks.SelectMany(block => block.Runs).Select(run => run.Text)), Does.Contain("²").And.Contain("C30"));
    }

    [Test]
    public void VisualFormattingDoesNotChangeBlocks()
    {
        var plain = Note("Heading1", "Heading2", "Normal", "heading 1", "heading 2", "Normal", false);
        var formatted = Note("Heading1", "Heading2", "Normal", "heading 1", "heading 2", "Normal", true);
        var plainResult = Parse(plain, SampleMap());
        var formattedResult = Parse(formatted, SampleMap());
        Assert.That(plainResult.Success, Is.True, Dump(plainResult));
        Assert.That(formattedResult.Success, Is.True, Dump(formattedResult));
        Assert.That(Project(formattedResult.Document!), Is.EqualTo(Project(plainResult.Document!)));
        Assert.That(plainResult.Diagnostics.Any(item => item.Code == DocxDiagnosticCodes.IgnoredFormat), Is.False);
        Assert.That(formattedResult.Diagnostics.Any(item => item.Code == DocxDiagnosticCodes.IgnoredFormat && item.Severity == Severity.Info), Is.True);
        Assert.That(formattedResult.Document!.Blocks.Any(block => block.Type == BlockType.Spacer), Is.False);
    }

    [Test]
    public void HeadingInheritanceDoesNotDowngradeHeading3()
    {
        var builder = WordBuilder();
        builder.Styles.Add(DocxFixtureBuilder.ParagraphStyle("Chapter", "章", "Heading1"));
        builder.Styles.Add(DocxFixtureBuilder.ParagraphStyle("Heading3", "heading 3", "Normal"));
        builder.Body.Add(DocxFixtureBuilder.Paragraph("Chapter", DocxFixtureBuilder.TextRun("第一章")));
        var inherited = Parse(builder.Build(), SampleMap());
        Assert.That(inherited.Success, Is.True, Dump(inherited));
        Assert.That(inherited.Document!.Blocks.Single().Type, Is.EqualTo(BlockType.Heading1));

        var rejectedBuilder = WordBuilder();
        rejectedBuilder.Styles.Add(DocxFixtureBuilder.ParagraphStyle("Heading3", "heading 3", "Normal"));
        rejectedBuilder.Body.Add(DocxFixtureBuilder.Paragraph("Heading3", DocxFixtureBuilder.TextRun("三级")));
        var rejected = Parse(rejectedBuilder.Build(), SampleMap());
        Assert.That(rejected.Document, Is.Null);
        var error = rejected.Diagnostics.Single(item => item.Code == DiagnosticCodes.EUnsupportedContent);
        Assert.That(error.SourceRef!.ParagraphIndex, Is.EqualTo(0));
        Assert.That(error.Details!["kind"], Is.EqualTo("unmappedStyle"));
        Assert.That(error.Details["styleId"], Is.EqualTo("Heading3"));
    }

    [Test]
    public void StyleIdMatchPrecedesDisplayName()
    {
        var builder = WordBuilder();
        builder.Styles.Add(DocxFixtureBuilder.ParagraphStyle("Custom", "标题 1"));
        builder.Body.Add(DocxFixtureBuilder.Paragraph("Custom", DocxFixtureBuilder.TextRun("仍是正文")));
        var map = SampleMap();
        map.Entries.Add(new StyleMapEntry { Match = StyleMapMatch.StyleId, Key = "Custom", Target = BlockType.Paragraph });
        var result = Parse(builder.Build(), map);
        Assert.That(result.Success, Is.True, Dump(result));
        Assert.That(result.Document!.Blocks.Single().Type, Is.EqualTo(BlockType.Paragraph));
    }

    [Test]
    public void DirectFormattingDoesNotPromoteBodyToHeading()
    {
        var builder = WordBuilder();
        builder.Body.Add(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "Normal" }),
            new Run(
                new RunProperties(new Bold(), new FontSize { Val = "72" }, new Color { Val = "0000FF" }),
                new Text("看起来像标题"))));
        var result = Parse(builder.Build(), SampleMap());
        Assert.That(result.Success, Is.True, Dump(result));
        Assert.That(result.Document!.Blocks.Single().Type, Is.EqualTo(BlockType.Paragraph));
        Assert.That(result.Document.Blocks.Single().Runs[0].Text, Is.EqualTo("看起来像标题"));
    }

    [Test]
    public void EmptyParagraphsStaySeparateAndFinalTerminatorIsDropped()
    {
        var builder = WordBuilder();
        builder.Body.Add(DocxFixtureBuilder.Paragraph("Normal", DocxFixtureBuilder.TextRun("甲")));
        builder.Body.Add(DocxFixtureBuilder.Paragraph("Normal"));
        builder.Body.Add(DocxFixtureBuilder.Paragraph("Normal"));
        builder.Body.Add(DocxFixtureBuilder.Paragraph("Normal", DocxFixtureBuilder.TextRun("乙")));
        builder.Body.Add(DocxFixtureBuilder.Paragraph("Normal"));
        var result = Parse(builder.Build(), SampleMap());
        Assert.That(result.Success, Is.True, Dump(result));
        Assert.That(result.Document!.Blocks.Select(block => block.Type), Is.EqualTo(new[]
        {
            BlockType.Paragraph, BlockType.Spacer, BlockType.Spacer, BlockType.Paragraph, BlockType.Spacer
        }));
        Assert.That(result.Document.Blocks[1].SlotCount, Is.EqualTo(1));
        Assert.That(result.Document.Blocks[1].Id, Is.EqualTo("p1"));
        Assert.That(result.Document.Blocks.Last().Id, Is.EqualTo("p4"));
    }

    [Test]
    public void DocumentThatIsOnlyTheTerminatorHasNoBlocks()
    {
        var result = Parse(WordBuilder().Build(), SampleMap());
        Assert.That(result.Success, Is.True, Dump(result));
        Assert.That(result.Document!.Blocks, Is.Empty);
    }

    [Test]
    public void ExplicitBreaksAreKeptAndPageBreaksAreNot()
    {
        var builder = WordBuilder();
        builder.Body.Add(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "Normal" }),
            new Run(
                new Text("甲") { Space = SpaceProcessingModeValues.Preserve },
                new Break(),
                new Text("乙") { Space = SpaceProcessingModeValues.Preserve },
                new Break(),
                new Break(),
                new Text("丙") { Space = SpaceProcessingModeValues.Preserve })));
        builder.Body.Add(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "Normal" }),
            new Run(
                new Text("丁") { Space = SpaceProcessingModeValues.Preserve },
                new Break { Type = BreakValues.Page },
                new LastRenderedPageBreak(),
                new Text("戊") { Space = SpaceProcessingModeValues.Preserve })));
        builder.Body.Add(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "Normal" }),
            new Run(new Break { Type = BreakValues.Column })));
        builder.Body.Add(DocxFixtureBuilder.Paragraph("Normal", DocxFixtureBuilder.TextRun("己")));
        var result = Parse(builder.Build(), SampleMap());
        Assert.That(result.Success, Is.True, Dump(result));
        Assert.That(result.Document!.Blocks[0].Runs.Select(run => run.Text), Is.EqualTo(new[] { "甲", "\n", "乙", "\n", "\n", "丙" }));
        Assert.That(result.Document.Blocks[1].Runs.Single().Text, Is.EqualTo("丁戊"));
        Assert.That(result.Document.Blocks.Select(block => block.Type), Is.EqualTo(new[] { BlockType.Paragraph, BlockType.Paragraph, BlockType.Paragraph }));
        Assert.That(result.Diagnostics.Count(item => item.Code == DocxDiagnosticCodes.IgnoredPagination), Is.EqualTo(2));
        Assert.That(result.Diagnostics.Any(item => item.Details != null && item.Details.TryGetValue("break", out var kind) && kind.Contains("page")), Is.True);
    }

    [Test]
    public void WhitespaceHyperlinkAndCharacterStyleArePreserved()
    {
        var builder = WordBuilder();
        builder.Styles.Add(new Style(
            new StyleName { Val = "sup" },
            new StyleRunProperties(new VerticalTextAlignment { Val = VerticalPositionValues.Superscript }))
        { Type = StyleValues.Character, StyleId = "Sup" });
        builder.Body.Add(DocxFixtureBuilder.Paragraph("Normal", DocxFixtureBuilder.TextRun("  保留  ")));
        builder.Body.Add(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "Normal" }),
            new Hyperlink(DocxFixtureBuilder.TextRun("规范")) { Anchor = "top" },
            new Run(
                new RunProperties(new RunStyle { Val = "Sup" }),
                new Text("下标位") { Space = SpaceProcessingModeValues.Preserve })));
        var result = Parse(builder.Build(), SampleMap());
        Assert.That(result.Success, Is.True, Dump(result));
        Assert.That(result.Document!.Blocks[0].Runs.Single().Text, Is.EqualTo("  保留  "));
        Assert.That(result.Document.Blocks[1].Runs[0].Text, Is.EqualTo("规范"));
        Assert.That(result.Document.Blocks[1].Runs[1].Semantic, Is.EqualTo(RunSemantic.Superscript));
    }

    [Test]
    public void DecimalNumberingRestartAndManualNumbers()
    {
        var builder = WordBuilder();
        builder.Numbering.Add(DocxFixtureBuilder.NumberingAbstract(
            0,
            new DocxFixtureBuilder.LevelSpec(0, "%1.", 1, NumberFormatValues.Decimal, null),
            new DocxFixtureBuilder.LevelSpec(1, "%1.%2", 1, NumberFormatValues.Decimal, null),
            new DocxFixtureBuilder.LevelSpec(2, "%1.%2.%3", 1, NumberFormatValues.Decimal, 0)));
        builder.Numbering.Add(DocxFixtureBuilder.ListInstance(1, 0));
        builder.Numbering.Add(DocxFixtureBuilder.ListInstance(2, 0, 0, 5));
        builder.Body.Add(DocxFixtureBuilder.Numbered("Normal", 1, 0, "总则"));
        builder.Body.Add(DocxFixtureBuilder.Numbered("Normal", 1, 1, "材料"));
        builder.Body.Add(DocxFixtureBuilder.Numbered("Normal", 1, 1, "配筋"));
        builder.Body.Add(DocxFixtureBuilder.Numbered("Normal", 1, 2, "细部"));
        builder.Body.Add(DocxFixtureBuilder.Numbered("Normal", 1, 0, "构造"));
        builder.Body.Add(DocxFixtureBuilder.Numbered("Normal", 1, 1, "连接"));
        builder.Body.Add(DocxFixtureBuilder.Numbered("Normal", 1, 2, "继续"));
        builder.Body.Add(DocxFixtureBuilder.Numbered("Normal", 2, 0, "另一列表"));
        builder.Body.Add(DocxFixtureBuilder.Paragraph("Normal", DocxFixtureBuilder.TextRun("1.1 手工编号不拆出。")));
        var result = Parse(builder.Build(), SampleMap());
        Assert.That(result.Success, Is.True, Dump(result));
        Assert.That(result.Document!.Blocks.Select(block => block.Numbering?.Label), Is.EqualTo(new string?[]
        {
            "1.", "1.1", "1.2", "1.2.1", "2.", "2.1", "2.1.2", "5.", null
        }));
        Assert.That(result.Document.Blocks[0].Numbering!.SourceKind, Is.EqualTo(NumberingSourceKind.Automatic));
        Assert.That(result.Document.Blocks[0].Runs.Single().Text, Is.EqualTo("总则"));
        Assert.That(result.Document.Blocks[6].Numbering!.Label, Is.EqualTo("2.1.2"));
        Assert.That(result.Document.Blocks[8].Runs.Single().Text, Is.EqualTo("1.1 手工编号不拆出。"));
    }

    [Test]
    public void UnsupportedNumberFormatsDoNotInventLabels()
    {
        foreach (var format in new[] { NumberFormatValues.LowerRoman, NumberFormatValues.Bullet })
        {
            var builder = WordBuilder();
            builder.Numbering.Add(DocxFixtureBuilder.NumberingAbstract(0, new DocxFixtureBuilder.LevelSpec(0, "%1.", 1, format, null)));
            builder.Numbering.Add(DocxFixtureBuilder.ListInstance(1, 0));
            builder.Body.Add(DocxFixtureBuilder.Numbered("Normal", 1, 0, "条目"));
            var result = Parse(builder.Build(), SampleMap());
            Assert.That(result.Document, Is.Null, format.ToString());
            Assert.That(result.Diagnostics.Any(item => item.Code == DiagnosticCodes.ENumbering && item.SourceRef!.ParagraphIndex == 0), Is.True, Dump(result));
            Assert.That(Dump(result), Does.Not.Contain("label"));
        }
    }

    [Test]
    public void MissingNumberingDefinitionIsAnError()
    {
        var builder = WordBuilder();
        builder.Body.Add(DocxFixtureBuilder.Numbered("Normal", 9, 0, "条目"));
        var result = Parse(builder.Build(), SampleMap());
        Assert.That(result.Document, Is.Null);
        Assert.That(result.Diagnostics.Single(item => item.Severity == Severity.Error).Code, Is.EqualTo(DiagnosticCodes.ENumbering));
    }

    [Test]
    public void UnsupportedContentIsLocatedWithoutReturningADocument()
    {
        var table = TableDocument();
        var tableResult = Parse(table, SampleMap());
        Assert.That(tableResult.Document, Is.Null);
        Assert.That(tableResult.Diagnostics.Single(item => item.Details!["kind"] == "table").SourceRef!.ParagraphIndex, Is.EqualTo(1));
        Assert.That(tableResult.Diagnostics.Single().Details!["excerpt"], Does.Contain("单元格"));

        var revision = WordBuilder();
        revision.Body.Add(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "Normal" }),
            new InsertedRun(new Run(new Text("未接受"))) { Author = "editor" }));
        var revisionResult = Parse(revision.Build(), SampleMap());
        Assert.That(revisionResult.Document, Is.Null);
        Assert.That(revisionResult.Diagnostics.Single().Details!["kind"], Is.EqualTo("revision"));

        var tab = WordBuilder();
        tab.Body.Add(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "Normal" }),
            new Run(new TabChar(), new Text("后续"))));
        var tabResult = Parse(tab.Build(), SampleMap());
        Assert.That(tabResult.Document, Is.Null);
        Assert.That(tabResult.Diagnostics.Single().Details!["kind"], Is.EqualTo("tab"));

        var field = WordBuilder();
        field.Body.Add(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "Normal" }),
            new SimpleField(new Run(new Text("3"))) { Instruction = " PAGE " }));
        var fieldResult = Parse(field.Build(), SampleMap());
        Assert.That(fieldResult.Document, Is.Null);
        Assert.That(fieldResult.Diagnostics.Single().Details!["kind"], Is.EqualTo("field"));

        var image = WordBuilder();
        image.Body.Add(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "Normal" }),
            new Run(new Text("见图"), new Drawing())));
        var imageResult = Parse(image.Build(), SampleMap());
        Assert.That(imageResult.Document, Is.Null);
        Assert.That(imageResult.Diagnostics.Any(item => item.Details!["kind"] == "image"), Is.True, Dump(imageResult));

        var equation = WordBuilder();
        equation.Body.Add(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "Normal" }),
            new DocumentFormat.OpenXml.Math.OfficeMath()));
        var equationResult = Parse(equation.Build(), SampleMap());
        Assert.That(equationResult.Document, Is.Null);
        Assert.That(equationResult.Diagnostics.Single().Details!["kind"], Is.EqualTo("equation"));
    }

    [Test]
    public void TextBoxFootnoteAndEmbeddedObjectAreLocated()
    {
        var textBox = WordBuilder();
        var paragraph = new Paragraph();
        paragraph.InnerXml = "<w:pPr xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:pStyle w:val=\"Normal\"/></w:pPr>"
            + "<w:r xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:pict><w:txbxContent><w:p><w:r><w:t>框内</w:t></w:r></w:p></w:txbxContent></w:pict></w:r>";
        textBox.Body.Add(paragraph);
        var textBoxResult = Parse(textBox.Build(), SampleMap());
        Assert.That(textBoxResult.Document, Is.Null);
        Assert.That(textBoxResult.Diagnostics.Any(item => item.Details!["kind"] == "textBox"), Is.True, Dump(textBoxResult));

        var footnote = WordBuilder();
        footnote.Body.Add(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "Normal" }),
            new Run(new FootnoteReference { Id = 1 })));
        var footnoteResult = Parse(footnote.Build(), SampleMap());
        Assert.That(footnoteResult.Document, Is.Null);
        Assert.That(footnoteResult.Diagnostics.Any(item => item.Details!["kind"] == "footnote"), Is.True, Dump(footnoteResult));

        var embedded = WordBuilder();
        embedded.Body.Add(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "Normal" }),
            new Run(new EmbeddedObject())));
        var embeddedResult = Parse(embedded.Build(), SampleMap());
        Assert.That(embeddedResult.Document, Is.Null);
        Assert.That(embeddedResult.Diagnostics.Any(item => item.Details!["kind"] == "embeddedObject"), Is.True, Dump(embeddedResult));
    }

    [Test]
    public void OnePassReportsEveryBlockingIssue()
    {
        var builder = WordBuilder();
        builder.Styles.Add(DocxFixtureBuilder.ParagraphStyle("Heading3", "heading 3", "Normal"));
        builder.Body.Add(DocxFixtureBuilder.Paragraph("Heading3", DocxFixtureBuilder.TextRun("三级")));
        builder.Body.Add(new Table(new TableRow(new TableCell(
            new Paragraph(new Run(new Text("表")))))));
        builder.Body.Add(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "Normal" }),
            new InsertedRun(new Run(new Text("修订"))) { Author = "editor" }));
        var result = Parse(builder.Build(), SampleMap());
        Assert.That(result.Document, Is.Null);
        Assert.That(result.Diagnostics.Count(item => item.Severity == Severity.Error), Is.EqualTo(3));
        Assert.That(result.Diagnostics.Select(item => item.Details!["kind"]), Is.EquivalentTo(new[] { "unmappedStyle", "table", "revision" }));
    }

    [Test]
    public void HeaderFooterAndMacrosStayOutOfTheBody()
    {
        var builder = WordBuilder();
        builder.HeaderText = "内部页眉";
        builder.FooterText = "内部页脚";
        builder.IncludeVba = true;
        builder.Body.Add(DocxFixtureBuilder.Paragraph("Normal", DocxFixtureBuilder.TextRun("正文")));
        var result = Parse(builder.Build(), SampleMap());
        Assert.That(result.Success, Is.True, Dump(result));
        Assert.That(Project(result.Document!), Does.Not.Contain("页眉").And.Not.Contain("页脚"));
        Assert.That(result.Diagnostics.Count(item => item.Code == DocxDiagnosticCodes.IgnoredHeaderFooter), Is.EqualTo(2));
        Assert.That(result.Diagnostics.Any(item => item.Code == DocxDiagnosticCodes.MacroNotExecuted), Is.True);
    }

    [Test]
    public void CorruptAndForeignPackagesAreReadErrors()
    {
        foreach (var bytes in new[]
        {
            new byte[] { 1, 2, 3, 4 },
            new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00 },
            new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }
        })
        {
            var result = Parse(bytes, SampleMap(), name: "损坏.docx");
            Assert.That(result.Document, Is.Null);
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(DiagnosticCodes.EDocxRead));
            Assert.That(result.Diagnostics.Single().Message, Does.Contain("损坏.docx"));
        }
    }

    [Test]
    public void FileParseIsReadOnlyAndReportsLocks()
    {
        var bytes = Note("Heading1", "Heading2", "Normal", "heading 1", "heading 2", "Normal", false);
        var directory = TestContext.CurrentContext.WorkDirectory;
        var path = Path.Combine(directory, "t04-readonly-" + Guid.NewGuid().ToString("N") + ".docx");
        File.WriteAllBytes(path, bytes);
        try
        {
            var before = Sha(File.ReadAllBytes(path));
            var result = ParseFile(path, SampleMap());
            Assert.That(result.Success, Is.True, Dump(result));
            Assert.That(Sha(File.ReadAllBytes(path)), Is.EqualTo(before));
            Assert.That(result.Document!.Source.ContentHash, Is.EqualTo(Sha(bytes)));
            Assert.That(result.Document.DocumentId, Is.EqualTo("sample-doc-001"));
            Assert.That(result.Document.DocumentId, Is.Not.EqualTo(path));
            Assert.That(result.Document.Source.LocalPath, Is.EqualTo(path));
            Assert.That(result.Document.SchemaVersion, Is.EqualTo("1.0"));

            using var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            var denied = ParseFile(path, SampleMap());
            Assert.That(denied.Document, Is.Null);
            Assert.That(denied.Diagnostics.Single().Code, Is.EqualTo(DiagnosticCodes.EDocxRead));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Test]
    public void SuccessfulDocumentSatisfiesTheJsonSchema()
    {
        var bytes = Note("Heading1", "Heading2", "Normal", "heading 1", "heading 2", "Normal", false);
        var result = Parse(bytes, SampleMap());
        Assert.That(result.Success, Is.True, Dump(result));
        var document = result.Document!;
        var json = JsonProtocol.Default.SaveDocument(document);
        var loaded = JsonProtocol.Default.LoadDocument(json);
        Assert.That(loaded.Success, Is.True, string.Join(" | ", loaded.Diagnostics.Select(item => item.Message)));
        Assert.That(Project(loaded.Value!), Is.EqualTo(Project(document)));
        Assert.That(document.Source.ContentHash, Is.EqualTo(Sha(bytes)));
    }

    [Test]
    public void CancellationAndCallerSuppliedLimitsStopTheParse()
    {
        var bytes = Note("Heading1", "Heading2", "Normal", "heading 1", "heading 2", "Normal", false);
        var cancelled = new DocxDocumentParser(Options(SampleMap())).Parse(
            new DocxBytesSource("sample.docx", bytes),
            Profile(),
            new CancellationToken(true));
        Assert.That(cancelled.Document, Is.Null);
        Assert.That(cancelled.Diagnostics.Single().Code, Is.EqualTo(DiagnosticCodes.Cancelled));

        var tooBig = Parse(bytes, SampleMap(), new DocxParseOptions
        {
            DocumentId = "sample-doc-001",
            DisciplineCode = "structure",
            StyleMap = SampleMap(),
            MaxSourceBytes = 32
        });
        Assert.That(tooBig.Diagnostics.Single().Code, Is.EqualTo(DiagnosticCodes.EResourceLimit));

        var tooExpanded = Parse(bytes, SampleMap(), new DocxParseOptions
        {
            DocumentId = "sample-doc-001",
            DisciplineCode = "structure",
            StyleMap = SampleMap(),
            MaxUncompressedBytes = 32
        });
        Assert.That(tooExpanded.Diagnostics.Single().Code, Is.EqualTo(DiagnosticCodes.EResourceLimit));
    }

    [Test]
    public void BasedOnCyclesDoNotHang()
    {
        var builder = new DocxFixtureBuilder();
        builder.Styles.Add(DocxFixtureBuilder.ParagraphStyle("A", "甲", "B", true));
        builder.Styles.Add(DocxFixtureBuilder.ParagraphStyle("B", "乙", "A"));
        builder.Body.Add(DocxFixtureBuilder.Paragraph("A", DocxFixtureBuilder.TextRun("循环")));
        var result = Parse(builder.Build(), SampleMap());
        Assert.That(result.Document, Is.Null);
        Assert.That(result.Diagnostics.Any(item => item.Details!["kind"] == "unmappedStyle"), Is.True);
    }

    [Test]
    public void OptionsRejectSilentTargets()
    {
        Assert.Throws<ArgumentException>(() => new DocxDocumentParser(new DocxParseOptions()));
        Assert.Throws<ArgumentException>(() => new DocxDocumentParser(new DocxParseOptions
        {
            DocumentId = "id",
            DisciplineCode = "structure",
            StyleMap = new StyleMap
            {
                Entries = { new StyleMapEntry { Match = StyleMapMatch.StyleId, Key = "Normal", Target = BlockType.Spacer } }
            }
        }));
    }

    private static byte[] Note(string heading1, string heading2, string body, string heading1Name, string heading2Name, string bodyName, bool formatted)
    {
        var builder = new DocxFixtureBuilder { TerminatorStyleId = body };
        builder.Styles.Add(DocxFixtureBuilder.ParagraphStyle(body, bodyName, null, true));
        builder.Styles.Add(DocxFixtureBuilder.ParagraphStyle(heading1, heading1Name, body));
        builder.Styles.Add(DocxFixtureBuilder.ParagraphStyle(heading2, heading2Name, body));
        builder.Body.Add(DocxFixtureBuilder.Paragraph(heading1, DocxFixtureBuilder.TextRun("材料要求")));
        builder.Body.Add(DocxFixtureBuilder.Paragraph(heading2, DocxFixtureBuilder.TextRun("混凝土")));
        if (!formatted)
        {
            builder.Body.Add(DocxFixtureBuilder.Paragraph(body, DocxFixtureBuilder.TextRun("（1）钢筋采用HRB400。")));
            builder.Body.Add(new Paragraph(
                new ParagraphProperties(new ParagraphStyleId { Val = body }),
                DocxFixtureBuilder.TextRun("活荷载"),
                new Run(
                    new RunProperties(new VerticalTextAlignment { Val = VerticalPositionValues.Superscript }),
                    new Text("2") { Space = SpaceProcessingModeValues.Preserve }),
                DocxFixtureBuilder.TextRun("与字面²。")));
            builder.Body.Add(DocxFixtureBuilder.Paragraph(body, DocxFixtureBuilder.TextRun("强度等级 C30。")));
            return builder.Build();
        }

        builder.Body.Add(new Paragraph(
            new ParagraphProperties(
                new ParagraphStyleId { Val = body },
                new SpacingBetweenLines { Before = "240", After = "120", Line = "480", LineRule = LineSpacingRuleValues.Auto },
                new Indentation { Left = "720", FirstLine = "480" }),
            new Run(
                new RunProperties(
                    new Bold(),
                    new Italic(),
                    new Underline { Val = UnderlineValues.Single },
                    new Color { Val = "FF0000" },
                    new FontSize { Val = "44" },
                    new RunFonts { Ascii = "Arial", EastAsia = "宋体" }),
                new Text("（1）钢筋采用HRB400。") { Space = SpaceProcessingModeValues.Preserve })));
        builder.Body.Add(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = body }),
            DocxFixtureBuilder.TextRun("活荷载"),
            new Run(
                new RunProperties(new VerticalTextAlignment { Val = VerticalPositionValues.Superscript }),
                new Text("2") { Space = SpaceProcessingModeValues.Preserve }),
            DocxFixtureBuilder.TextRun("与字面²。")));
        builder.Body.Add(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = body }),
            new Run(
                new RunProperties(new FontSize { Val = "18" }, new Color { Val = "00FF00" }),
                new Text("强度等级 C30。") { Space = SpaceProcessingModeValues.Preserve })));
        builder.Body.Add(new SectionProperties(new PageMargin
        {
            Top = 720,
            Bottom = 720,
            Left = 900,
            Right = 900,
            Header = 360,
            Footer = 360,
            Gutter = 0
        }));
        return builder.Build();
    }

    private static byte[] TableDocument()
    {
        var builder = WordBuilder();
        builder.Body.Add(DocxFixtureBuilder.Paragraph("Normal", DocxFixtureBuilder.TextRun("前文")));
        builder.Body.Add(new Table(new TableRow(new TableCell(
            new Paragraph(new Run(new Text("单元格")))))));
        return builder.Build();
    }

    private static DocxFixtureBuilder WordBuilder()
    {
        var builder = new DocxFixtureBuilder();
        builder.Styles.Add(DocxFixtureBuilder.ParagraphStyle("Normal", "Normal", null, true));
        builder.Styles.Add(DocxFixtureBuilder.ParagraphStyle("Heading1", "heading 1", "Normal"));
        builder.Styles.Add(DocxFixtureBuilder.ParagraphStyle("Heading2", "heading 2", "Normal"));
        return builder;
    }

    private static StyleMap SampleMap()
    {
        return new StyleMap
        {
            Entries =
            {
                new StyleMapEntry { Match = StyleMapMatch.StyleId, Key = "Heading1", Target = BlockType.Heading1 },
                new StyleMapEntry { Match = StyleMapMatch.StyleId, Key = "Heading2", Target = BlockType.Heading2 },
                new StyleMapEntry { Match = StyleMapMatch.StyleId, Key = "Normal", Target = BlockType.Paragraph },
                new StyleMapEntry { Match = StyleMapMatch.StyleId, Key = "1", Target = BlockType.Heading1 },
                new StyleMapEntry { Match = StyleMapMatch.StyleId, Key = "2", Target = BlockType.Heading2 },
                new StyleMapEntry { Match = StyleMapMatch.StyleId, Key = "a", Target = BlockType.Paragraph },
                new StyleMapEntry { Match = StyleMapMatch.Name, Key = "heading 1", Target = BlockType.Heading1 },
                new StyleMapEntry { Match = StyleMapMatch.Name, Key = "heading 2", Target = BlockType.Heading2 },
                new StyleMapEntry { Match = StyleMapMatch.Name, Key = "标题 1", Target = BlockType.Heading1 },
                new StyleMapEntry { Match = StyleMapMatch.Name, Key = "标题 2", Target = BlockType.Heading2 },
                new StyleMapEntry { Match = StyleMapMatch.Name, Key = "正文", Target = BlockType.Paragraph },
                new StyleMapEntry { Match = StyleMapMatch.Name, Key = "Normal", Target = BlockType.Paragraph }
            }
        };
    }

    private static DocumentParseResult Parse(byte[] bytes, StyleMap map, DocxParseOptions? options = null, string name = "sample.docx")
    {
        var parser = new DocxDocumentParser(options ?? Options(map));
        return parser.Parse(new DocxBytesSource(name, bytes), Profile(), CancellationToken.None);
    }

    private static DocumentParseResult ParseFile(string path, StyleMap map)
    {
        return new DocxDocumentParser(Options(map)).Parse(new DocxFileSource(path), Profile(), CancellationToken.None);
    }

    private static DocxParseOptions Options(StyleMap map)
    {
        return new DocxParseOptions
        {
            DocumentId = "sample-doc-001",
            DisciplineCode = "structure",
            StyleMap = map
        };
    }

    private static ParseProfile Profile()
    {
        return new ParseProfile { Standard = new StandardRef { Id = "institution-note", Version = "pending" } };
    }

    private static string Project(ModelDocument document)
    {
        return string.Join("\n", document.Blocks.Select(block =>
            block.Type + "#" + block.SlotCount + "#" + block.Numbering?.Label + "#" + block.Numbering?.Level + "#"
            + string.Join("|", block.Runs.Select(run => run.Semantic + ":" + run.Text.Replace("\n", "\\n")))));
    }

    private static string Dump(DocumentParseResult result)
    {
        return string.Join(" | ", result.Diagnostics.Select(item => item.Severity + ":" + item.Code + ":" + item.Message));
    }

    private static string Sha(byte[] bytes)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(bytes);
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var value in hash)
            builder.Append(value.ToString("x2"));
        return builder.ToString();
    }
}
