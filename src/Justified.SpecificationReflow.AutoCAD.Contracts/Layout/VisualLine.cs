using System.Collections.Generic;
using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using Justified.SpecificationReflow.AutoCAD.Contracts.Geometry;

namespace Justified.SpecificationReflow.AutoCAD.Contracts.Layout;

public sealed class VisualLine
{
    public List<SourceRef> SourceSlices { get; set; } = new List<SourceRef>();

    public string Text { get; set; } = string.Empty;

    public double MeasuredWidth { get; set; }

    public Bounds2 InkBounds { get; set; }

    public List<RenderRun> RenderRuns { get; set; } = new List<RenderRun>();
}
