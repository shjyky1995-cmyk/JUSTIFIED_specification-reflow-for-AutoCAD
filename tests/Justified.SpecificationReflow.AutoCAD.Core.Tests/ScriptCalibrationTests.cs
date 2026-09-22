using System;
using System.IO;
using System.Linq;
using System.Threading;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Layout;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;
using Justified.SpecificationReflow.AutoCAD.Standards;
using NUnit.Framework;

namespace Justified.SpecificationReflow.AutoCAD.Core.Tests;

// 上下标标定：项目制定值、Problems 拒绝与 Apply 的缩放/偏移。PRD V1 有限输入契约。
public class ScriptCalibrationTests
{
    [Test]
    public void CompleteStandardHasNoProblemsAndScalesScripts()
    {
        var standard = LayoutSamples.Standard();
        Assert.That(ScriptCalibration.Problems(standard), Is.Empty);
        Assert.That(ScriptCalibration.IsComplete(standard), Is.True);

        var baseStyle = new ResolvedStyle
        {
            StyleId = "body",
            Semantic = RunSemantic.Normal,
            Font = new FontEntry { Family = "TSSD", FileIdentity = "tssdeng.shx" },
            TextHeight = 4.5,
            WidthFactor = 0.75,
            ObliqueAngle = 0
        };

        var normal = ScriptCalibration.Apply(baseStyle, RunSemantic.Normal);
        Assert.That(normal.Style.TextHeight, Is.EqualTo(4.5).Within(1e-9));
        Assert.That(normal.BaselineOffset, Is.EqualTo(0));

        var superscript = ScriptCalibration.Apply(baseStyle, RunSemantic.Superscript);
        Assert.That(superscript.Style.TextHeight, Is.EqualTo(3.15).Within(1e-9));
        Assert.That(superscript.BaselineOffset, Is.EqualTo(1.575).Within(1e-9));
        Assert.That(superscript.Style.Semantic, Is.EqualTo(RunSemantic.Superscript));

        var subscript = ScriptCalibration.Apply(baseStyle, RunSemantic.Subscript);
        Assert.That(subscript.Style.TextHeight, Is.EqualTo(3.15).Within(1e-9));
        Assert.That(subscript.BaselineOffset, Is.EqualTo(-0.9).Within(1e-9));
    }

    [Test]
    public void MissingOrOutOfRangeValuesAreRejected()
    {
        var missing = LayoutSamples.Standard(calibrateScripts: false);
        var problems = ScriptCalibration.Problems(missing);
        Assert.That(problems, Is.Not.Empty);
        Assert.That(problems.All(item => item.Contains("尚未标定")), Is.True);
        Assert.That(ScriptCalibration.IsComplete(missing), Is.False);

        var tooBig = LayoutSamples.Standard();
        tooBig.SuperscriptScale = 1.5;
        Assert.That(ScriptCalibration.Problems(tooBig).Single(), Does.Contain("0 到 1"));
        var negative = LayoutSamples.Standard();
        negative.SubscriptDrop = -0.1;
        Assert.That(ScriptCalibration.Problems(negative).Single(), Does.Contain("0 到 1"));
        Assert.That(ScriptCalibration.Problems(null).Single(), Does.Contain("为空"));
    }

    [Test]
    public void PublishedStandardRejectsMissingScriptCalibration()
    {
        var missing = new PackageValidator().ValidateStandard(LayoutSamples.Standard(calibrateScripts: false));
        Assert.That(missing.Any(item => item.Severity == Severity.Error && item.Message.Contains("上下标标定不完整")), Is.True);
        var complete = new PackageValidator().ValidateStandard(LayoutSamples.Standard());
        Assert.That(complete.Any(item => item.Severity == Severity.Error && item.Message.Contains("上下标")), Is.False);
    }

    [Test]
    public void DraftLoaderFillsScriptCalibrationInMemoryOnly()
    {
        var directory = Path.Combine(Root(), "standards", "drafts");
        var notePath = Path.Combine(directory, "jsr-note-1.0.0.json");
        var before = System.IO.File.ReadAllText(notePath);
        var session = new DraftSessionLoader().Load(directory, "A1", "jsr-linebreak-1.0.0", System.Threading.CancellationToken.None);
        Assert.That(session.Success, Is.True, string.Join(" | ", session.Diagnostics.Select(item => item.Message)));
        Assert.That(session.Standard!.SuperscriptScale, Is.EqualTo(ScriptCalibration.SuperscriptScale));
        Assert.That(session.Standard.SuperscriptRise, Is.EqualTo(ScriptCalibration.SuperscriptRise));
        Assert.That(session.Standard.SubscriptScale, Is.EqualTo(ScriptCalibration.SubscriptScale));
        Assert.That(session.Standard.SubscriptDrop, Is.EqualTo(ScriptCalibration.SubscriptDrop));
        Assert.That(ScriptCalibration.IsComplete(session.Standard), Is.True);
        Assert.That(session.Diagnostics.Count(item => item.Code == "DRAFT" && (item.Message.Contains("上标") || item.Message.Contains("下标"))), Is.EqualTo(4));
        Assert.That(System.IO.File.ReadAllText(notePath), Is.EqualTo(before));
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        if (dir == null) throw new InvalidOperationException("找不到仓库根目录。");
        return dir.FullName;
    }
}

public class FontGlyphCoverageTests
{
    [Test]
    public void KnownMissingGlyphsBlockAndOthersPass()
    {
        Assert.That(FontGlyphCoverage.FirstMissingGlyph("tssdeng.shx", "tssdchn.shx", "直径 Φ20@200 ①"), Is.EqualTo('\u2460'));
        Assert.That(FontGlyphCoverage.FirstMissingGlyph("tssdeng.shx", "tssdchn.shx", "面积 120m²，温度 -10℃ ≤ 25m/s"), Is.Null);
        Assert.That(FontGlyphCoverage.FirstMissingGlyph("tssdeng.shx", "tssdchn.shx", ""), Is.Null);
        Assert.That(FontGlyphCoverage.FirstMissingGlyph("tssdeng.shx", "tssdchn.shx", "⑳"), Is.EqualTo('\u2473'));
        Assert.That(FontGlyphCoverage.FirstMissingGlyph("other.shx", "tssdchn.shx", "①"), Is.EqualTo('\u2460'));
        Assert.That(FontGlyphCoverage.FirstMissingGlyph("unknown.shx", "unknown-big.shx", "①"), Is.Null);
    }

    [Test]
    public void LayoutBlocksDocumentWithMissingGlyph()
    {
        var document = LayoutSamples.DocumentOf(LayoutSamples.Runs(0, BlockType.Paragraph, null, LayoutSamples.Run("说明①")));
        var result = LayoutSamples.Engine().Layout(document, LayoutSamples.Standard(), LayoutSamples.Columns(60), new FakeMeasure(), CancellationToken.None);
        Assert.That(result.Pages, Is.Empty);
        Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(DiagnosticCodes.EFontMissing));
        Assert.That(result.Diagnostics.Single().Message, Does.Contain("没有字形"));

        var fine = LayoutSamples.Engine().Layout(
            LayoutSamples.DocumentOf(LayoutSamples.Runs(0, BlockType.Paragraph, null, LayoutSamples.Run("直径 Φ20@200，温度 -10℃"))),
            LayoutSamples.Standard(),
            LayoutSamples.Columns(60),
            new FakeMeasure(),
            CancellationToken.None);
        LayoutSamples.Ok(fine);
    }
}
