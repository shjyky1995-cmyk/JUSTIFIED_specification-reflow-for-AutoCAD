using Justified.SpecificationReflow.AutoCAD.Contracts.Geometry;
using Justified.SpecificationReflow.AutoCAD.Contracts.Layout;

namespace Justified.SpecificationReflow.AutoCAD.Contracts.Rendering;

public sealed class RenderRequest
{
    public LayoutResult LayoutResult { get; set; } = new LayoutResult();

    public Point2 AnchorWcs { get; set; }

    public double UnitScale { get; set; }

    public TargetSpace TargetSpace { get; set; }

    public string EnvironmentFingerprint { get; set; } = string.Empty;
}
