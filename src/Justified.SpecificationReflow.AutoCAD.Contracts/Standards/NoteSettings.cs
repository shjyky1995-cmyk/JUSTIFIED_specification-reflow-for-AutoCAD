using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Justified.SpecificationReflow.AutoCAD.Contracts.Standards;

// 工程设置：一个项目或一张图设一次，之后 DN_NOTE 只点位置。存在当前图的 JSR_NOTE_SETTINGS 字典里。
public sealed class NoteSettings
{
    public string StandardRoot { get; set; } = string.Empty;

    public string StandardId { get; set; } = string.Empty;

    public string StandardVersion { get; set; } = string.Empty;

    public string TemplateId { get; set; } = string.Empty;

    public string TemplateVersion { get; set; } = string.Empty;

    public string PaperCode { get; set; } = string.Empty;

    public double UnitScale { get; set; }

    public string DocumentPath { get; set; } = string.Empty;

    public string? ReportDirectory { get; set; }

    public string SavedUtc { get; set; } = string.Empty;

    // 保存前自检。未知单位不猜，路径和引用不许留空。
    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>();
        Require(problems, StandardRoot, "标准包根目录");
        Require(problems, StandardId, "标准编号");
        Require(problems, StandardVersion, "标准版本");
        Require(problems, TemplateId, "模板编号");
        Require(problems, TemplateVersion, "模板版本");
        Require(problems, DocumentPath, "说明文档");
        if (!string.Equals(PaperCode, "A1", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(PaperCode, "A2", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(PaperCode, "A3", StringComparison.OrdinalIgnoreCase))
            problems.Add("图幅只能是 A1、A2 或 A3。");
        if (double.IsNaN(UnitScale) || double.IsInfinity(UnitScale) || UnitScale <= 0)
            problems.Add("单位比例必须是正数。未知单位时不能猜测。");
        if (!string.IsNullOrWhiteSpace(ReportDirectory) && ReportDirectory!.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            problems.Add("运行报告目录含有非法字符。");
        if (!string.IsNullOrWhiteSpace(SavedUtc)
            && !DateTimeOffset.TryParseExact(SavedUtc, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
            problems.Add("保存时间不是有效的 ISO 8601 时间。");
        return problems;
    }

    private static void Require(List<string> problems, string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) problems.Add(label + "不能为空。");
    }
}
