using System;
using System.IO;
using System.Linq;
using System.Threading;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using Justified.SpecificationReflow.AutoCAD.Contracts.Ports;
using Justified.SpecificationReflow.AutoCAD.DocxAdapter;
using NUnit.Framework;

namespace Justified.SpecificationReflow.AutoCAD.Core.Tests;

public class RealisticNoteTests
{
    [Test]
    public void HandoffSampleOpensAsTheSameNote()
    {
        var built = RealisticNote.Build();
        var path = Path.Combine(Root(), "测试文件", "示例-结构说明.docx");
        if (!File.Exists(path))
            File.WriteAllBytes(path, built);

        var fromFile = Parse(File.ReadAllBytes(path));
        var fromBuilder = Parse(built);
        Assert.That(fromFile.Success, Is.True, Dump(fromFile));
        Assert.That(fromBuilder.Success, Is.True, Dump(fromBuilder));
        Assert.That(Project(fromFile.Document!), Is.EqualTo(Project(fromBuilder.Document!)));

        var document = fromFile.Document!;
        Assert.That(document.Blocks.Select(block => block.Numbering?.Label), Is.EqualTo(new string?[]
        {
            null, null, "1.", "1.1", "1.2", null, null, "2.", "2.1", "2.2", null, null, "3.", "3.1"
        }));
        Assert.That(document.Blocks[0].Type, Is.EqualTo(BlockType.Heading1));
        Assert.That(document.Blocks[0].Runs.Single().Text, Is.EqualTo("结构设计总说明"));
        Assert.That(document.Blocks[5].Type, Is.EqualTo(BlockType.Spacer));
        Assert.That(document.Blocks[8].Runs.Select(run => run.Semantic + ":" + run.Text), Is.EqualTo(new[]
        {
            "Normal:混凝土轴心抗压强度设计值f",
            "Subscript:c",
            "Normal:取14.3N/mm",
            "Superscript:2",
            "Normal:。字面平方符号单独保留为²，不得写成普通数字2。"
        }));
        Assert.That(document.Blocks[10].Numbering, Is.Null);
        Assert.That(document.Blocks[10].Runs.Single().Text, Does.StartWith("注：1."));
        Assert.That(document.Blocks[12].Runs.Select(run => run.Text), Is.EqualTo(new[] { "框架梁纵向钢筋应通长设置。", "\n", "梁柱节点核心区箍筋应加密。" }));
        Assert.That(string.Concat(document.Blocks.SelectMany(block => block.Runs).Select(run => run.Text)), Does.Contain("Φ20@200").And.Contain("GB 50010-2010").And.Contain("²"));
        Assert.That(fromFile.Diagnostics.Any(item => item.Severity == Severity.Error), Is.False);
        Assert.That(fromFile.Diagnostics.Count(item => item.Code == DocxDiagnosticCodes.IgnoredHeaderFooter), Is.EqualTo(2));
    }

    private static DocumentParseResult Parse(byte[] bytes)
    {
        var parser = new DocxDocumentParser(new DocxParseOptions
        {
            DocumentId = "sample-note-001",
            DisciplineCode = "structure",
            StyleMap = new StyleMap
            {
                Entries =
                {
                    new StyleMapEntry { Match = StyleMapMatch.StyleId, Key = "Heading1", Target = BlockType.Heading1 },
                    new StyleMapEntry { Match = StyleMapMatch.StyleId, Key = "Heading2", Target = BlockType.Heading2 },
                    new StyleMapEntry { Match = StyleMapMatch.StyleId, Key = "Normal", Target = BlockType.Paragraph }
                }
            }
        });
        return parser.Parse(new DocxBytesSource("示例-结构说明.docx", bytes), new ParseProfile(), CancellationToken.None);
    }

    private static string Project(Document document)
    {
        return string.Join("\n", document.Blocks.Select(block =>
            block.Type + "#" + block.Numbering?.Label + "#"
            + string.Join("|", block.Runs.Select(run => run.Semantic + ":" + run.Text.Replace("\n", "\\n")))));
    }

    private static string Dump(DocumentParseResult result)
    {
        return string.Join(" | ", result.Diagnostics.Select(item => item.Severity + ":" + item.Code + ":" + item.Message));
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("找不到仓库根目录。");
    }
}
