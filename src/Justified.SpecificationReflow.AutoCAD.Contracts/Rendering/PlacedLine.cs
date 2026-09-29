using Justified.SpecificationReflow.AutoCAD.Contracts.Geometry;

namespace Justified.SpecificationReflow.AutoCAD.Contracts.Rendering;

public sealed class PlacedLine
{
    public int PageIndex { get; set; }
    public Point2 Start { get; set; }
    public Point2 End { get; set; }
    public string Layer { get; set; } = string.Empty;
    public string Linetype { get; set; } = string.Empty;
    public int Lineweight { get; set; }
}
