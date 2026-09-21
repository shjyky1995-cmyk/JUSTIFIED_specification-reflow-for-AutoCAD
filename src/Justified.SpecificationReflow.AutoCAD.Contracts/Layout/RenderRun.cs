using Justified.SpecificationReflow.AutoCAD.Contracts.Geometry;

namespace Justified.SpecificationReflow.AutoCAD.Contracts.Layout;

public sealed class RenderRun
{
    public string Text { get; set; } = string.Empty;

    // 相对所在行槽的局部原点（栏左边界与该行基线）。
    public Point2 RelativeOrigin { get; set; }

    public double BaselineOffset { get; set; }

    public ResolvedStyle ResolvedStyle { get; set; } = new ResolvedStyle();

    public double MeasuredAdvance { get; set; }

    public Bounds2 InkBounds { get; set; }
}
