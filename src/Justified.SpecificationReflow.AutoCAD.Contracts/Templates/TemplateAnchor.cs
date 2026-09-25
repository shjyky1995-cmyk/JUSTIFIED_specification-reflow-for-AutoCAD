namespace Justified.SpecificationReflow.AutoCAD.Contracts.Templates;

public sealed class TemplateAnchor
{
    public AnchorKind Kind { get; set; }

    public double X { get; set; }

    public double Y { get; set; }
}

// V1 锚点语义：点击图面左上角，说明区在整页范围内横向居中，顶部预留图框与标题栏空间（PRD 7.3）。
// NoteTopRight 为 2026-09-25 之前的旧语义，保留以兼容已安装的旧模板文件。
public enum AnchorKind
{
    NoteTopLeft,
    NoteTopRight
}
