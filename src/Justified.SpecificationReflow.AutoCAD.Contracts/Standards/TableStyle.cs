namespace Justified.SpecificationReflow.AutoCAD.Contracts.Standards;

// 留白按字高计；线宽以百分之一毫米表示，与 CAD LineWeight 对应。
public sealed class TableStyle
{
    public string AutoWidthPolicy { get; set; } = "reject";
    public double HorizontalPaddingEm { get; set; }
    public double VerticalPaddingEm { get; set; }
    public int BeforeSlots { get; set; }
    public int AfterSlots { get; set; }
    public string Layer { get; set; } = string.Empty;
    public string Linetype { get; set; } = string.Empty;
    public int Lineweight { get; set; }
}
