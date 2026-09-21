using Justified.SpecificationReflow.AutoCAD.Contracts.Geometry;

namespace Justified.SpecificationReflow.AutoCAD.Contracts.Rendering;

public sealed class RenderTransform
{
    public Point2 AnchorWcs { get; set; }

    public double UnitScale { get; set; }

    public TargetSpace TargetSpace { get; set; }
}

// V1 Baseline 仅在模型空间生成（PRD 7.3）；纸空间为后续能力。
public enum TargetSpace
{
    Model
}
