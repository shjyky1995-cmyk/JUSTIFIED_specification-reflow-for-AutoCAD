namespace Justified.SpecificationReflow.AutoCAD.Contracts.Documents;

public sealed class SourceInfo
{
    public DocumentSourceKind Kind { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ContentHash { get; set; } = string.Empty;

    // 仅本地诊断可选，不作为身份（PRD 5.2）。
    public string? LocalPath { get; set; }
}

public enum DocumentSourceKind
{
    Docx
}
