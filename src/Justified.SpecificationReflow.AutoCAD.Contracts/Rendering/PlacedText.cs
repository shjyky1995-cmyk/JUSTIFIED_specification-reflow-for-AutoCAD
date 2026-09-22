using Justified.SpecificationReflow.AutoCAD.Contracts.Geometry;

namespace Justified.SpecificationReflow.AutoCAD.Contracts.Rendering;

// 已经换成世界坐标的一条 DBText。栏左坐标在放置时并入 Position，渲染器不再换行。
public sealed class PlacedText
{
    public string Text { get; set; } = string.Empty;

    public Point2 Position { get; set; }

    public double Height { get; set; }

    public double WidthFactor { get; set; }

    public double ObliqueDegrees { get; set; }

    public string StyleName { get; set; } = string.Empty;

    public string FontFile { get; set; } = string.Empty;

    public string BigFont { get; set; } = string.Empty;

    public string Layer { get; set; } = string.Empty;

    public int PageIndex { get; set; }

    public int ColumnIndex { get; set; }

    public int RowIndex { get; set; }
}
