using System;
using System.Collections.Generic;
using System.Linq;
using Justified.SpecificationReflow.AutoCAD.Application;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Geometry;
using Justified.SpecificationReflow.AutoCAD.Contracts.Layout;
using Justified.SpecificationReflow.AutoCAD.Contracts.Rendering;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;
using NUnit.Framework;

namespace Justified.SpecificationReflow.AutoCAD.Core.Tests;

// 运行报告：T11 性能和 T12 验收要核对每次生成的页数、对象数和告警。
public class NoteRunReportTests
{
    [Test]
    public void ReportCollectsStandardTemplateBoundsAndDiagnostics()
    {
        var settings = new NoteSettings
        {
            StandardRoot = @"G:\packs\jsr",
            StandardId = "jsr-note",
            StandardVersion = "1.0.0",
            TemplateId = "jsr-A1-three-column",
            TemplateVersion = "1.0.0",
            PaperCode = "A1",
            UnitScale = 2,
            DocumentPath = @"G:\notes\说明.docx",
            SavedUtc = DateTimeOffset.Parse("1970-01-11T00:00:00.0000000+00:00").ToString("O")
        };
        var standard = new InstitutionStandard { StandardId = "jsr-note", Version = "1.0.0" };
        var template = new Contracts.Templates.LayoutTemplate { TemplateId = "jsr-A1-three-column", Version = "1.0.0" };

        var generated = new NoteGenerationResult
        {
            Success = true,
            Texts =
            {
                new PlacedText { Text = "第一行", Position = new Point2 { X = -710, Y = -6.2 }, Height = 9, WidthFactor = 0.75, PageIndex = 0 },
                new PlacedText { Text = "第二行", Position = new Point2 { X = -710, Y = -13.4 }, Height = 9, WidthFactor = 0.75, PageIndex = 0 },
                new PlacedText { Text = "第三行", Position = new Point2 { X = 151, Y = -6.2 }, Height = 9, WidthFactor = 0.75, PageIndex = 1 }
            },
            Diagnostics =
            {
                new Diagnostic { Code = "W_TOKEN_SPLIT", Severity = Severity.Warning, Message = "长 token 被拆断。" },
                new Diagnostic { Code = "E_PARSE", Severity = Severity.Error, Message = "不应出现在成功路径。" }
            }
        };
        var rendered = new RenderReportResult
        {
            Success = true,
            Committed = true,
            Pages = 2,
            Objects = 3
        };

        var started = DateTimeOffset.Parse("2026-09-22T06:30:00.0000000+00:00");
        var finished = started.AddMilliseconds(1200);
        var report = NoteRunReportBuilder.Create(@"G:\draw\Drawing1.dwg", settings, standard, template, generated, rendered, started, finished);

        Assert.That(report.Success, Is.True);
        Assert.That(report.Committed, Is.True);
        Assert.That(report.ElapsedMilliseconds, Is.EqualTo(1200));
        Assert.That(report.DrawingPath, Is.EqualTo(@"G:\draw\Drawing1.dwg"));
        Assert.That(report.DocumentPath, Is.EqualTo(@"G:\notes\说明.docx"));
        Assert.That(report.StandardId, Is.EqualTo("jsr-note"));
        Assert.That(report.TemplateVersion, Is.EqualTo("1.0.0"));
        Assert.That(report.PaperCode, Is.EqualTo("A1"));
        Assert.That(report.UnitScale, Is.EqualTo(2));
        Assert.That(report.Pages, Is.EqualTo(2));
        Assert.That(report.Objects, Is.EqualTo(3));
        Assert.That(report.Warnings.Single(), Does.Contain("W_TOKEN_SPLIT"));
        Assert.That(report.Errors.Single(), Does.Contain("E_PARSE"));
        var bounds = report.TextBounds.Single().Split(',');
        Assert.That(bounds, Has.Length.EqualTo(4));
        Assert.That(double.Parse(bounds[0]), Is.EqualTo(-710));
        Assert.That(double.Parse(bounds[1]), Is.EqualTo(-13.4));
        Assert.That(double.Parse(bounds[2]), Is.EqualTo(151));
        Assert.That(double.Parse(bounds[3]), Is.EqualTo(-6.2));
        Assert.That(DateTimeOffset.Parse(report.StartedUtc), Is.EqualTo(started));
    }

    [Test]
    public void FailedRunMarksNotCommittedAndHasNoBounds()
    {
        var settings = Complete();
        var standard = new InstitutionStandard { StandardId = "jsr-note", Version = "1.0.0" };
        var template = new Contracts.Templates.LayoutTemplate { TemplateId = "jsr-A1-three-column", Version = "1.0.0" };
        var generated = new NoteGenerationResult { Success = false };
        var rendered = new RenderReportResult { Success = false, Committed = false };
        var now = DateTimeOffset.Parse("2026-09-22T06:30:00.0000000+00:00");
        var report = NoteRunReportBuilder.Create("Drawing1", settings, standard, template, generated, rendered, now, now.AddMilliseconds(50));
        Assert.That(report.Success, Is.False);
        Assert.That(report.Committed, Is.False);
        Assert.That(report.Objects, Is.Zero);
        Assert.That(report.TextBounds, Is.Empty);
    }

    private static NoteSettings Complete()
    {
        return new NoteSettings
        {
            StandardRoot = @"G:\packs\jsr",
            StandardId = "jsr-note",
            StandardVersion = "1.0.0",
            TemplateId = "jsr-A1-three-column",
            TemplateVersion = "1.0.0",
            PaperCode = "A1",
            UnitScale = 1,
            DocumentPath = @"G:\notes\说明.docx",
            SavedUtc = DateTimeOffset.Parse("1970-01-01T00:00:00.0000000+00:00").ToString("O")
        };
    }
}
