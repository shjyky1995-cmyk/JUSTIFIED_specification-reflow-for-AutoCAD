namespace Justified.SpecificationReflow.AutoCAD.Contracts.Templates;

public sealed class TemplateAnchor
{
    public AnchorKind Kind { get; set; }

    public double X { get; set; }

    public double Y { get; set; }
}

// V1 只有一种锚点语义：模板说明区固定右上角（PRD 7.3）。
public enum AnchorKind
{
    NoteTopRight
}
