using System.Linq;
using System.Threading;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using Justified.SpecificationReflow.AutoCAD.Contracts.Ports;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;
using Justified.SpecificationReflow.AutoCAD.DocxAdapter;
using NUnit.Framework;

namespace Justified.SpecificationReflow.AutoCAD.Core.Tests;

// T05 审计已交付的 DocxDocumentParser。这些断言补上 T04 没有单独锁住的重启、抑制和空白边界。
public class NumberingWhitespaceAuditTests
{
    [Test]
    public void DecimalLabelsFollowRestartRulesAndStayOutOfTheBody()
    {
        var builder = NormalBuilder();
        builder.Numbering.Add(DocxFixtureBuilder.NumberingAbstract(
            0,
            new DocxFixtureBuilder.LevelSpec(0, "%1.", 1, NumberFormatValues.Decimal, null),
            new DocxFixtureBuilder.LevelSpec(1, "%1.%2", 1, NumberFormatValues.Decimal, null),
            new DocxFixtureBuilder.LevelSpec(2, "%1.%2.%3", 1, NumberFormatValues.Decimal, null)));
        builder.Numbering.Add(DocxFixtureBuilder.ListInstance(1, 0));
        builder.Numbering.Add(DocxFixtureBuilder.ListInstance(2, 0, 0, 5));
        builder.Body.Add(DocxFixtureBuilder.Numbered("Normal", 1, 0, "1. 甲"));
        builder.Body.Add(DocxFixtureBuilder.Paragraph("Normal", DocxFixtureBuilder.TextRun("中间正文")));
        builder.Body.Add(DocxFixtureBuilder.Numbered("Normal", 1, 0, "乙"));
        builder.Body.Add(DocxFixtureBuilder.Numbered("Normal", 1, 1, "子项"));
        builder.Body.Add(DocxFixtureBuilder.Paragraph("Normal", DocxFixtureBuilder.TextRun("仍不重启")));
        builder.Body.Add(DocxFixtureBuilder.Numbered("Normal", 1, 1, "续项"));
        builder.Body.Add(DocxFixtureBuilder.Numbered("Normal", 1, 2, "细部"));
        builder.Body.Add(DocxFixtureBuilder.Numbered("Normal", 1, 0, "丙"));
        builder.Body.Add(DocxFixtureBuilder.Numbered("Normal", 1, 2, "跳级"));
        builder.Body.Add(DocxFixtureBuilder.Numbered("Normal", 2, 0, "另一列表"));
        builder.Body.Add(DocxFixtureBuilder.Paragraph("Normal", DocxFixtureBuilder.TextRun("1. 手工编号留在正文")));

        var result = Parse(builder);
        Assert.That(result.Success, Is.True, Dump(result));
        Assert.That(result.Document!.Blocks.Select(block => block.Numbering?.Label), Is.EqualTo(new string?[]
        {
            "1.", null, "2.", "2.1", null, "2.2", "2.2.1", "3.", "3.1.1", "5.", null
        }));
        Assert.That(result.Document.Blocks[0].Numbering!.SourceKind, Is.EqualTo(NumberingSourceKind.Automatic));
        Assert.That(result.Document.Blocks[0].Numbering!.Level, Is.EqualTo(0));
        Assert.That(result.Document.Blocks[0].Runs.Single().Text, Is.EqualTo("1. 甲"));
        Assert.That(result.Document.Blocks[2].Runs.Single().Text, Is.EqualTo("乙"));
        Assert.That(result.Document.Blocks[8].Runs.Single().Text, Is.EqualTo("跳级"));
        Assert.That(result.Document.Blocks[10].Numbering, Is.Null);
        Assert.That(result.Document.Blocks[10].Runs.Single().Text, Is.EqualTo("1. 手工编号留在正文"));
    }

