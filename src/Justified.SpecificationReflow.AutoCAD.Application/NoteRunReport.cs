using System;
using System.Collections.Generic;
using System.Linq;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;
using Justified.SpecificationReflow.AutoCAD.Contracts.Templates;

namespace Justified.SpecificationReflow.AutoCAD.Application;

// 一次生成的本地运行报告。宿主按设置把它写成 JSON 文件，供 T11 性能和 T12 验收核对。
public sealed class NoteRunReport
{
    public string InputHash { get; set; } = string.Empty;
    public string EngineVersion { get; set; } = string.Empty;
    public string LineBreakRuleVersion { get; set; } = string.Empty;
    public string SymbolMapVersion { get; set; } = string.Empty;
    public int Lines { get; set; }
    public double ReadMilliseconds { get; set; }
    public double ParseMilliseconds { get; set; }
    public double LayoutMilliseconds { get; set; }
    public double PlacementMilliseconds { get; set; }
    public double PreparationMilliseconds { get; set; }
    public double UserWaitMilliseconds { get; set; }
    public double RenderMilliseconds { get; set; }
    public double EngineMilliseconds => ReadMilliseconds + ParseMilliseconds + LayoutMilliseconds + PlacementMilliseconds + RenderMilliseconds;
    // 指本进程首次执行生成命令，不冒称 AutoCAD 启动/插件加载耗时。
    public bool FirstRunInProcess { get; set; }

    public string StartedUtc { get; set; } = string.Empty;

    public string FinishedUtc { get; set; } = string.Empty;

    public long ElapsedMilliseconds { get; set; }

    public string DrawingPath { get; set; } = string.Empty;

    public string DocumentPath { get; set; } = string.Empty;

    public string StandardId { get; set; } = string.Empty;

    public string StandardVersion { get; set; } = string.Empty;

    public string TemplateId { get; set; } = string.Empty;

    public string TemplateVersion { get; set; } = string.Empty;

    public string PaperCode { get; set; } = string.Empty;

    public double UnitScale { get; set; }

    public bool Success { get; set; }

    public bool Committed { get; set; }

    public int Pages { get; set; }

    public int Objects { get; set; }

    public List<string> Warnings { get; set; } = new List<string>();

    public List<string> Errors { get; set; } = new List<string>();

    public List<string> TextBounds { get; set; } = new List<string>();
}

public static class NoteRunReportBuilder
{
    public static NoteRunReport Create(
        string drawingPath,
        NoteSettings settings,
        InstitutionStandard standard,
        LayoutTemplate template,
        NoteGenerationResult generated,
        RenderReportResult rendered,
        DateTimeOffset startedUtc,
        DateTimeOffset finishedUtc)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        if (standard == null) throw new ArgumentNullException(nameof(standard));
        if (template == null) throw new ArgumentNullException(nameof(template));
        if (generated == null) throw new ArgumentNullException(nameof(generated));
        if (rendered == null) throw new ArgumentNullException(nameof(rendered));

        var report = new NoteRunReport
        {
            InputHash = generated.Document?.Source.ContentHash ?? string.Empty,
            EngineVersion = generated.Layout?.EngineVersion ?? string.Empty,
            LineBreakRuleVersion = standard.LineBreakRuleVersion,
            SymbolMapVersion = standard.SymbolMapVersion,
            Lines = generated.Layout?.Pages.Sum(page => page.Columns.Sum(column => column.Rows.Count(row => row.VisualLine != null))) ?? 0,
            ReadMilliseconds = generated.ReadMilliseconds,
            ParseMilliseconds = generated.ParseMilliseconds,
            LayoutMilliseconds = generated.LayoutMilliseconds,
            PlacementMilliseconds = generated.PlacementMilliseconds,
            StartedUtc = startedUtc.UtcDateTime.ToString("O"),
            FinishedUtc = finishedUtc.UtcDateTime.ToString("O"),
            ElapsedMilliseconds = (long)(finishedUtc - startedUtc).TotalMilliseconds,
            DrawingPath = drawingPath,
            DocumentPath = settings.DocumentPath,
            StandardId = standard.StandardId,
            StandardVersion = standard.Version,
            TemplateId = template.TemplateId,
            TemplateVersion = template.Version,
            PaperCode = settings.PaperCode,
            UnitScale = settings.UnitScale,
            Success = generated.Success && rendered.Success,
            Committed = rendered.Committed,
            Pages = rendered.Pages,
            Objects = rendered.Objects
        };
        foreach (var diagnostic in generated.Diagnostics)
        {
            if (diagnostic == null) continue;
            var line = diagnostic.Code + " " + diagnostic.Message;
            if (diagnostic.Severity == Severity.Warning) report.Warnings.Add(line);
            else if (diagnostic.Severity == Severity.Error) report.Errors.Add(line);
        }

        foreach (var diagnostic in rendered.Diagnostics)
        {
            if (diagnostic == null) continue;
            if (diagnostic.Severity == Severity.Error) report.Errors.Add(diagnostic.Code + " " + diagnostic.Message);
            else if (diagnostic.Severity == Severity.Warning) report.Warnings.Add(diagnostic.Code + " " + diagnostic.Message);
        }
        report.Success = report.Success && report.Errors.Count == 0;

        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;
        foreach (var text in generated.Texts)
        {
            if (text == null) continue;
            minX = Math.Min(minX, text.Position.X);
            minY = Math.Min(minY, text.Position.Y);
            maxX = Math.Max(maxX, text.Position.X);
            maxY = Math.Max(maxY, text.Position.Y);
        }

        if (generated.Texts.Count > 0)
            report.TextBounds.Add(minX.ToString("G17", System.Globalization.CultureInfo.InvariantCulture) + ","
                + minY.ToString("G17", System.Globalization.CultureInfo.InvariantCulture) + ","
                + maxX.ToString("G17", System.Globalization.CultureInfo.InvariantCulture) + ","
                + maxY.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
        return report;
    }
}

// 落图结果的宿主无关镜像，供报告组装；宿主把 RenderReport 投影到这个类型。
public sealed class RenderReportResult
{
    public bool Success { get; set; }

    public bool Committed { get; set; }

    public int Pages { get; set; }

    public int Objects { get; set; }

    public List<Diagnostic> Diagnostics { get; set; } = new List<Diagnostic>();
}
