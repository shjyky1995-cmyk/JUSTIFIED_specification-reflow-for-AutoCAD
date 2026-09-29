using Justified.SpecificationReflow.AutoCAD.Contracts.Geometry;

namespace Justified.SpecificationReflow.AutoCAD.Contracts.Layout;

public sealed class LayoutLine
{
    // x 相对栏左，y 为页内坐标，与 RowSlot.Baseline 使用相同坐标系。
    public Point2 Start { get; set; }
    public Point2 End { get; set; }
}