    [Test]
    public void ExplicitLevelRestartOnlyFollowsTheDeclaredLevel()
    {
        var builder = NormalBuilder();
        builder.Numbering.Add(DocxFixtureBuilder.NumberingAbstract(
            0,
            new DocxFixtureBuilder.LevelSpec(0, "%1.", 1, NumberFormatValues.Decimal, null),
            new DocxFixtureBuilder.LevelSpec(1, "%1.%2", 1, NumberFormatValues.Decimal, null),
            new DocxFixtureBuilder.LevelSpec(2, "%1.%2.%3", 1, NumberFormatValues.Decimal, 1)));
        builder.Numbering.Add(DocxFixtureBuilder.ListInstance(1, 0));
        builder.Body.Add(DocxFixtureBuilder.Numbered("Normal", 1, 0, "甲"));
        builder.Body.Add(DocxFixtureBuilder.Numbered("Normal", 1, 1, "乙"));
        builder.Body.Add(DocxFixtureBuilder.Numbered("Normal", 1, 2, "丙"));
        builder.Body.Add(DocxFixtureBuilder.Numbered("Normal", 1, 1, "丁"));
        builder.Body.Add(DocxFixtureBuilder.Numbered("Normal", 1, 2, "戊"));
        builder.Body.Add(DocxFixtureBuilder.Numbered("Normal", 1, 0, "己"));
        builder.Body.Add(DocxFixtureBuilder.Numbered("Normal", 1, 2, "庚"));

        var result = Parse(builder);
        Assert.That(result.Success, Is.True, Dump(result));
        Assert.That(result.Document!.Blocks.Select(block => block.Numbering!.Label), Is.EqualTo(new[]
        {
            "1.", "1.1", "1.1.1", "1.2", "1.2.2", "2.", "2.1.1"
        }));
        Assert.That(result.Document.Blocks.Select(block => block.Runs.Single().Text), Is.EqualTo(new[]
        {
            "甲", "乙", "丙", "丁", "戊", "己", "庚"
        }));
    }

    [Test]
    public void StyleNumberingStopsWhenTheParagraphTurnsItOff()
    {
        var builder = new DocxFixtureBuilder();
        builder.Styles.Add(new Style(
            new StyleName { Val = "Normal" },
            new StyleParagraphProperties(
                new NumberingProperties(
                    new NumberingLevelReference { Val = 0 },
                    new NumberingId { Val = 1 })))
        {
            Type = StyleValues.Paragraph,
            StyleId = "Normal",
            Default = true
        });
        builder.Numbering.Add(DocxFixtureBuilder.NumberingAbstract(
            0,
            new DocxFixtureBuilder.LevelSpec(0, "%1.", 1, NumberFormatValues.Decimal, null)));
        builder.Numbering.Add(DocxFixtureBuilder.ListInstance(1, 0));
        builder.Body.Add(DocxFixtureBuilder.Paragraph("Normal", DocxFixtureBuilder.TextRun("甲")));
        builder.Body.Add(new Paragraph(
            new ParagraphProperties(
                new ParagraphStyleId { Val = "Normal" },
                new NumberingProperties(
                    new NumberingLevelReference { Val = 0 },
                    new NumberingId { Val = 0 })),
            DocxFixtureBuilder.TextRun("2. 手工")));
        builder.Body.Add(DocxFixtureBuilder.Paragraph("Normal", DocxFixtureBuilder.TextRun("乙")));

        var result = Parse(builder);
        Assert.That(result.Success, Is.True, Dump(result));
        Assert.That(result.Document!.Blocks.Select(block => block.Numbering?.Label), Is.EqualTo(new string?[] { "1.", null, "2." }));
        Assert.That(result.Document.Blocks[0].Runs.Single().Text, Is.EqualTo("甲"));
        Assert.That(result.Document.Blocks[1].Numbering, Is.Null);
        Assert.That(result.Document.Blocks[1].Runs.Single().Text, Is.EqualTo("2. 手工"));
        Assert.That(result.Document.Blocks[2].Runs.Single().Text, Is.EqualTo("乙"));
    }

