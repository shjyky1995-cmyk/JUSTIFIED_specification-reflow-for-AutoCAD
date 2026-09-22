using System.Collections.Generic;
using Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics;

namespace Justified.SpecificationReflow.AutoCAD.Standards;

// 目录里一份模板的摘要，供设置命令挑选。宽松读取，只做展示，不替代 Load 的发布校验。
public sealed class TemplateSummary
{
    public string TemplateId { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string PaperCode { get; set; } = string.Empty;

    public string DisciplineCode { get; set; } = string.Empty;

    public string Classification { get; set; } = string.Empty;

    public bool Calibrated { get; set; }

    public string FilePath { get; set; } = string.Empty;
}

public sealed class TemplateListResult
{
    public List<TemplateSummary> Templates { get; } = new List<TemplateSummary>();

    public List<Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics.Diagnostic> Diagnostics { get; } = new List<Justified.SpecificationReflow.AutoCAD.Contracts.Diagnostics.Diagnostic>();
}
