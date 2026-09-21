using System.Threading;
using Justified.SpecificationReflow.AutoCAD.Contracts.Layout;
using Justified.SpecificationReflow.AutoCAD.Contracts.Rendering;

namespace Justified.SpecificationReflow.AutoCAD.Contracts.Ports;

public interface ICadRenderer
{
    RenderReport Render(LayoutResult result, RenderTransform transform, CancellationToken cancellationToken);
}