    [Test]
    public void ConsecutiveRealEmptyParagraphsStayAndTheTerminatorDoesNot()
    {
        var builder = NormalBuilder();
        builder.Numbering.Add(DocxFixtureBuilder.NumberingAbstract(
            0,
            new DocxFixtureBuilder.LevelSpec(0, "%1.", 1, NumberFormatValues.Decimal, null)));
        builder.Numbering.Add(DocxFixtureBuilder.ListInstance(1, 0));
        builder.Body.Add(DocxFixtureBuilder.Paragraph("Normal"));
        builder.Body.Add(DocxFixtureBuilder.Paragraph("Normal"));
        builder.Body.Add(DocxFixtureBuilder.Paragraph("Normal", DocxFixtureBuilder.TextRun("   ")));
        builder.Body.Add(DocxFixtureBuilder.Paragraph("Normal"));
        builder.Body.Add(new Paragraph(new ParagraphProperties(
            new ParagraphStyleId { Val = "Normal" },
            new NumberingProperties(
                new NumberingLevelReference { Val = 0 },
                new NumberingId { Val = 1 }))));

        var result = Parse(builder);
        Assert.That(result.Success, Is.True, Dump(result));
        Assert.That(result.Document!.Blocks.Select(block => block.Type), Is.EqualTo(new[]
        {
            BlockType.Spacer, BlockType.Spacer, BlockType.Paragraph, BlockType.Spacer, BlockType.Paragraph
        }));
        Assert.That(result.Document.Blocks.Select(block => block.Id), Is.EqualTo(new[] { "p0", "p1", "p2", "p3", "p4" }));
        Assert.That(result.Document.Blocks[0].SlotCount, Is.EqualTo(1));
        Assert.That(result.Document.Blocks[1].SlotCount, Is.EqualTo(1));
        Assert.That(result.Document.Blocks[2].Runs.Single().Text, Is.EqualTo("   "));
        Assert.That(result.Document.Blocks[4].Numbering!.Label, Is.EqualTo("1."));
        Assert.That(result.Document.Blocks[4].Runs, Is.Empty);

        var onlyEmpties = NormalBuilder();
        onlyEmpties.Body.Add(DocxFixtureBuilder.Paragraph("Normal"));
        onlyEmpties.Body.Add(DocxFixtureBuilder.Paragraph("Normal"));
        var emptied = Parse(onlyEmpties);
        Assert.That(emptied.Success, Is.True, Dump(emptied));
        Assert.That(emptied.Document!.Blocks.Select(block => block.Type), Is.EqualTo(new[] { BlockType.Spacer, BlockType.Spacer }));
        Assert.That(emptied.Document.Blocks.Select(block => block.Id), Is.EqualTo(new[] { "p0", "p1" }));

        var numberedTail = NormalBuilder();
        numberedTail.EmitTerminator = false;
        numberedTail.Numbering.Add(DocxFixtureBuilder.NumberingAbstract(
            0,
            new DocxFixtureBuilder.LevelSpec(0, "%1.", 1, NumberFormatValues.Decimal, null)));
        numberedTail.Numbering.Add(DocxFixtureBuilder.ListInstance(1, 0));
        numberedTail.Body.Add(DocxFixtureBuilder.Paragraph("Normal", DocxFixtureBuilder.TextRun("甲")));
        numberedTail.Body.Add(new Paragraph(new ParagraphProperties(
            new ParagraphStyleId { Val = "Normal" },
            new NumberingProperties(
                new NumberingLevelReference { Val = 0 },
                new NumberingId { Val = 1 }))));
        var tail = Parse(numberedTail);
        Assert.That(tail.Success, Is.True, Dump(tail));
        Assert.That(tail.Document!.Blocks.Select(block => block.Runs.Single().Text), Is.EqualTo(new[] { "甲" }));
        Assert.That(tail.Document.Blocks.Single().Numbering, Is.Null);
    }

