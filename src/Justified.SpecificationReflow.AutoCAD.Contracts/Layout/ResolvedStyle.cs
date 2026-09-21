using Justified.SpecificationReflow.AutoCAD.Contracts.Documents;
using Justified.SpecificationReflow.AutoCAD.Contracts.Standards;

namespace Justified.SpecificationReflow.AutoCAD.Contracts.Layout;

public sealed class ResolvedStyle
{
    public string StyleId { get; set; } = string.Empty;

    public RunSemantic Semantic { get; set; }

    public FontEntry Font { get; set; } = new FontEntry();

    public double TextHeight { get; set; }

    public double WidthFactor { get; set; }

    public double ObliqueAngle { get; set; }
}