    [Test]
    public void ExplicitHardBreaksStayAndAutomaticWrappingDoesNot()
    {
        var builder = NormalBuilder();
        builder.Numbering.Add(DocxFixtureBuilder.NumberingAbstract(
            0,
            new DocxFixtureBuilder.LevelSpec(0, "%1.", 1, NumberFormatValues.Decimal, null)));
        builder.Numbering.Add(DocxFixtureBuilder.ListInstance(1, 0));
        var carried = new string('框', 48) + "梁纵向钢筋应通长设置，不因页面宽度折成多段。";
        builder.Body.Add(new Paragraph(
            new ParagraphProperties(
                new ParagraphStyleId { Val = "Normal" },
                new NumberingProperties(
                    new NumberingLevelReference { Val = 0 },
                    new NumberingId { Val = 1 })),
            DocxFixtureBuilder.TextRun("上行"),
            new Run(new CarriageReturn(), new CarriageReturn()),
            DocxFixtureBuilder.TextRun("下行")));
        builder.Body.Add(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "Normal" }),
            new Run(
                new Text(carried.Substring(0, 20)) { Space = SpaceProcessingModeValues.Preserve },
                new LastRenderedPageBreak(),
                new Text(carried.Substring(20)) { Space = SpaceProcessingModeValues.Preserve })));
        builder.Body.Add(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "Normal" }),
            new Run(new Break { Type = BreakValues.TextWrapping }, new Break { Type = BreakValues.TextWrapping })));

        var result = Parse(builder);
        Assert.That(result.Success, Is.True, Dump(result));
        Assert.That(result.Document!.Blocks.Select(block => block.Type), Is.EqualTo(new[]
        {
            BlockType.Paragraph, BlockType.Paragraph, BlockType.Paragraph
        }));
        Assert.That(result.Document.Blocks[0].Numbering!.Label, Is.EqualTo("1."));
        Assert.That(result.Document.Blocks[0].Runs.Select(run => run.Text), Is.EqualTo(new[] { "上行", "\n", "\n", "下行" }));
        Assert.That(string.Concat(result.Document.Blocks[0].Runs.Select(run => run.Text)), Is.EqualTo("上行\n\n下行"));
        Assert.That(result.Document.Blocks[1].Runs.Select(run => run.Text), Is.EqualTo(new[] { carried }));
        Assert.That(string.Concat(result.Document.Blocks[1].Runs.Select(run => run.Text)), Does.Not.Contain("\n"));
        Assert.That(result.Document.Blocks[2].Runs.Select(run => run.Text), Is.EqualTo(new[] { "\n", "\n" }));
        Assert.That(result.Document.Blocks[2].Type, Is.EqualTo(BlockType.Paragraph));
    }

    private static DocxFixtureBuilder NormalBuilder()
    {
        var builder = new DocxFixtureBuilder();
        builder.Styles.Add(DocxFixtureBuilder.ParagraphStyle("Normal", "Normal", null, true));
        return builder;
    }

    private static DocumentParseResult Parse(DocxFixtureBuilder builder)
    {
        var parser = new DocxDocumentParser(new DocxParseOptions
        {
            DocumentId = "t05-audit",
            DisciplineCode = "structure",
            StyleMap = new StyleMap
            {
                Entries =
                {
                    new StyleMapEntry { Match = StyleMapMatch.StyleId, Key = "Normal", Target = BlockType.Paragraph }
                }
            }
        });
        return parser.Parse(new DocxBytesSource("t05-audit.docx", builder.Build()), new ParseProfile
        {
            Standard = new StandardRef { Id = "institution-note", Version = "pending" }
        }, CancellationToken.None);
    }

    private static string Dump(DocumentParseResult result)
    {
        return string.Join(" | ", result.Diagnostics.Select(item => item.Severity + ":" + item.Code + ":" + item.Message));
    }
}
